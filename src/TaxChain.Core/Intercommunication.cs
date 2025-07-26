namespace TaxChain.core;

using System.Collections.Generic;
public class ControlRequest
{
    public string Command { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
}

public class ControlResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}

public class TaxpayerInformation
{
    public string? TaxpayerId { get; set; }
    public float Balance { get; set; }
    public Transaction[]? Transactions {get; set;}
}