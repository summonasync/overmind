using System.Diagnostics;

namespace Overmind.Host.Kestrel.Tests;

public static class WebBuildRunner
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromMinutes(15);

    public static async Task<WebBuildResult> RunAsync(
        NodeToolchain toolchain,
        string webDirectory,
        CancellationToken ct)
    {
        var logPath = Path.Combine(Path.GetTempPath(), $"overmind-web-build-{Guid.NewGuid():N}.log");

        var install = await RunNpmAsync(toolchain, webDirectory, ["ci"], logPath, ct);
        if (!install)
        {
            return new WebBuildResult(false, logPath, $"npm ci failed, log: {logPath}");
        }

        var build = await RunNpmAsync(toolchain, webDirectory, ["run", "build"], logPath, ct);
        return build
            ? new WebBuildResult(true, logPath, null)
            : new WebBuildResult(false, logPath, $"npm run build failed, log: {logPath}");
    }

    private static async Task<bool> RunNpmAsync(
        NodeToolchain toolchain,
        string webDirectory,
        IReadOnlyList<string> arguments,
        string logPath,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = toolchain.NpmPath,
            WorkingDirectory = webDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["PATH"] =
            $"{Path.GetDirectoryName(toolchain.NodePath)}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";

        using var process = new Process { StartInfo = startInfo };
        await using var log = new StreamWriter(logPath, append: false) { AutoFlush = true };
        process.Start();
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(StepTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(standardOutput, standardError);
            await log.WriteLineAsync(await standardOutput);
            await log.WriteLineAsync(await standardError);
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            await DrainQuietly(standardOutput, standardError);
            await log.WriteLineAsync($"step {string.Join(' ', arguments)} exceeded {StepTimeout}");
            if (ct.IsCancellationRequested)
            {
                throw;
            }

            return false;
        }
    }

    private static async Task DrainQuietly(Task<string> standardOutput, Task<string> standardError)
    {
        try
        {
            await Task.WhenAll(standardOutput, standardError);
        }
        catch (Exception)
        {
        }
    }

    private static void KillProcessTree(Process process)
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
    }
}
