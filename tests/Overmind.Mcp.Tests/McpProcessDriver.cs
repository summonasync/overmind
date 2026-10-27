using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;

namespace Overmind.Mcp.Tests;

public sealed class McpProcessDriver : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    // Captured at type load: tests that move Environment.CurrentDirectory must not leak the
    // move into the adapter processes spawned by parallel test collections.
    private static readonly string FallbackWorkingDirectory = Environment.CurrentDirectory;

    private readonly Process _process;
    private readonly StreamWriter _stdin;
    private readonly ConcurrentQueue<string> _stdoutLines = new();
    private readonly StringBuilder _stderr = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonNode>> _pending = new();
    private readonly Task _stdoutPump;
    private readonly Task _stderrPump;
    private int _nextId;
    private bool _disposed;

    public IReadOnlyCollection<string> StdoutLines => _stdoutLines.ToArray();

    public string Stderr => _stderr.ToString();

    public bool HasExited => _process.HasExited;

    public Task Completion => Task.WhenAll(_stdoutPump, _stderrPump);

    private McpProcessDriver(Process process, StreamWriter stdin)
    {
        _process = process;
        _stdin = stdin;
        _stdoutPump = Task.Run(() => PumpStdoutAsync(process));
        _stderrPump = Task.Run(() => PumpStderrAsync(process));
    }

    public static McpProcessDriver Start(string? apiUrl, IReadOnlyDictionary<string, string>? environment = null)
    {
        var dllPath = typeof(Overmind.Mcp.OvermindApiClient).Assembly.Location;

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = FallbackWorkingDirectory
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(dllPath);

        if (apiUrl is not null)
        {
            startInfo.EnvironmentVariables["OVERMIND_API_URL"] = apiUrl;
        }

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }

        var process = Process.Start(startInfo)!;
        return new McpProcessDriver(process, process.StandardInput);
    }

    public async Task<JsonNode> CallAsync(string method, JsonNode? parameters, TimeSpan? timeout = null)
    {
        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method
        };
        if (parameters is not null)
        {
            request["params"] = parameters;
        }

        lock (_stdin)
        {
            var line = request.ToJsonString(JsonOptions);
            _stdin.WriteLine(line);
            _stdin.Flush();
        }

        var response = await tcs.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(20));
        return response;
    }

    public Task SendNotificationAsync(string method, JsonNode? parameters = null)
    {
        var notification = new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method };
        if (parameters is not null)
        {
            notification["params"] = parameters;
        }

        lock (_stdin)
        {
            _stdin.WriteLine(notification.ToJsonString(JsonOptions));
            _stdin.Flush();
        }

        return Task.CompletedTask;
    }

    public async Task<JsonNode> InitializeAsync()
    {
        var result = await CallAsync("initialize", new JsonObject
        {
            ["protocolVersion"] = "2024-11-05",
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject
            {
                ["name"] = "overmind-contract-tests",
                ["version"] = "1.0.0"
            }
        });

        await SendNotificationAsync("notifications/initialized");
        return result;
    }

    public Task<JsonNode> ListToolsAsync() => CallAsync("tools/list", new JsonObject());

    public Task<JsonNode> ListResourcesAsync() => CallAsync("resources/list", new JsonObject());

    public Task<JsonNode> ListPromptsAsync() => CallAsync("prompts/list", new JsonObject());

    public Task<JsonNode> CallToolAsync(string name, JsonObject arguments) =>
        CallAsync("tools/call", new JsonObject
        {
            ["name"] = name,
            ["arguments"] = arguments
        });

    public Task<JsonNode> ReadResourceAsync(string uri) =>
        CallAsync("resources/read", new JsonObject
        {
            ["uri"] = uri
        });

    public Task<JsonNode> GetPromptAsync(string name) =>
        CallAsync("prompts/get", new JsonObject
        {
            ["name"] = name
        });

    public async Task<int> WaitForExitAsync(TimeSpan timeout)
    {
        await _process.WaitForExitAsync().WaitAsync(timeout);
        return _process.ExitCode;
    }

    public void AssertStdoutPurity()
    {
        foreach (var line in _stdoutLines)
        {
            var node = JsonNode.Parse(line);
            node.ShouldNotBeNull($"stdout line is not valid JSON: '{line}'");
            node!["jsonrpc"]?.GetValue<string>().ShouldBe("2.0", $"stdout line is not JSON-RPC: '{line}'");
        }
    }

    private async Task PumpStdoutAsync(Process process)
    {
        try
        {
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                _stdoutLines.Enqueue(line);

                JsonNode? node;
                try
                {
                    node = JsonNode.Parse(line);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (node?["id"] is JsonValue value && value.TryGetValue<long>(out var id) &&
                    _pending.TryRemove(id, out var tcs))
                {
                    tcs.TrySetResult(node);
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private async Task PumpStderrAsync(Process process)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync() is { } line)
            {
                lock (_stderr)
                {
                    _stderr.AppendLine(line);
                }
            }
        }
        catch (Exception)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
        }

        try
        {
            _process.WaitForExit(5000);
        }
        catch (Exception)
        {
        }

        _process.Dispose();
    }
}
