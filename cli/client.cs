using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using Spectre.Console.Cli;
using tax_registry_blockchain.storage;

namespace tax_registry_blockchain.cli;

class CLIClient
{
    public static class StorageManager
    {
        private static FileStorage? _fileStorage;
        private static DBStorage? _dbStorage;
        public static FileStorage FileStore
        {
            get
            {
                _fileStorage ??= new FileStorage();
                return _fileStorage;
            }
        }
        public static DBStorage DBStore
        {
            get
            {
                _dbStorage ??= new DBStorage();
                return _dbStorage;
            }
        }
        public static Dictionary<Guid, IStorable> blockchainToStorageMap = [];
    }
    public static void Run(string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.AddCommand<commands.CreateCommand>("--create")
              .WithDescription("Creates a new blockchain");
        });
        app.Run(args);
    }
}