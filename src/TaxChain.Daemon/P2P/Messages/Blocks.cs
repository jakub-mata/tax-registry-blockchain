using System;
using System.Collections.Generic;
using TaxChain.core;

namespace TaxChain.P2P.Messages;

public record Blocks(Blockchain Blockchain, List<Block> ChainBlocks);
