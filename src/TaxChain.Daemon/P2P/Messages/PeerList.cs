using System;
using System.Collections.Generic;

namespace TaxChain.Daemon.P2P.Messages;

/// <summary>
/// Represents a message containing a list of peers in the P2P network.
/// </summary>
/// <param name="Peers">The list of peers in an IPEndpoint format, e.g. 10.10.10.10:1010</param>
public record PeerListMessage(List<string> Peers);