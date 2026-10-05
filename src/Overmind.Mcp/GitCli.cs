using System.Diagnostics;
using ModelContextProtocol;

namespace Overmind.Mcp;

public sealed class GitCli : IGitCli
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public async Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new McpException($"git could not be started: {ex.Message}");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return new GitCommandResult(process.ExitCode, await output, await error);
        }
        catch (OperationCanceledException)
        {
            // Kill on every cancellation path — the external client token must not orphan git.
            TryKill(process);
            if (!cancellationToken.IsCancellationRequested)
            {
                throw new McpException($"git {arguments} timed out after {Timeout.TotalSeconds:0}s in {workingDirectory}.");
            }

            throw;
        }
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
