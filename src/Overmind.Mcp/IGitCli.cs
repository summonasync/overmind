namespace Overmind.Mcp;

public interface IGitCli
{
    Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken);
}
