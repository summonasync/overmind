using System.Diagnostics;

namespace Overmind.Infrastructure.Grimoire;

public sealed class GrimoireEngineRunner(
    IOvermindCliLocator locator,
    TimeSpan? timeout = null) : IGrimoireEngineRunner
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    private readonly TimeSpan _timeout = timeout ?? DefaultTimeout;

    public async Task<GrimoireEngineRunResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var binaryPath = locator.Locate();
        if (binaryPath is null)
        {
            return GrimoireEngineRunResult.NotStarted;
        }

        using var process = new Process { StartInfo = CreateStartInfo(binaryPath, arguments) };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_timeout);
        try
        {
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return new GrimoireEngineRunResult(true, process.ExitCode, await output, await error);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (!ct.IsCancellationRequested)
            {
                return GrimoireEngineRunResult.NotStarted;
            }

            throw;
        }
        catch (Exception)
        {
            return GrimoireEngineRunResult.NotStarted;
        }
    }

    private static ProcessStartInfo CreateStartInfo(string binaryPath, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(binaryPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
        }
    }
}
