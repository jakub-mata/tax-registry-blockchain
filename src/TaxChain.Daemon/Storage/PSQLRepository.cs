using System;
using System.Collections.Generic;
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

        _connectionString = $"Host={host};Port={port};Username={username};Password={password};Database={_databaseName};Include Error Detail=true";
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
                latest_block INTEGER,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );",

            @"CREATE TABLE IF NOT EXISTS blocks (
                id SERIAL PRIMARY KEY,
                chain_id UUID REFERENCES chains(id) ON DELETE CASCADE,
                prev_hash TEXT NOT NULL,
                hash TEXT NOT NULL,
                nonce BIGINT NOT NULL,
                timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(id, chain_id)
            );",

            @"CREATE TABLE IF NOT EXISTS transactions (
                id UUID PRIMARY KEY,
                chain_id UUID REFERENCES chains(id) ON DELETE CASCADE,
                block_id INTEGER REFERENCES blocks(id) ON DELETE CASCADE,
                taxpayer_id TEXT NOT NULL,
                amount DECIMAL(18,8) NOT NULL
            );",

            @"CREATE TABLE IF NOT EXISTS pending_transactions (
                id UUID PRIMARY KEY,
                chain_id UUID REFERENCES chains(id) ON DELETE CASCADE,
                amount DECIMAL(18,8) NOT NULL,
                taxpayer_id TEXT NOT NULL,
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

            _logger.LogDebug("Checking whether the DB exists...");
            using var cmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @dbName", connection);
            cmd.Parameters.AddWithValue("dbName", _databaseName);

            var result = cmd.ExecuteScalar();
            int? parsed = (result == null) ? null : (int)result;
            if (parsed == null || parsed == 0)
            {
                _logger.LogInformation("Creating the database anew");
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
            using var connection = GetConnection();
            connection.Open();
            using var t = connection.BeginTransaction();

            bool ok = StorePendingTransaction(chainId, transaction, connection, t);
            if (!ok)
            {
                _logger.LogError("to store pending transaction");
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

    public bool FetchPending(Guid chainId, out Transaction? transaction)
    {
        transaction = null;
        try
        {
            using var connection = GetConnection();
            connection.Open();
            var fetchSQL = @"
                SELECT id, chain_id, created_at
                FROM pending_transactions
                WHERE chain_id=@chain_id;
            ";
            using var fetchCmd = new NpgsqlCommand(fetchSQL, connection);
            fetchCmd.Parameters.AddWithValue("chain_id", chainId);
            FetchedT last;
            using (var reader = fetchCmd.ExecuteReader())
            {
                var results = new List<FetchedT>();
                // Sort results based on created_at field
                while (reader.Read())
                {
                    Guid id = reader.GetGuid(reader.GetOrdinal("id"));
                    DateTime createdAt = reader.GetDateTime(reader.GetOrdinal("created_at"));
                    results.Add(new FetchedT { Id = id, CreatedAt = createdAt });
                }

                if (results.Count == 0)
                {
                    transaction = null;
                    return true;
                }
                results.Sort(new Comparer());
                last = results[^1];
            }
            // Fetch last
            var lastSQL = @"
                SELECT id, chain_id, taxpayer_id, amount
                FROM pending_transactions
                WHERE id=@id AND chain_id=@chainId;
            ";
            using var lastCmd = new NpgsqlCommand(lastSQL, connection);
            lastCmd.Parameters.AddWithValue("id", last.Id);
            lastCmd.Parameters.AddWithValue("chainId", chainId);
            using var lastReader = lastCmd.ExecuteReader();
            if (lastReader.Read())
            {
                Transaction trans = new Transaction();
                trans.ID = lastReader.GetGuid(lastReader.GetOrdinal("id"));
                trans.Amount = lastReader.GetDecimal(lastReader.GetOrdinal("amount"));
                trans.TaxpayerId = lastReader.GetString(lastReader.GetOrdinal("taxpayer_id"));
                transaction = trans;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to fetch any pending transaction: {ex}", ex);
            return false;
        }
    }

    private readonly struct FetchedT { public Guid Id { get; init; } public DateTime CreatedAt { get; init; } };

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

            var delSql = @"
                DELETE FROM chains
                WHERE id=@id;
            ";
            using var delCmd = new NpgsqlCommand(delSql, connection);
            delCmd.Parameters.AddWithValue("id", chainId);
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

            int id = SetUpGenesisBlock(blockchain.Id, connection, transaction);
            if (id == -1)
            {
                _logger.LogWarning("Chain created, failed to set up genesis block");
                return false;
            }
            UpdateLatestBlock(blockchain.Id, id, connection, transaction);

            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error storing blockchain: {ex.Message}");
            return false;
        }
    }

    private int SetUpGenesisBlock(Guid chainId, NpgsqlConnection conn, NpgsqlTransaction t)
    {
        var genSql = @"
            INSERT INTO blocks (chain_id, prev_hash, hash, nonce)
            VALUES (@chain_id, @prev_hash, @hash, @nonce)
            RETURNING id
        ";
        var genCmd = new NpgsqlCommand(genSql, conn, t);
        genCmd.Parameters.AddWithValue("chain_id", chainId);
        genCmd.Parameters.AddWithValue("prev_hash", "");
        genCmd.Parameters.AddWithValue("hash", "");
        genCmd.Parameters.AddWithValue("nonce", 0L);
        var result = genCmd.ExecuteScalar();
        int? id = result is DBNull ? null : (int?)result;
        return id ?? -1;
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

    private bool StoreTransaction(Guid chainId, int? blockId, Transaction t, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        if (blockId == null)
        {
            return false;
        }
        try
        {
            var tSql = @$"
            INSERT INTO transactions (id, chain_id, block_id, taxpayer_id, amount)
            VALUES (@id, @chain_id, @block_id, @taxpayer_id, @amount)";
            using var tCommand = new NpgsqlCommand(tSql, connection, transaction);
            tCommand.Parameters.AddWithValue("chain_id", chainId);
            tCommand.Parameters.AddWithValue("block_id", blockId);
            tCommand.Parameters.AddWithValue("id", t.ID);
            tCommand.Parameters.AddWithValue("taxpayer_id", t.TaxpayerId);
            tCommand.Parameters.AddWithValue("amount", t.Amount);

            tCommand.ExecuteNonQuery();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to add transaction: {ex}");
            return false;
        }
    }

    private bool StorePendingTransaction(Guid chainId, Transaction t, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        try
        {
            var tSql = @$"
            INSERT INTO pending_transactions (id, chain_id, amount, taxpayer_id)
            VALUES (@id, @chain_id, @amount, @taxpayer_id)";
            using var tCommand = new NpgsqlCommand(tSql, connection, transaction);
            tCommand.Parameters.AddWithValue("id", t.ID);
            tCommand.Parameters.AddWithValue("chain_id", chainId);
            tCommand.Parameters.AddWithValue("taxpayer_id", t.TaxpayerId);
            tCommand.Parameters.AddWithValue("amount", t.Amount);

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
            // Fetch latest block
            var latestBlockSql = @"
                SELECT latest_block
                FROM chains
                WHERE id=@id;
            ";
            var latestBlockCmd = new NpgsqlCommand(latestBlockSql, connection);
            latestBlockCmd.Parameters.AddWithValue("id", chainId);
            var result = latestBlockCmd.ExecuteScalar();
            int? blockId = (result is DBNull)
                ? null
                : (int?)result;
            if (blockId == null)
            {
                _logger.LogWarning("Failed to convert latest block id to int");
                return false;
            }

            // Fetch block
            Block? tailBlock = GetBlock(chainId, blockId, connection);
            if (tailBlock == null)
                return false;
            blocks[0] = tailBlock;
            for (int i = 1; i < n; ++i)
            {
                Block? next = FindBlockByHash(chainId, blocks[i - 1].Hash, connection);
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
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception when fetching tail: {ex}");
            return false;
        }
    }

    public Block? GetBlock(Guid chainId, int? blockId, NpgsqlConnection connection)
    {
        if (blockId == null)
            return null;
        try
        {
            var blockSql = @"
            SELECT id, chain_id, prev_hash, hash, nonce, timestamp
            FROM blocks
            WHERE id=@id AND chain_id=@chain_id;
            ";
            using var blockCommand = new NpgsqlCommand(blockSql, connection);
            blockCommand.Parameters.AddWithValue("id", blockId);
            blockCommand.Parameters.AddWithValue("chain_id", chainId);

            using var reader = blockCommand.ExecuteReader();
            if (reader.Read())
            {
                var id = reader.GetInt32(reader.GetOrdinal("id"));
                var chain_id = reader.GetGuid(reader.GetOrdinal("chain_id"));
                var prev_hash = reader.GetString(reader.GetOrdinal("prev_hash"));
                var hash = reader.GetString(reader.GetOrdinal("hash"));
                var nonce = reader.GetInt64(reader.GetOrdinal("nonce"));
                var timestamp = reader.GetDateTime(reader.GetOrdinal("timestamp"));

                var block = new Block(
                    chain_id,
                    prev_hash,
                    hash,
                    nonce,
                    timestamp,
                    new()
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

    private Block? FindBlockByHash(Guid chainId, string? lookupHash, NpgsqlConnection connection)
    {
        if (lookupHash == null)
            return null;

        try
        {
            var blockSql = @"
            SELECT id, chain_id, prev_hash, hash, nonce, timestamp
            FROM blocks
            WHERE chain_id=@chain_id AND hash=@hash;
            ";
            using var blockCommand = new NpgsqlCommand(blockSql, connection);
            blockCommand.Parameters.AddWithValue("hash", lookupHash);
            blockCommand.Parameters.AddWithValue("chain_id", chainId);

            using var reader = blockCommand.ExecuteReader();
            if (reader.Read())
            {
                var id = reader.GetInt32(reader.GetOrdinal("id"));
                var chain_id = reader.GetGuid(reader.GetOrdinal("chain_id"));
                var prev_hash = reader.GetString(reader.GetOrdinal("prev_hash"));
                var hash = reader.GetString(reader.GetOrdinal("hash"));
                var nonce = reader.GetInt64(reader.GetOrdinal("nonce"));
                var timestamp = reader.GetDateTime(reader.GetOrdinal("timestamp"));

                var block = new Block(
                    chain_id,
                    prev_hash,
                    hash,
                    nonce,
                    timestamp,
                    new()
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

            var lSQL = @"
                SELECT id, name, reward, difficulty
                FROM chains;
            ";
            var lCommand = new NpgsqlCommand(lSQL, connection);
            using var result = lCommand.ExecuteReader();
            while (result.Read())
            {
                Guid id = result.GetGuid(result.GetOrdinal("id"));
                string name = result.GetString(result.GetOrdinal("name"));
                float reward = result.GetFloat(result.GetOrdinal("reward"));
                int diff = result.GetInt32(result.GetOrdinal("difficulty"));

                chains.Add(new Blockchain(id, name, reward, diff));
            }
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
            using var connection = GetConnection();
            connection.Open();
            int latestId = GetBlockchainLatestBlock(chainId, connection);
            if (latestId == -1) // just the genesis block
                return true;
            Block? curr = GetBlock(chainId, latestId, connection);
            if (curr == null)
                return false;

            while (curr?.PreviousHash != "") // identifies the genesis block
            {
                if (curr?.Digest() != curr?.Hash)
                {
                    _logger.LogWarning($"Digest and hash differ");
                    return false;
                }
                Block? next = FindBlockByHash(chainId, curr?.PreviousHash, connection);
                if (next == null)
                    return false;
                curr = next;
            }
            return true;
        }
        catch (System.Exception ex)
        {
            _logger.LogError($"Exception during blockchain verification: {ex}");
            return false;
        }
    }

    private static int GetBlockchainLatestBlock(Guid chainId, NpgsqlConnection conn)
    {
        string chainSql = @"
            SELECT (latest_block) FROM chains WHERE id=@id;
        ";
        var chainCmd = new NpgsqlCommand(chainSql, conn);
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
            SELECT (id, chain_id, block_id, taxpayer_id, jurisdiction, amount, taxable_base, tax_rate, tax_type, status, notes, period_start, period_end, due_date, payment_date)
            FROM transactions
            WHERE chain_id=@chain_id AND taxpayer_id=@taxpayer_id;";
            var payerCmd = new NpgsqlCommand(taxpayerSql, conn, t);
            payerCmd.Parameters.AddWithValue("chain_id", chainId);
            payerCmd.Parameters.AddWithValue("taxpayer_id", taxpayerId);
            using (var reader = payerCmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    Transaction curr = new();
                    curr.ID = reader.GetGuid(reader.GetOrdinal("id"));
                    curr.Amount = reader.GetDecimal(reader.GetOrdinal("amount"));
                    curr.TaxpayerId = reader.GetString(reader.GetOrdinal("taxpayer_id"));
                    transactions.Add(curr);
                }
            }
            return true;

        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception during gathering taxpayer information: {ex}");
            return false;
        }
    }

    public AppendResult AppendBlock(Block block)
    {
        try
        {
            using var conn = GetConnection();
            conn.Open();
            using var t = conn.BeginTransaction();

            var chainSql = @"
                SELECT id, difficulty, latest_block
                FROM chains
                WHERE id=@id;
            ";
            var chainCmd = new NpgsqlCommand(chainSql, conn);
            chainCmd.Parameters.AddWithValue("id", block.ChainId);
            int latest_block;
            using (var reader = chainCmd.ExecuteReader())
            {
                if (!reader.Read())
                {
                    _logger.LogWarning("Failed to found blockchain; cannot append a new block");
                    return AppendResult.BlockchainUndefined;
                }
                int difficulty = reader.GetInt32(reader.GetOrdinal("difficulty"));
                latest_block = reader.GetInt32(reader.GetOrdinal("latest_block"));
                if (!IsBlockValid(block, difficulty))
                {
                    _logger.LogWarning("Block to append is not valid");
                    return AppendResult.DigestMismatch;
                }
            };
            if (!DoesPrevHashMatch(block, latest_block, conn))
            {
                _logger.LogWarning("Prev hash does not match hash of latest block");
                return AppendResult.PrevHashMismatch;
            }

            if (IsTransactionDuplicate(block.Payload.ID, conn))
            {
                _logger.LogWarning("Transaction is already stored, stop inserting to avoid duplicates");
                return AppendResult.AlreadyIn;
            }

            // Remove from pending
            bool ok = RemovePending(block.Payload.ID, conn);
            if (!ok)
            {
                _logger.LogError("Failed to remove transaction from pending...");
                return AppendResult.DBFail;
            }
            // Store block
            int? blockId = StoreBlock(block, conn, t);
            if (!ok)
            {
                _logger.LogError("Failed to store transaction");
                return AppendResult.DBFail;
            }
            // Store transaction
            ok = StoreTransaction(block.ChainId, blockId, block.Payload, conn, t);
            if (!ok)
            {
                _logger.LogError("Failed to store new block");
                return AppendResult.DBFail;
            }
            // Update blockchain info
            UpdateLatestBlock(block.ChainId, blockId, conn, t);
            t.Commit();
            return AppendResult.Success;
        }
        catch (Exception e)
        {
            _logger.LogError($"Exception during appending block information: {e}");
            return AppendResult.Exception; 
        }
    }

    private bool RemovePending(Guid pendingId, NpgsqlConnection conn)
    {
        var removeSql = @"
            DELETE from pending_transactions
            WHERE id=@id;
        ";
        using var removeCmd = new NpgsqlCommand(removeSql, conn);
        removeCmd.Parameters.AddWithValue("id", pendingId);
        return removeCmd.ExecuteNonQuery() > 0;
    }

    private bool IsBlockValid(Block b, int difficulty)
    {
        for (int i = 0; i < difficulty; ++i)
        {
            if (b.Hash[i] != '0')
                return false;
        }
        return b.Digest() == b.Hash;
    }

    private bool DoesPrevHashMatch(Block b, int latestBlock, NpgsqlConnection conn)
    {
        try
        {
            Block? last = GetBlock(b.ChainId, latestBlock, conn);
            if (last == null)
                return false;
            return last.Hash == b.PreviousHash;
        }
        catch (Exception e)
        {
            _logger.LogError("Exception during prev hash comparison: {e}", e);
            return false;
        }
    }

    private bool IsTransactionDuplicate(Guid transactionId, NpgsqlConnection conn)
    {
        try
        {
            var existSQL = "SELECT EXISTS(SELECT 1 FROM transactions WHERE id=@id);";
            var existCmd = new NpgsqlCommand(existSQL, conn);
            existCmd.Parameters.AddWithValue("id", transactionId);
            object? result = existCmd.ExecuteScalar();
            return result is bool exists && exists;
        }
        catch (Exception e)
        {
            _logger.LogError("Exception during checking for transaction uniqueness: {e}", e);
            return false;
        }
    }

    private int? StoreBlock(Block b, NpgsqlConnection conn, NpgsqlTransaction t)
    {
        var genSql = @"
            INSERT INTO blocks (chain_id, prev_hash, hash, nonce)
            VALUES (@chain_id, @prev_hash, @hash, @nonce)
            RETURNING id;
        ";
        var genCmd = new NpgsqlCommand(genSql, conn, t);
        genCmd.Parameters.AddWithValue("chain_id", b.ChainId);
        genCmd.Parameters.AddWithValue("prev_hash", b.PreviousHash);
        genCmd.Parameters.AddWithValue("hash", b.Hash);
        genCmd.Parameters.AddWithValue("nonce", b.Nonce);
        var result = genCmd.ExecuteScalar();
        return (result is DBNull) ? null : (int?)result;
    }

    public bool GetBlockchain(Guid chainId, out Blockchain? b)
    {
        b = null;
        try
        {
            using var connection = GetConnection();
            connection.Open();
            string chainSQL = @"
                SELECT id, name, reward, difficulty
                FROM chains
                WHERE id=@id
            ";
            var chainCmd = new NpgsqlCommand(chainSQL, connection);
            chainCmd.Parameters.AddWithValue("id", chainId);
            using var reader = chainCmd.ExecuteReader();
            if (reader.Read())
            {
                b = new Blockchain(
                    reader.GetGuid(reader.GetOrdinal("id")),
                    reader.GetString(reader.GetOrdinal("name")),
                    reader.GetFloat(reader.GetOrdinal("reward")),
                    reader.GetInt32(reader.GetOrdinal("difficulty"))
                );
            }
            else
            {
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to retrieve blockchain from PGSQL database: {ex}", ex);
            return false;
        }
    }
}