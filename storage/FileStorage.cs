using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace tax_registry_blockchain.storage;

public class FileStorage : IStorable
{
    private readonly string _dir;
    public FileStorage()
    {
        Directory.CreateDirectory("./blockchains");
        _dir = "./blockchains";
    }
    public bool Store<T>(Blockchain<T> blockchain) where T : IAddressable, IRewarding
    {
        try
        {
            string blockchainPath = Path.Combine(_dir, $"{blockchain.Name}-{blockchain.ID}.json");
            var serialized = JsonSerializer.Serialize(
                blockchain,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }
            );
            // Store the blockchain
            File.WriteAllText(blockchainPath, serialized);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Store<T>(Block<T> block) where T : IAddressable, IRewarding
    {
        return false;
    }
    public bool Store<T>(T transaction) where T : IAddressable, IRewarding
    {
        return false;
    }
    public Blockchain<T>? Retrieve<T>(string name, Guid id) where T : IAddressable, IRewarding
    {
        return null;
    }

    public List<Tuple<string, Guid>> List()
    {
        string directory = "./blockchains";
        if (!Directory.Exists(directory))
            return [];

        var files = Directory.GetFiles(directory, "*-*.json");
        if (files.Length == 0)
            return [];

        List<Tuple<string, Guid>> list = [];
        foreach(string f in files)
        {
            string fileName = Path.GetFileName(f);
            string[] splitName = fileName.Split('-');
            var blockchainName = splitName[0];
            string guid = splitName[1];
            list.Add(new Tuple<string, Guid>(blockchainName, Guid.Parse(guid)));
        }
        return list;
    }

    public Block<TaxPayload>[] ListLedger(string giud)
    {
        throw new NotImplementedException();
    }
}