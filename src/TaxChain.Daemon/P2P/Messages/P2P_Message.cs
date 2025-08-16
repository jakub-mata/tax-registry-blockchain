using System.Text.Json;

namespace TaxChain.P2P.Messages;

public class P2PMessage
{
    public string Type { get; set; } = "";
    public JsonElement Payload { get; set; }

    public static P2PMessage Create<T>(string type, T payload) =>
        new P2PMessage { Type = type, Payload = JsonSerializer.SerializeToElement(payload) };

    public T Deserialize<T>() =>
        JsonSerializer.Deserialize<T>(Payload.GetRawText())!;
}
