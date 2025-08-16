using System;

namespace TaxChain.P2P.Messages;

public record ChainInfo(Guid ChainId, int BlockCount);
