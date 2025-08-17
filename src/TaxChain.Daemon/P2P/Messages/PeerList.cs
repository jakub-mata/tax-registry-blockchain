using System;
using System.Collections.Generic;

namespace TaxChain.Daemon.P2P.Messages;
public record PeerListMessage(List<string> Peers);