using System;
using System.Threading;
using System.Threading.Tasks;

namespace TaxChain.Daemon.P2P;

/// <summary>
/// Interface for managing the P2P network.
/// </summary>
public interface INetworkManaging : IDisposable
{
    /// <summary>
    /// Starts the P2P network manager.
    /// </summary>
    /// <param name="port">The listening port.</param>
    /// <param name="discoveryDelay">The time interval between automatic peer discovery.</param>
    /// <param name="ct">Cancellation token in case the network manager is to stopped.</param>
    /// <returns>Task</returns>
    void StartAsync(int port, int discoveryDelay = 30, CancellationToken ct = default);
    /// <summary>
    /// Adds a known peer to the P2P network.
    /// This method is used to manually add a peer to the network.
    /// It is useful for connecting to specific peers that are not discovered automatically.
    /// </summary>
    /// <param name="host">Host name or IP address</param>
    /// <param name="port">Port</param>
    void AddKnownPeer(string host, int port);
    /// <summary>
    /// Counts the number of peer in out network.
    /// </summary>
    /// <returns>The number of peers in the network.</returns>
    int CountPeers();
    /// <summary>
    /// Gets the status of the P2P network manager.
    /// </summary>
    /// <returns>A tuple of (bool, DateTime), bool signifying success and datetime a timestamp.</returns>
    Tuple<bool, DateTime> GetStatus();
    /// <summary>
    /// Synchronizes the blockchain with the specified chain ID.
    /// This method is used to ensure that the local blockchain is up-to-date with the network
    /// </summary>
    /// <param name="chainId">The ID of the blockchain</param>
    /// <param name="ct">Cancelation token in case the sync process is to be stopped.</param>
    /// <returns>Task</returns>
    Task SyncChain(Guid chainId, CancellationToken ct = default);
}