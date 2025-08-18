using System;

namespace TaxChain.Daemon.P2P.Messages;

/// <summary>
/// Represents information about a blockchain, including its ID and the number of blocks it contains.
/// This is used to provide a summary of the blockchain's state.
/// </summary>
public record ChainInfo(Guid ChainId, int BlockCount);
