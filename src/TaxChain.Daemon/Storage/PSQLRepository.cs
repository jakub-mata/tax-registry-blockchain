using System;
using System.Runtime.CompilerServices;
using Npgsql;
using TaxChain.core;

namespace TaxChain.Daemon.Storage;

public class PGSQLRepository : IBlockchainRepository
{
    private readonly string _connectionString;
    private readonly string _databaseName;
    private NpgsqlConnection? _connection;
    public PGSQLRepository(string connectionString)
    {
        // Load configuration from environment variables
        var host = Environment.GetEnvironmentVariable("TAXCHAIN_DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("TAXCHAIN_DB_PORT") ?? "5432";
        var username = Environment.GetEnvironmentVariable("TAXCHAIN_DB_USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("TAXCHAIN_DB_PASSWORD") ?? "postgres";
        _databaseName = Environment.GetEnvironmentVariable("TAXCHAIN_DB_NAME") ?? "taxchain";
        var adminDb = Environment.GetEnvironmentVariable("TAXCHAIN_ADMIN_DB") ?? "postgres";

        _connectionString = $"Host={host};Port={port};Username={username};Password={password};Database={_databaseName}";
        EnsureDatabaseExists(adminDb);
        SetupConnection();
        CreateTableIfNotExists();
    }

    private void CreateTableIfNotExists()
    {
        try
        {
            // Create chains table
            var chainsTable = @"
                CREATE TABLE IF NOT EXISTS chains (
                    id UUID PRIMARY KEY,
                    name TEXT NOT NULL,
                    reward INTEGER DEFAULT 0,
                    difficulty INTEGER DEFAULT 1,
                    latest_block INTEGER DEFAULT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE(id)
                );";
            // Create blocks table
            var blocksTable = @"
                CREATE TABLE IF NOT EXISTS blocks (
                    id SERIAL PRIMARY KEY,
                    chain_id UUID REFERENCES chains(id),
                    prev_hash TEXT NOT NULL,
                    hash TEXT NOT NULL,
                    timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE(id, chain_id)
                );";
            // Create transactions table
            var transactionsTable = @"
                CREATE TABLE IF NOT EXISTS transactions (
                    chain_id UUID REFERENCES chains(id),
                    block_id INTEGER REFERENCES blocks(id),
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
                    payment_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );";

            // Create pending_transactions table
            var pendingTable = @"
                CREATE TABLE IF NOT EXISTS pending_transactions (
                    id SERIAL PRIMARY KEY,
                    chain_id UUID REFERENCES chains(id),
                    sender TEXT,
                    receiver TEXT,
                    amount DECIMAL(18,8) NOT NULL,
                    tax_type TEXT NOT NULL,
                    data JSONB,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );";

            using var transaction = _connection?.BeginTransaction();

            ExecuteNonQuery(chainsTable, transaction);
            ExecuteNonQuery(blocksTable, transaction);
            ExecuteNonQuery(transactionsTable, transaction);
            ExecuteNonQuery(pendingTable, transaction);

            transaction?.Commit();

            Console.WriteLine("Database tables created/verified successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating tables: {ex.Message}");
            throw;
        }
    }
    private void ExecuteNonQuery(string sql, NpgsqlTransaction? transaction = null)
    {
        using var cmd = new NpgsqlCommand(sql, _connection);
        if (transaction != null)
            cmd.Transaction = transaction;
        cmd.ExecuteNonQuery();
    }
    private void EnsureDatabaseExists(string adminConnectionString)
    {
        // Search for postgres databases on the local Linux system
        try
        {
            using var connection = new NpgsqlConnection(adminConnectionString);
            connection.Open();

            // Check if database exists
            using var cmd = new NpgsqlCommand(
                "SELECT 1 FROM pg_database WHERE datname = @dbName", connection);
            cmd.Parameters.AddWithValue("dbName", _databaseName);

            var result = cmd.ExecuteScalar();

            if (result == null)
            {
                // Database doesn't exist, create it
                using var createCmd = new NpgsqlCommand(
                    $"CREATE DATABASE \"{_databaseName}\"", connection);
                createCmd.ExecuteNonQuery();
                Console.WriteLine($"Database '{_databaseName}' created successfully.");
            }
            else
            {
                Console.WriteLine($"Database '{_databaseName}' already exists.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error ensuring database exists: {ex.Message}");
            throw;
        }
    }
    private void SetupConnection()
    {
        _connection = new NpgsqlConnection(_connectionString);
        try
        {
            _connection.Open();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to database: {ex.Message}");
            throw ex;
        }
    }
    public bool EnqueueTransaction(Transaction transaction)
    {
        throw new NotImplementedException();
    }

    public bool Fetch(Guid chainId, out Block[] blocks)
    {
        throw new NotImplementedException();
    }

    public bool FetchPending(Guid chainId, out Transaction? transaction)
    {
        throw new NotImplementedException();
    }

    public bool RemoveChain(Guid chainId)
    {
        throw new NotImplementedException();
    }

    public bool RemoveLastBlock(Guid chainId)
    {
        throw new NotImplementedException();
    }

    public bool Store(Blockchain blockchain)
    {
        try
        {
            using var transaction = _connection?.BeginTransaction();

            // Store chain info
            var chainSql = @"
                INSERT INTO chains (id, name, reward, difficulty)
                VALUES (@id, @name, @reward, @difficulty)";

            using var chainCmd = new NpgsqlCommand(chainSql, _connection, transaction);
            chainCmd.Parameters.AddWithValue("id", blockchain.Id.ToString());
            chainCmd.Parameters.AddWithValue("name", blockchain.Name ?? "Unnamed Chain");
            chainCmd.Parameters.AddWithValue("reward", blockchain.RewardAmount);
            chainCmd.Parameters.AddWithValue("difficulty", blockchain.Difficulty);

            chainCmd.ExecuteNonQuery();

            transaction?.Commit();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error storing blockchain: {ex.Message}");
            return false;
        }
    }

    public bool Store(Guid chainId, Block block)
    {
        try
        {
            using var transaction = _connection?.BeginTransaction();
            if (transaction == null)
            {
                Console.WriteLine("Transaction object is null");
                return false;
            }
            // Check if given blockchain exists
            if (!DoesBlockchainExist(chainId, transaction))
            {
                Console.WriteLine($"Provided blockchain does not exist: {chainId.ToString()}");
                return false;
            }

            // Store block
            var blockSql = @"
            INSERT INTO blocks (chain_id, prev_hash, hash)
            VALUES (@chain_id, @prev_hash, @hash)
            RETURNING id";

            using var blockCmd = new NpgsqlCommand(blockSql, _connection, transaction);
            blockCmd.Parameters.AddWithValue("chain_id", chainId);
            blockCmd.Parameters.AddWithValue("prev_hash", block.PreviousHash ?? (object)DBNull.Value);
            blockCmd.Parameters.AddWithValue("hash", block.Hash);

            var blockId = (int?)blockCmd.ExecuteScalar();
            if (blockId == null)
            {
                Console.WriteLine("DB did not return block's id upon insertion");
                return false;
            }

            // Store block transaction
            foreach (Transaction t in block.Payload)
            {
                StoreTransaction(chainId, block.Id, t, transaction);
            }

            // Update latest block in chain table
            bool ok = UpdateLatestBlock(chainId, blockId, transaction);
            if (!ok)
                return false;

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error storing block: {ex.Message}");
            return false;
        }
    }

    private bool UpdateLatestBlock(Guid chain_id, int? blockId, NpgsqlTransaction transaction)
    {
        if (blockId == null)
        {
            Console.WriteLine("Block id is null, cannot update latest block id");
            return false;
        }
        try
        {
            var updateSql = @"
                UPDATE chains
                SET latest_block=@block_id
                WHERE id=@id;
            ";
            var updateCmd = new NpgsqlCommand(updateSql, _connection, transaction);
            updateCmd.Parameters.AddWithValue("id", chain_id);
            updateCmd.Parameters.AddWithValue("block_id", blockId);

            int rows = updateCmd.ExecuteNonQuery();
            if (rows == 0)
            {
                Console.WriteLine("No rows affected when updating latest block id");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to update chain's latest block: {ex}");
            return false;
        }
    }

    private bool DoesBlockchainExist(Guid chainId, NpgsqlTransaction transaction)
    {
        try
        {
            var checkSql = @"
                SELECT EXISTS(SELECT 1 FROM chains WHERE id=@id);
            ";
            var checkCmd = new NpgsqlCommand(checkSql, _connection, transaction);
            checkCmd.Parameters.AddWithValue("id", chainId);
            var result = checkCmd.ExecuteScalar();
            return result is bool exists && exists;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to check blockchain exists: {ex}");
            return false;
        }
    }

    private bool StoreTransaction(Guid chainId, int blockId, Transaction t, NpgsqlTransaction transaction)
    {
        try
        {
            var tSql = @"
            INSERT INTO (chain_id, block_id, taxpayer_id, jurisdiction, amount, taxable_base, tax_rate, tax_type, status, notes, period_start, period_end, due_date, payment_date
            VALUES (@chain_id, @block_id, @taxpayer_id, @jurisdiction, @amount, @taxable_base, @tax_rate, @tax_type, @status, @notes, @period_start, @period_end, @due_date, @payment_date)";
            using var tCommand = new NpgsqlCommand(tSql, _connection, transaction);
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
            Console.WriteLine($"Failed to add transaction: {ex}");
            return false;
        }
    }

    public bool Tail(Guid chainId, int n, out Block[] blocks)
    {
        throw new NotImplementedException();
    }
}