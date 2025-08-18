using System;
using System.Collections.Generic;
using TaxChain.core;

namespace TaxChain.Daemon.P2P.Messages;

/// <summary>
/// Represents a collection of blocks in the blockchain. It is used to transfer blocks
/// between nodes in the network.
/// </summary>
/// <param name="Blockchain">The blockchain containing blocks</param>
/// <param name="ChainBlocks">Transferred blocks</param>
public record Blocks(Blockchain Blockchain, List<Block> ChainBlocks);
