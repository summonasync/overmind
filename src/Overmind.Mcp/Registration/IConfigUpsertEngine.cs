namespace Overmind.Mcp.Registration;

public interface IConfigUpsertEngine
{
    AgentConfigFormat Format { get; }

    Task<ConfigUpsertResult> UpsertAsync(
        string filePath,
        string commandPath,
        bool dryRun,
        CancellationToken cancellationToken,
        ManagedServerKey key = ManagedServerKey.Overmind);

    Task<ConfigUpsertResult> RemoveAsync(
        string filePath,
        bool dryRun,
        CancellationToken cancellationToken,
        ManagedServerKey key = ManagedServerKey.Overmind);
}
