using System.IO.Pipes;
using System.Text.Json;
using Spectre.Console;
using System.Threading.Tasks;
using System;
using System.IO;
using TaxChain.core;
using System.Collections.Generic;
using System.Diagnostics;

namespace TaxChain.CLI.Services
{
    public class DaemonClient
    {
        private const string PipeName = "TaxChainControlPipe";

        public DaemonClient() { }

        public async Task<bool> IsDaemonRunningAsync()
        {
            try
            {
                using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
                await pipeClient.ConnectAsync(1000); // 1 second timeout

                var request = new ControlRequest { Command = "status" };
                var response = await SendRequestAsync(pipeClient, request);
                return response.Success;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ControlResponse> SendCommandAsync(string command, Dictionary<string, object>? parameters = null)
        {
            try
            {
                using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
                await pipeClient.ConnectAsync(5000);

                var request = new ControlRequest
                {
                    Command = command,
                    Parameters = parameters
                };

                return await SendRequestAsync(pipeClient, request);
            }
            catch (Exception ex)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = $"Failed to connect to daemon: {ex.Message}"
                };
            }
        }

        private async Task<ControlResponse> SendRequestAsync(NamedPipeClientStream pipe, ControlRequest request)
        {
            using var reader = new StreamReader(pipe);
            using var writer = new StreamWriter(pipe) { AutoFlush = true };

            var requestJson = JsonSerializer.Serialize(request);
            await writer.WriteLineAsync(requestJson);

            var responseJson = await reader.ReadLineAsync();
            if (responseJson != null)
            {
                return JsonSerializer.Deserialize<ControlResponse>(responseJson) ??
                       new ControlResponse { Success = false, Message = "Invalid response" };
            }

            return new ControlResponse { Success = false, Message = "No response received" };
        }

        public async Task<bool> StartDaemonAsync()
        {
            try
            {
                // Check if already running
                if (await IsDaemonRunningAsync())
                {
                    AnsiConsole.MarkupLine("[yellow]Daemon is already running[/]");
                    return true;
                }

                // Start the daemon process
                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run --project ../TaxChain.Daemon/TaxChain.Daemon.csproj",
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);

                // Wait for daemon to start
                for (int i = 0; i < 30; i++) // Wait up to 30 seconds
                {
                    await Task.Delay(1000);
                    if (await IsDaemonRunningAsync())
                    {
                        AnsiConsole.MarkupLine("[green]Daemon started successfully[/]");
                        return true;
                    }
                }
                AnsiConsole.MarkupLine("[red]Failed to start daemon[/]");
                return false;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error starting daemon: {ex.Message}[/]");
                return false;
            }
        }

        public async Task<bool> StopDaemonAsync()
        {
            try
            {
                if (!await IsDaemonRunningAsync())
                {
                    AnsiConsole.MarkupLine("[yellow]Daemon is not running, nothing to stop.[/]");
                    return true;
                }

                var response = await SendCommandAsync("stop");
                if (response == null || !response.Success)
                {
                    AnsiConsole.MarkupLine("[red]Failed to stop the daemon.[/]");
                    return true;
                }
                AnsiConsole.MarkupLine("[green]Successfully stopped the daemon.[/]");
                return false;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error starting daemon: {ex.Message}[/]");
                return false;
            }
        }
    }
}