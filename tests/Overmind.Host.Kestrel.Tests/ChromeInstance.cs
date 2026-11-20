using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

namespace Overmind.Host.Kestrel.Tests;

public sealed class ChromeInstance : IAsyncDisposable
{
    private readonly Process _process;
    private readonly string _userDataDirectory;

    private ChromeInstance(Process process, string userDataDirectory, string pageWebSocketUrl)
    {
        _process = process;
        _userDataDirectory = userDataDirectory;
        PageWebSocketUrl = pageWebSocketUrl;
    }

    public string PageWebSocketUrl { get; }

    public static async Task<ChromeInstance> LaunchAsync(CancellationToken ct)
    {
        var binary = ChromeLocator.Find()
            ?? throw new InvalidOperationException("chrome binary not found on this machine");
        var devtoolsPort = ReserveEphemeralPort();
        var userDataDirectory = Path.Combine(Path.GetTempPath(), $"overmind-chrome-{Guid.NewGuid():N}");

        var startInfo = new ProcessStartInfo
        {
            FileName = binary,
            UseShellExecute = false,
        };
        foreach (var argument in new[]
                 {
                     $"--remote-debugging-port={devtoolsPort}",
                     $"--user-data-dir={userDataDirectory}",
                     "--headless=new",
                     "--no-first-run",
                     "--no-default-browser-check",
                     "--disable-gpu",
                     "--disable-dev-shm-usage",
                     "--window-size=1440,1000",
                     "about:blank",
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"chrome process failed to start: {binary}");

        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{devtoolsPort}") };
        try
        {
            await WaitUntilDevToolsReadyAsync(httpClient, ct);
            var pageWebSocketUrl = await OpenPageTargetAsync(httpClient, ct);
            return new ChromeInstance(process, userDataDirectory, pageWebSocketUrl);
        }
        catch
        {
            await DisposeProcessAsync(process, userDataDirectory);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        DisposeProcessAsync(_process, _userDataDirectory).GetAwaiter().GetResult();
        return ValueTask.CompletedTask;
    }

    private static int ReserveEphemeralPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitUntilDevToolsReadyAsync(HttpClient httpClient, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var version = await httpClient.GetAsync("/json/version", ct);
                if (version.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
            }

            await Task.Delay(200, ct);
        }

        throw new TimeoutException("chrome devtools endpoint never became reachable");
    }

    private static async Task<string> OpenPageTargetAsync(HttpClient httpClient, CancellationToken ct)
    {
        using var created = new HttpRequestMessage(HttpMethod.Put, "/json/new?about:blank");
        using var response = await httpClient.SendAsync(created, ct);
        if (response.IsSuccessStatusCode)
        {
            var target = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            return RequirePageWebSocketUrl(target);
        }

        using var fallback = await httpClient.GetAsync("/json/new?about:blank", ct);
        var fallbackTarget = await fallback.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return RequirePageWebSocketUrl(fallbackTarget);
    }

    private static string RequirePageWebSocketUrl(JsonElement target) =>
        target.GetProperty("webSocketDebuggerUrl").GetString()
        ?? throw new InvalidOperationException("chrome page target carries no debugger url");

    private static async Task DisposeProcessAsync(Process process, string userDataDirectory)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }

        process.Dispose();
        await Task.CompletedTask;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(userDataDirectory))
                {
                    Directory.Delete(userDataDirectory, recursive: true);
                }

                return;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            await Task.Delay(200);
        }
    }
}
