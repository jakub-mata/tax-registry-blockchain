using System;
using System.Collections.Generic;

namespace tax_registry_blockchain.storage;

public interface IStorable
{
    public bool Store<T>(Blockchain<T> blockchain) where T : IAddressable, IRewarding;
    public bool Store<T>(Block<T> block) where T : IAddressable, IRewarding;
    public bool Store<T>(T transaction) where T : IAddressable, IRewarding;
    public Blockchain<T>? Retrieve<T>(string name, Guid id) where T : IAddressable, IRewarding;
    public List<Tuple<string, Guid>> List();
    public Block<TaxPayload>[] ListLedger(string giud);
}