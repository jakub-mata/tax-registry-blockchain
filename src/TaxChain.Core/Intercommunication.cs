using System;

namespace TaxChain.core;

using System.Collections.Generic;

/// <summary>
/// Represents a request to control the daemon.
/// It includes a command to be executed and optional parameters.
/// </summary>
public class ControlRequest
{
    /// <summary>
    /// A command to be run by the daemon, e.g "add", "fetch",...
    /// </summary>
    public string Command { get; set; } = string.Empty;
    /// <summary>
    /// Optional parameters needed for certain commands, e.g. ID of 
    /// a blockchain, verbose flag,...
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Represents a response from the daemon after executing a control command.
/// It indicates whether the command was successful, includes a message for additional context,
/// and can contain additional data.
/// </summary>
public class ControlResponse
{
    /// <summary>
    /// Signifies daemon's success of the requested operation
    /// </summary>
    public bool Success { get; set; }
    /// <summary>
    /// Daemon's message to the client. Important when success is set to false
    /// </summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>
    /// Optional requested data
    /// </summary>
    public object? Data { get; set; }
}

/// <summary>
/// Represents the status information of the daemon.
/// It includes the current status, process ID, uptime, and other relevant details.
/// This information is useful for monitoring the daemon's health and performance.
/// </summary>
public class StatusInformation
{
    public string? Status { get; set; }
    public int ProcessId { get; set; }
    public TimeSpan? Uptime { get; set; }
    public DateTime TimeStamp { get; set; }
    public bool Mining { get; set; }
    public int Port { get; set; }
    public bool SyncSuccess { get; set; }
    public DateTime SyncLast { get; set; }
    public int ConnectedPeers { get; set; }
}