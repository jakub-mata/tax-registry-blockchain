using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TaxChain.Daemon.P2P.Messages;

namespace TaxChain.Daemon.P2P;

public class PeerConnection : IEquatable<PeerConnection>, IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;

    public Guid PeerId { get; private set; }

    public PeerConnection(TcpClient client)
    {
        _client = client;
        _stream = _client.GetStream();
    }

    public async Task SendAsync<T>(string type, T payload, CancellationToken ct = default)
    {
        var msg = P2PMessage.Create(type, payload);
        var json = JsonSerializer.Serialize(msg);
        var bytes = Encoding.UTF8.GetBytes(json);
        var length = BitConverter.GetBytes(bytes.Length);

        await _stream.WriteAsync(length, 0, length.Length, ct);
        await _stream.WriteAsync(bytes, 0, bytes.Length, ct);
    }

    public async Task<P2PMessage?> ReceiveAsync(CancellationToken ct = default)
    {
        var lengthBuffer = new byte[4];
        int read = await _stream.ReadAsync(lengthBuffer, ct);
        if (read == 0) return null;

        int length = BitConverter.ToInt32(lengthBuffer);
        var buffer = new byte[length];
        await _stream.ReadExactlyAsync(buffer, ct);

        return JsonSerializer.Deserialize<P2PMessage>(Encoding.UTF8.GetString(buffer));
    }

    public async Task HandshakeAsync(Guid localId, CancellationToken ct = default)
    {
        await SendAsync("Hello", new Hello(localId), ct);
        var msg = await ReceiveAsync(ct);
        if (msg?.Type == "Hello")
        {
            var hello = msg.Deserialize<Hello>();
            PeerId = hello.PeerId;
        }
        else throw new InvalidOperationException("Handshake failed");
    }

    // Methods for IEquatable
    public bool Equals(PeerConnection? other) => other != null && PeerId == other.PeerId;
    public override int GetHashCode() => PeerId.GetHashCode();

    // Methods for IDisposable
    public void Dispose()
    {
        try { _stream.Dispose(); } catch { }
        try { _stream.Dispose(); } catch { }
    }
}
