using System;

namespace TaxChain.Daemon.P2P.Messages;

public record ChainInfo(Guid ChainId, int BlockCount);
