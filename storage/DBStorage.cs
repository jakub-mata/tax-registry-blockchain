using System;
using System.Collections.Generic;

namespace tax_registry_blockchain.storage;

public class DBStorage : IStorable
{
    public DBStorage()
    {
    }
    public bool Store<T>(Blockchain<T> blockchain) where T : IAddressable, IRewarding
    {
        return false;
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
        throw new NotImplementedException();
    }

    public Block<TaxPayload>[] ListLedger(string giud)
    {
        throw new NotImplementedException();
    }
}