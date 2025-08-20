using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TaxChain.Daemon.P2P.Messages;

namespace TaxChain.Daemon.P2P;

/// <summary>
/// Represents a connection to a peer in the P2P network.
/// It handles sending and receiving messages, as well as performing handshakes.
/// The connection is established over a TCP client and uses a network stream for communication.
/// </summary>
public class PeerConnection : IEquatable<PeerConnection>, IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;

    /// <summary>
    /// The remote peer's globally unique identifier.
    /// </summary>
    public Guid PeerId { get; private set; }

    public PeerConnection(TcpClient client)
    {
        _client = client;
        _stream = _client.GetStream();
    }

    /// <summary>
    /// The remote network endpoint (may be null if not connected).
    /// </summary>
    public IPEndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint as IPEndPoint;

    public async Task SendAsync<T>(string type, T payload, CancellationToken ct = default)
    {
        var msg = P2PMessage.Create(type, payload);
        var json = JsonSerializer.Serialize(msg);
        var bytes = Encoding.UTF8.GetBytes(json);
        var length = BitConverter.GetBytes(bytes.Length);

        try
        {
            await _stream.WriteAsync(length, 0, length.Length, ct);
            await _stream.WriteAsync(bytes, 0, bytes.Length, ct);
        }
        catch (IOException) { }
    }

    public async Task<P2PMessage?> ReceiveAsync(CancellationToken ct = default)
    {
        try
        {
            var lengthBuffer = new byte[4];
            int read = await _stream.ReadAsync(lengthBuffer, ct);
            if (read == 0) return null;  // connection closed
            int length = BitConverter.ToInt32(lengthBuffer);
            var buffer = new byte[length];
            await _stream.ReadExactlyAsync(buffer, ct);

            return JsonSerializer.Deserialize<P2PMessage>(Encoding.UTF8.GetString(buffer));
        }
        catch (OperationCanceledException)
        {
            return new P2PMessage();
        }
    }

    /// <summary>
    /// Perform handshake as the client (initiator). 
    /// Sends Hello(localId) and expects HelloAck(remoteId).
    /// </summary>
    public async Task PerformClientHandshake(Guid localId, CancellationToken ct)
    {
        await SendAsync("Hello", new Hello(localId), ct);

        var msg = await ReceiveAsync(ct);
        if (msg?.Type != "HelloAck")
            throw new InvalidOperationException("Handshake failed: expected HelloAck");

        var ack = msg.Deserialize<HelloAck>();
        if (ack.PeerId == Guid.Empty)
            throw new InvalidOperationException("Handshake failed: empty remote peer id");

        PeerId = ack.PeerId;
    }

    /// <summary>
    /// Perform handshake as the server (receiver).
    /// Expects Hello(remoteId) and responds with HelloAck(localId).
    /// </summary>
    public async Task PerformServerHandshake(Guid localId, CancellationToken ct)
    {
        var msg = await ReceiveAsync(ct);
        if (msg?.Type != "Hello")
            throw new InvalidOperationException("Handshake failed: expected Hello");

        var hello = msg.Deserialize<Hello>();
        if (hello.PeerId == Guid.Empty)
            throw new InvalidOperationException("Handshake failed: empty remote peer id");

        PeerId = hello.PeerId;
        await SendAsync("HelloAck", new HelloAck(localId), ct);
    }

    // Methods for IEquatable
    public bool Equals(PeerConnection? other) => other is not null && PeerId != Guid.Empty && PeerId == other.PeerId;
    public override bool Equals(object? obj) => Equals(obj as PeerConnection);
    public override int GetHashCode() => PeerId.GetHashCode();

    // Methods for IDisposable
    public void Dispose()
    {
        try { _stream.Dispose(); } catch { }
        try { _client.Dispose(); } catch { }
    }
}
