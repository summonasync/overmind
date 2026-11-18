using ModelContextProtocol;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class GitCliCancellationTests
{
    [Fact]
    public async Task RunAsync_CallerAlreadyCancelled_RethrowsCancellationAndNeverReportsATimeout()
    {
        var directory = Directory.CreateTempSubdirectory("overmind-gitcli-cancel").FullName;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        try
        {
            var error = await Record.ExceptionAsync(() =>
                new GitCli().RunAsync(directory, "status --porcelain", cancelled.Token));

            error.ShouldNotBeNull();
            error.ShouldBeAssignableTo<OperationCanceledException>();
            error.ShouldNotBeAssignableTo<McpException>();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
