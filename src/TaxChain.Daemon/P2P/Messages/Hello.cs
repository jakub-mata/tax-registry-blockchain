using System;

namespace TaxChain.Daemon.P2P.Messages;

public record Hello(Guid PeerId);

public record HelloAck(Guid PeerId);