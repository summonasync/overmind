namespace Overmind.Mcp.Registration;

public sealed class OnboardBlockWriter
{
    private readonly ConfigBackupTracker _backupTracker;

    public OnboardBlockWriter(ConfigBackupTracker backupTracker)
    {
        ArgumentNullException.ThrowIfNull(backupTracker);
        _backupTracker = backupTracker;
    }

    public async Task<OnboardWriteResult> WriteAsync(
        string filePath,
        string block,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(block);

        var existing = File.Exists(filePath)
            ? await File.ReadAllTextAsync(filePath, cancellationToken)
            : null;

        var merged = OnboardManagedBlock.Merge(existing, block);
        var outcome = existing is null
            ? OnboardWriteOutcome.Created
            : string.Equals(merged, existing, StringComparison.Ordinal)
                ? OnboardWriteOutcome.Unchanged
                : OnboardWriteOutcome.Updated;

        if (!dryRun && outcome != OnboardWriteOutcome.Unchanged)
        {
            await AtomicConfigFileWriter.WriteAsync(filePath, merged, _backupTracker, cancellationToken);
        }

        return new OnboardWriteResult
        {
            Outcome = outcome,
            FilePath = filePath,
            Conflicts = AgentFileConflictScanner.Scan(existing),
        };
    }
}
