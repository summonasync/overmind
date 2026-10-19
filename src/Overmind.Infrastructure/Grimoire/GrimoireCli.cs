using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Overmind.Infrastructure.Grimoire;

public sealed partial class GrimoireCli : IGrimoireCli
{
    public static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _probeTimeout;

    public GrimoireCli(TimeSpan? probeTimeout = null)
    {
        _probeTimeout = probeTimeout ?? DefaultProbeTimeout;
    }

    public async Task<GrimoireVersionProbe> ProbeVersionAsync(string binaryPath, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(binaryPath, "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_probeTimeout);
        try
        {
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return new GrimoireVersionProbe(true, FindVersion(await output));
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (!ct.IsCancellationRequested)
            {
                return new GrimoireVersionProbe(false, null);
            }

            throw;
        }
        catch (Exception)
        {
            return new GrimoireVersionProbe(false, null);
        }
    }

    private static string? FindVersion(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            var match = VersionToken().Match(line);
            if (match.Success)
            {
                return match.Value;
            }
        }

        return null;
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

    [GeneratedRegex(@"v?\d+(\.\d+)+")]
    private static partial Regex VersionToken();
}
