using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using TaxChain.core;

namespace TaxChain.Daemon.Storage;

public class PGSQLRepository : IBlockchainRepository
{
    private readonly string _connectionString;
    private readonly string _databaseName;
    private readonly ILogger<PGSQLRepository> _logger;
    public PGSQLRepository(ILogger<PGSQLRepository> logger)
    {
        _logger = logger;
        // Load configuration from environment variables
        var host = Environment.GetEnvironmentVariable("TAXCHAIN_DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("TAXCHAIN_DB_PORT") ?? "5432";
        var username = Environment.GetEnvironmentVariable("TAXCHAIN_DB_USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("TAXCHAIN_DB_PASSWORD") ?? "postgres";
        _databaseName = Environment.GetEnvironmentVariable("TAXCHAIN_DB_NAME") ?? "taxchain";

        _connectionString = $"Host={host};Port={port};Username={username};Password={password};Database={_databaseName}";
    }

    public void Initialize()
    {
        var adminConnStr = Environment.GetEnvironmentVariable("TAXCHAIN_ADMIN_DB") ?? "Host=localhost;Username=postgres;Password=postgres;Database=postgres";
        EnsureDatabaseExists(adminConnStr);
        CreateTables();
    }

    private NpgsqlConnection GetConnection() => new(_connectionString);

    private void CreateTables()
    {
        string[] sqlStatements =
        {
            @"CREATE TABLE IF NOT EXISTS chains (
                id UUID PRIMARY KEY,
                name TEXT NOT NULL,
                reward INTEGER DEFAULT 0,
                difficulty INTEGER DEFAULT 1,
                latest_block INTEGER DEFAULT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );",

            @"CREATE TABLE IF NOT EXISTS blocks (
                id SERIAL PRIMARY KEY,
                chain_id UUID REFERENCES chains(id),
                prev_hash TEXT NOT NULL,
                hash TEXT NOT NULL,
                nonce INTEGER NOT NULL,
                timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(id, chain_id)
            );",

            @"CREATE TABLE IF NOT EXISTS transactions (
                chain_id UUID REFERENCES chains(id),
                block_id INTEGER REFERENCES blocks(id),
                identifier UUID UNIQUE,
                taxpayer_id TEXT NOT NULL,
                jurisdiction TEXT NOT NULL,
                amount DECIMAL(18,8) NOT NULL,
                taxable_base DECIMAL(18,8),
                tax_rate DECIMAL(18,8),
                tax_type TEXT NOT NULL,
                status TEXT NOT NULL,
                notes TEXT,
                period_start TIMESTAMP NOT NULL,
                period_end TIMESTAMP NOT NULL,
                due_date TIMESTAMP NOT NULL,
                payment_date TIMESTAMP
            );",

            @"CREATE TABLE IF NOT EXISTS pending_transactions (
                id UUID PRIMARY KEY,
                chain_id UUID REFERENCES chains(id),
                sender TEXT,
                receiver TEXT,
                amount DECIMAL(18,8) NOT NULL,
                tax_type TEXT NOT NULL,
                data JSONB,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );"
        };

        try
        {
            using var connection = GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            foreach (var sql in sqlStatements)
            {
                using var cmd = new NpgsqlCommand(sql, connection, transaction);
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
            _logger.LogInformation("Database tables created/verified successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating tables: {ex.Message}");
            throw;
        }
    }

    private void EnsureDatabaseExists(string adminConnectionString)
    {
        try
        {
            using var connection = new NpgsqlConnection(adminConnectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @dbName", connection);
            cmd.Parameters.AddWithValue("dbName", _databaseName);

            var result = cmd.ExecuteScalar();
            if (result == null)
            {
                using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
                createCmd.ExecuteNonQuery();
                _logger.LogInformation($"Database '{_databaseName}' created successfully.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error ensuring database exists: {ex.Message}");
            throw;
        }
    }
    public bool EnqueueTransaction(Guid chainId, Transaction transaction)
    {
        try
        {
            using var connection = new NpgsqlConnection();
            connection.Open();
            using var t = connection.BeginTransaction();

            bool ok = StorePendingTransaction(chainId, null, transaction, connection, t);
            if (!ok)
            {
                _logger.LogError("Failed to store pending transaction");
                return false;
            }
            t.Commit();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to store pending transaction: {ex}");
            return false;
        }
    }

    public bool Fetch(Guid chainId, out Block[] blocks)
    {
        throw new NotImplementedException();
    }

    public bool FetchPending(Guid chainId, out Transaction transaction)
    {
        transaction = new();
        try
        {
            using var connection = GetConnection();
            connection.Open();
            using var t = connection.BeginTransaction();
            var fetchSQL = @"
                SELECT (id, created_at)
                FROM pending_transactions
            ";
            using var fetchCmd = new NpgsqlCommand(fetchSQL, connection, t);
            var reader = fetchCmd.ExecuteReader();
            var results = new List<FetchedT>();
            // Sort results based on created_at field
            while (reader.Read())
            {
                int id = reader.GetInt32(reader.GetOrdinal("id"));
                DateTime createdAt = reader.GetDateTime(reader.GetOrdinal("created_at"));
                results.Add(new FetchedT { Id = id, CreatedAt = createdAt });
            }
            if (results.Count == 0)
            {
                return true;
            }
            results.Sort(new Comparer());
            FetchedT last = results[-1];
            // Fetch last
            var lastSQL = @"
                SELECT (id, chain_id, block_id, taxpayer_id, jurisdiction, amount, taxable_base, tax_rate, tax_type, status, notes, period_start, period_end, due_date, payment_date)
                FROM transactions
                WHERE id=@id, chain_id=@chainId; 
            ";
            using var lastCmd = new NpgsqlCommand(lastSQL, connection, t);
            lastCmd.Parameters.AddWithValue("id", last.Id);
            lastCmd.Parameters.AddWithValue("chainId", chainId);
            var result = lastCmd.ExecuteReader();
            if (reader.Read())
            {
                transaction.Amount = reader.GetDecimal(reader.GetOrdinal("amount"));
                transaction.DueDate = reader.GetDateTime(reader.GetOrdinal("due_date"));
                transaction.Jurisdiction = reader.GetString(reader.GetOrdinal("jurisdiction"));
                transaction.PaymentDate = reader.GetDateTime(reader.GetOrdinal("payment_date"));
                transaction.TaxableBase = reader.IsDBNull(reader.GetOrdinal("taxable_base")) ? null : reader.GetDecimal(reader.GetOrdinal("taxable_base"));
                transaction.TaxRate = reader.IsDBNull(reader.GetOrdinal("tax_rate")) ? null : reader.GetDecimal(reader.GetOrdinal("tax_rate"));
                transaction.TaxPeriodStart = reader.GetDateTime(reader.GetOrdinal("period_start"));
                transaction.TaxPeriodEnd = reader.GetDateTime(reader.GetOrdinal("period_end"));
                transaction.TaxpayerId = reader.GetString(reader.GetOrdinal("taxpayer_id"));
                transaction.Status = (Transaction.TaxStatus)reader.GetInt32(reader.GetOrdinal("status"));
                transaction.Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes"));
                transaction.Type = (Transaction.TaxType)reader.GetInt32(reader.GetOrdinal("tax_type"));
            }
            t.Commit();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to fetch any pending transaction: {ex}", ex);
            return false;
        }
    }

    private readonly struct FetchedT { public int Id { get; init; } public DateTime CreatedAt { get; init; } };

    private class Comparer : IComparer<FetchedT>
    {
        public int Compare(FetchedT x, FetchedT y)
        {
            return x.CreatedAt.CompareTo(y.CreatedAt);
        }
    }

    public bool RemoveChain(Guid chainId)
    {
        try
        {
            using var connection = GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var delSql = @"
                DELETE FROM chains
                WHERE id=@id;
            ";
            using var delCmd = new NpgsqlCommand(delSql, connection, transaction);
            delCmd.Parameters.AddWithValue("id", chainId);
            transaction.Commit();
            return delCmd.ExecuteNonQuery() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting blockchain: {ex.Message}");
            return false;
        }
    }


    public bool Store(Blockchain blockchain)
    {
        try
        {
            using var connection = GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            // Store chain info
            var chainSql = @"
                INSERT INTO chains (id, name, reward, difficulty)
                VALUES (@id, @name, @reward, @difficulty)";

            using var chainCmd = new NpgsqlCommand(chainSql, connection, transaction);
            chainCmd.Parameters.AddWithValue("id", blockchain.Id);
            chainCmd.Parameters.AddWithValue("name", blockchain.Name ?? "Unnamed Chain");
            chainCmd.Parameters.AddWithValue("reward", blockchain.RewardAmount);
            chainCmd.Parameters.AddWithValue("difficulty", blockchain.Difficulty);
            chainCmd.ExecuteNonQuery();

            bool ok = SetUpGenesisBlock(blockchain.Id, connection, transaction);
            if (!ok)
            {
                _logger.LogWarning("Chain created, failed to set up genesis block");
                return false;
            }

            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error storing blockchain: {ex.Message}");
            return false;
        }
    }

    private bool SetUpGenesisBlock(Guid chainId, NpgsqlConnection conn, NpgsqlTransaction t)
    {
        var genSql = @"
            INSERT INTO (chain_id, prev_hash, hash, nonce)
            VALUES (@chain_id, @prev_hash, @hash, @nonce)
        ";
        var genCmd = new NpgsqlCommand(genSql, conn, t);
        genCmd.Parameters.AddWithValue("chain_id", chainId);
        genCmd.Parameters.AddWithValue("prev_hash", "");
        genCmd.Parameters.AddWithValue("hash", "");
        genCmd.Parameters.AddWithValue("nonce", 0);
        return genCmd.ExecuteNonQuery() > 0;
    }


    private bool UpdateLatestBlock(Guid chain_id, int? blockId, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        if (blockId == null)
        {
            _logger.LogWarning("Block id is null, cannot update latest block id");
            return false;
        }
        try
        {
            var updateSql = @"
                UPDATE chains
                SET latest_block=@block_id
                WHERE id=@id;
            ";
            var updateCmd = new NpgsqlCommand(updateSql, connection, transaction);
            updateCmd.Parameters.AddWithValue("id", chain_id);
            updateCmd.Parameters.AddWithValue("block_id", blockId);

            int rows = updateCmd.ExecuteNonQuery();
            if (rows == 0)
            {
                _logger.LogWarning("No rows affected when updating latest block id");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to update chain's latest block: {ex}");
            return false;
        }
    }

    private bool DoesBlockchainExist(Guid chainId, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        try
        {
            var checkSql = @"
                SELECT EXISTS(SELECT 1 FROM chains WHERE id=@id);
            ";
            var checkCmd = new NpgsqlCommand(checkSql, connection, transaction);
            checkCmd.Parameters.AddWithValue("id", chainId);
            var result = checkCmd.ExecuteScalar();
            return result is bool exists && exists;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to check blockchain exists: {ex}");
            return false;
        }
    }

    private bool StoreTransaction(Guid chainId, int? blockId, Transaction t, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        if (blockId == null)
        {
            return false;
        }
        try
        {
            var tSql = @$"
            INSERT INTO transactions (chain_id, block_id, identifier, taxpayer_id, jurisdiction, amount, taxable_base, tax_rate, tax_type, status, notes, period_start, period_end, due_date, payment_date)
            VALUES (@chain_id, @block_id, @taxpayer_id, @jurisdiction, @amount, @taxable_base, @tax_rate, @tax_type, @status, @notes, @period_start, @period_end, @due_date, @payment_date)";
            using var tCommand = new NpgsqlCommand(tSql, connection, transaction);
            tCommand.Parameters.AddWithValue("chain_id", chainId);
            tCommand.Parameters.AddWithValue("block_id", blockId);
            tCommand.Parameters.AddWithValue("identifier", new Guid());
            tCommand.Parameters.AddWithValue("taxpayer_id", t.TaxpayerId);
            tCommand.Parameters.AddWithValue("jurisdiction", t.Jurisdiction);
            tCommand.Parameters.AddWithValue("amount", t.Amount);
            tCommand.Parameters.AddWithValue("taxable_base", t.TaxableBase == null ? DBNull.Value : t.TaxableBase);
            tCommand.Parameters.AddWithValue("tax_rate", t.TaxRate == null ? DBNull.Value : t.TaxRate);
            tCommand.Parameters.AddWithValue("tax_type", t.Type);
            tCommand.Parameters.AddWithValue("status", t.Status);
            tCommand.Parameters.AddWithValue("notes", t.Notes == null ? DBNull.Value : t.Notes);
            tCommand.Parameters.AddWithValue("period_start", t.TaxPeriodStart);
            tCommand.Parameters.AddWithValue("period_end", t.TaxPeriodEnd);
            tCommand.Parameters.AddWithValue("due_date", t.DueDate);
            tCommand.Parameters.AddWithValue("payment_date", t.PaymentDate);

            tCommand.ExecuteNonQuery();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to add transaction: {ex}");
            return false;
        }
    }

    private bool StorePendingTransaction(Guid chainId, int? blockId, Transaction t, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        if (blockId == null)
        {
            return false;
        }
        try
        {
            Guid tID = new Guid();
            var tSql = @$"
            INSERT INTO pending_transctions (id, chain_id, block_id, taxpayer_id, jurisdiction, amount, taxable_base, tax_rate, tax_type, status, notes, period_start, period_end, due_date, payment_date)
            VALUES (@id, @chain_id, @block_id, @taxpayer_id, @jurisdiction, @amount, @taxable_base, @tax_rate, @tax_type, @status, @notes, @period_start, @period_end, @due_date, @payment_date)";
            using var tCommand = new NpgsqlCommand(tSql, connection, transaction);
            tCommand.Parameters.AddWithValue("id", tID);
            tCommand.Parameters.AddWithValue("chain_id", chainId);
            tCommand.Parameters.AddWithValue("block_id", blockId);
            tCommand.Parameters.AddWithValue("taxpayer_id", t.TaxpayerId);
            tCommand.Parameters.AddWithValue("jurisdiction", t.Jurisdiction);
            tCommand.Parameters.AddWithValue("amount", t.Amount);
            tCommand.Parameters.AddWithValue("taxable_base", t.TaxableBase == null ? DBNull.Value : t.TaxableBase);
            tCommand.Parameters.AddWithValue("tax_rate", t.TaxRate == null ? DBNull.Value : t.TaxRate);
            tCommand.Parameters.AddWithValue("tax_type", t.Type);
            tCommand.Parameters.AddWithValue("status", t.Status);
            tCommand.Parameters.AddWithValue("notes", t.Notes == null ? DBNull.Value : t.Notes);
            tCommand.Parameters.AddWithValue("period_start", t.TaxPeriodStart);
            tCommand.Parameters.AddWithValue("period_end", t.TaxPeriodEnd);
            tCommand.Parameters.AddWithValue("due_date", t.DueDate);
            tCommand.Parameters.AddWithValue("payment_date", t.PaymentDate);

            tCommand.ExecuteNonQuery();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to add transaction: {ex}");
            return false;
        }
    }

    public bool Tail(Guid chainId, int n, out Block[] blocks)
    {
        blocks = new Block[n];
        try
        {
            using var connection = GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            // Fetch latest block
            var latestBlockSql = @"
                SELECT latest_block
                FROM chains
                WHERE id=@id;
            ";
            var latestBlockCmd = new NpgsqlCommand(latestBlockSql, connection, transaction);
            latestBlockCmd.Parameters.AddWithValue("id", chainId);
            var result = latestBlockCmd.ExecuteScalar();
            int? blockId = (int?)result;
            if (blockId == null)
            {
                _logger.LogWarning("Failed to convert latest block id to int");
                return false;
            }

            // Fetch block
            Block? tailBlock = GetBlock(chainId, blockId, connection, transaction);
            if (tailBlock == null)
                return false;
            blocks[0] = tailBlock;
            for (int i = 1; i < n; ++i)
            {
                Block? next = FindBlockByHash(chainId, blocks[i - 1].Hash, connection, transaction);
                if (next == null)
                {
                    _logger.LogWarning($"Stopped prematurely at {i}th index");
                    return true;
                }
                blocks[i] = next;
                if (next.Hash == "0")
                {
                    _logger.LogWarning($"Reached the beginning of the chain at index {i}");
                    return true;
                }
            }
            transaction?.Commit();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception when fetching tail: {ex}");
            return false;
        }
    }

    public Block? GetBlock(Guid chainId, int? blockId, NpgsqlConnection connection, NpgsqlTransaction? transaction)
    {
        if (blockId == null || transaction == null)
            return null;
        try
        {
            var blockSql = @"
            SELECT id, chain_id, prev_hash, hash, nonce, timestamp
            FROM blocks
            WHERE id=@id AND chain_id=@chain_id;
            ";
            using var blockCommand = new NpgsqlCommand(blockSql, connection, transaction);
            blockCommand.Parameters.AddWithValue("id", blockId);
            blockCommand.Parameters.AddWithValue("chain_id", chainId);

            using var reader = blockCommand.ExecuteReader();
            if (reader.Read())
            {
                var id = reader.GetInt32(reader.GetOrdinal("id"));
                var chain_id = reader.GetGuid(reader.GetOrdinal("chain_id"));
                var prev_hash = reader.GetString(reader.GetOrdinal("prev_hash"));
                var hash = reader.GetString(reader.GetOrdinal("hash"));
                var nonce = reader.GetInt32(reader.GetOrdinal("nonce"));
                var timestamp = reader.GetDateTime(reader.GetOrdinal("timestamp"));

                var block = new Block(
                    chain_id,
                    prev_hash,
                    hash,
                    nonce,
                    timestamp,
                    []
                );

                return block;
            }
            else
            {
                _logger.LogWarning($"Block not found: id={blockId}, chain_id={chainId}");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception during block fetch: {ex}");
            return null;
        }
    }

    private Block? FindBlockByHash(Guid chainId, string? lookupHash, NpgsqlConnection connection, NpgsqlTransaction? transaction)
    {
        if (transaction == null || lookupHash == null)
            return null;

        try
        {
            var blockSql = @"
            SELECT id, chain_id, prev_hash, hash, nonce, timestamp
            FROM blocks
            WHERE chain_id=@chain_id AND hash=@hash;
            ";
            using var blockCommand = new NpgsqlCommand(blockSql, connection, transaction);
            blockCommand.Parameters.AddWithValue("hash", lookupHash);
            blockCommand.Parameters.AddWithValue("chain_id", chainId);

            using var reader = blockCommand.ExecuteReader();
            if (reader.Read())
            {
                var id = reader.GetInt32(reader.GetOrdinal("id"));
                var chain_id = reader.GetGuid(reader.GetOrdinal("chain_id"));
                var prev_hash = reader.GetString(reader.GetOrdinal("prev_hash"));
                var hash = reader.GetString(reader.GetOrdinal("hash"));
                var nonce = reader.GetInt32(reader.GetOrdinal("nonce"));
                var timestamp = reader.GetDateTime(reader.GetOrdinal("timestamp"));

                var block = new Block(
                    chain_id,
                    prev_hash,
                    hash,
                    nonce,
                    timestamp,
                    []
                );

                return block;
            }
            else
            {
                _logger.LogWarning($"Block not found: hash={lookupHash}, chain_id={chainId}");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception during block fetch: {ex}");
            return null;
        }
    }

    public bool ListChains(out List<Blockchain> chains)
    {
        chains = new List<Blockchain>();
        try
        {
            using var connection = GetConnection();
            connection.Open();
            using var t = connection.BeginTransaction();

            var lSQL = @"
                SELECT (id, name, reward, difficulty)
                FROM chains;
            ";
            var lCommand = new NpgsqlCommand(lSQL, connection, t);
            var result = lCommand.ExecuteReader();
            while (result.Read())
            {
                Guid id = result.GetGuid(result.GetOrdinal("id"));
                string name = result.GetName(result.GetOrdinal("name"));
                float reward = result.GetFloat(result.GetOrdinal("reward"));
                int diff = result.GetInt32(result.GetOrdinal("difficulty"));

                chains.Add(new Blockchain(id, name, reward, diff));
            }
            t.Commit();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception during blockchain fetching: {ex}");
            return false;
        }
    }

    public bool Verify(Guid chainId)
    {
        try
        {
            using var connection = new NpgsqlConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            int latestId = GetBlockchainLatestBlock(chainId, connection, transaction);
            if (latestId == -1) // just the genesis block
                return true;
            Block? curr = GetBlock(chainId, latestId, connection, transaction);
            if (curr == null)
                return false;

            while (curr?.PreviousHash != "") // identifies the genesis block
            {
                if (curr?.Digest() != curr?.Hash)
                {
                    _logger.LogWarning($"Digest and hash differ");
                    return false;
                }
                Block? next = FindBlockByHash(chainId, curr?.PreviousHash, connection, transaction);
                if (next == null)
                    return false;
                curr = next;
            }
            transaction.Commit();
            return true;
        }
        catch (System.Exception ex)
        {
            _logger.LogError($"Exception during blockchain verification: {ex}");
            return false;
        }
    }

    private static int GetBlockchainLatestBlock(Guid chainId, NpgsqlConnection conn, NpgsqlTransaction t)
    {
        string chainSql = @"
            SELECT (latest_block) FROM chains WHERE id=@id;
        ";
        var chainCmd = new NpgsqlCommand(chainSql, conn, t);
        chainCmd.Parameters.AddWithValue("id", chainId);
        var result = chainCmd.ExecuteScalar();
        if (result == null)
            return -1;
        int id = (int)result;
        return id;
    }

    public bool GatherTaxpayer(Guid chainId, int taxpayerId, out List<Transaction> transactions)
    {
        transactions = new();
        try
        {
            using var conn = new NpgsqlConnection();
            conn.Open();
            using var t = conn.BeginTransaction();
            string taxpayerSql = @"
            SELECT (chain_id, block_id, taxpayer_id, jurisdiction, amount, taxable_base, tax_rate, tax_type, status, notes, period_start, period_end, due_date, payment_date)
            FROM transactions
            WHERE chain_id=@chain_id, taxpayer_id=@taxpayer_id;";
            var payerCmd = new NpgsqlCommand(taxpayerSql, conn, t);
            payerCmd.Parameters.AddWithValue("chain_id", chainId);
            payerCmd.Parameters.AddWithValue("taxpayer_id", taxpayerId);
            var reader = payerCmd.ExecuteReader();
            var result = payerCmd.ExecuteReader();
            if (reader.Read())
            {
                Transaction curr = new();
                curr.Amount = reader.GetDecimal(reader.GetOrdinal("amount"));
                curr.DueDate = reader.GetDateTime(reader.GetOrdinal("due_date"));
                curr.Jurisdiction = reader.GetString(reader.GetOrdinal("jurisdiction"));
                curr.PaymentDate = reader.GetDateTime(reader.GetOrdinal("payment_date"));
                curr.TaxableBase = reader.IsDBNull(reader.GetOrdinal("taxable_base")) ? null : reader.GetDecimal(reader.GetOrdinal("taxable_base"));
                curr.TaxRate = reader.IsDBNull(reader.GetOrdinal("tax_rate")) ? null : reader.GetDecimal(reader.GetOrdinal("tax_rate"));
                curr.TaxPeriodStart = reader.GetDateTime(reader.GetOrdinal("period_start"));
                curr.TaxPeriodEnd = reader.GetDateTime(reader.GetOrdinal("period_end"));
                curr.TaxpayerId = reader.GetString(reader.GetOrdinal("taxpayer_id"));
                curr.Status = (Transaction.TaxStatus)reader.GetInt32(reader.GetOrdinal("status"));
                curr.Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes"));
                curr.Type = (Transaction.TaxType)reader.GetInt32(reader.GetOrdinal("tax_type"));

                transactions.Add(curr);
            }
            return true;

        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception during gathering taxpayer information: {ex}");
            return false;
        }
    }
}