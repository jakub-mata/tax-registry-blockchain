using System;

namespace TaxChain.Daemon.P2P.Messages;

/// <summary>
/// Represents a request message sent to a peer in the network to initiate communication.
/// </summary>
/// <param name="PeerId">The peer's unique identifier.</param>
public record Hello(Guid PeerId);

/// <summary>
/// Represents a response / acknoledgement sent to a peer in the network to initiate communication.
/// </summary>
/// <param name="PeerId">The peer's unique identifier.</param>
public record HelloAck(Guid PeerId);