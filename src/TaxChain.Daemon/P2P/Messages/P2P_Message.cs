using System.Text.Json;

namespace TaxChain.Daemon.P2P.Messages;

/// <summary>
/// Represents a message in the P2P network.
/// It contains a type to identify the message and a payload that can be any JSON-serializable object.
/// The payload is stored as a JSON element for flexibility in handling different message types.
/// </summary>
public class P2PMessage
{
    public string Type { get; set; } = "";
    public JsonElement Payload { get; set; }

    public static P2PMessage Create<T>(string type, T payload) =>
        new P2PMessage { Type = type, Payload = JsonSerializer.SerializeToElement(payload) };

    public T Deserialize<T>() =>
        JsonSerializer.Deserialize<T>(Payload.GetRawText())!;
}
