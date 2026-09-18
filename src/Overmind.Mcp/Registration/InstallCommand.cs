namespace Overmind.Mcp.Registration;

public sealed class InstallCommand
{
    private readonly AgentPathEnvironment _environment;
    private readonly string _workingDirectory;
    private readonly string _binaryPath;
    private readonly TextWriter _output;
    private readonly TextWriter _errorOutput;
    private readonly IReadOnlyDictionary<AgentConfigFormat, IConfigUpsertEngine> _engines;

    public InstallCommand(
        AgentPathEnvironment environment,
        string workingDirectory,
        string binaryPath,
        TextWriter output,
        TextWriter errorOutput)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(binaryPath);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(errorOutput);

        _environment = environment;
        _workingDirectory = workingDirectory;
        _binaryPath = binaryPath;
        _output = output;
        _errorOutput = errorOutput;

        var backupTracker = new ConfigBackupTracker();
        _engines = new Dictionary<AgentConfigFormat, IConfigUpsertEngine>
        {
            [AgentConfigFormat.Json] = new JsonConfigUpsertEngine(backupTracker),
            [AgentConfigFormat.Toml] = new TomlConfigUpsertEngine(backupTracker),
        };
    }

    public async Task<int> RunAsync(InstallOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        string? projectRoot = null;
        if (options.Project)
        {
            projectRoot = GitRootResolver.FindRoot(_workingDirectory);
            if (projectRoot is null)
            {
                await _errorOutput.WriteLineAsync(
                    $"install: --project requires a git repository (no .git found walking up from '{_workingDirectory}').");
                return 1;
            }
        }

        if (options.DryRun)
        {
            await _output.WriteLineAsync("dry run: no config files are written.");
        }

        var commandPath = StableInstallLinkResolver.ResolvePreferredBinaryPath(_binaryPath, _environment);
        var processed = 0;
        var added = 0;
        var updated = 0;
        var unchanged = 0;
        var removed = 0;
        var notPresent = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var (agent, selected) in PlanSelection(options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;

            var configPath = projectRoot is null
                ? AgentPathResolver.ResolveUserConfigPath(agent, _environment)
                : AgentPathResolver.ResolveProjectConfigPath(agent, projectRoot);

            if (!selected)
            {
                skipped++;
                await _output.WriteLineAsync($"{agent.Name}: skipped (not detected) — {configPath}");
                continue;
            }

            var result = await InvokeEngineAsync(agent, configPath, commandPath, options, cancellationToken);

            switch (result.Outcome)
            {
                case ConfigUpsertOutcome.Added:
                    added++;
                    break;
                case ConfigUpsertOutcome.Updated:
                    updated++;
                    break;
                case ConfigUpsertOutcome.Unchanged:
                    unchanged++;
                    break;
                case ConfigUpsertOutcome.Removed:
                    removed++;
                    break;
                case ConfigUpsertOutcome.NotPresent:
                    notPresent++;
                    break;
                default:
                    failed++;
                    break;
            }

            await _output.WriteLineAsync(FormatLine(agent.Name, configPath, result, options.DryRun));
        }

        await _output.WriteLineAsync(
            $"summary: {processed} processed, {added} added, {updated} updated, {unchanged} unchanged, " +
            $"{removed} removed, {notPresent} not present, {skipped} skipped, {failed} failed");

        return failed == 0 ? 0 : 1;
    }

    private async Task<ConfigUpsertResult> InvokeEngineAsync(
        AgentDefinition agent,
        string configPath,
        string commandPath,
        InstallOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var engine = _engines[agent.ConfigFormat];
            return options.Remove
                ? await engine.RemoveAsync(configPath, options.DryRun, cancellationToken)
                : await engine.UpsertAsync(configPath, commandPath, options.DryRun, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ConfigUpsertResult.Failure(ex.Message);
        }
    }

    private IEnumerable<(AgentDefinition Agent, bool Selected)> PlanSelection(InstallOptions options)
    {
        if (options.Agents.Count > 0)
        {
            foreach (var name in options.Agents)
            {
                yield return (AgentRegistry.Find(name)!, true);
            }

            yield break;
        }

        foreach (var agent in AgentRegistry.All)
        {
            yield return (agent, options.All || AgentDetector.IsDetected(agent, _environment));
        }
    }

    private static string FormatLine(string name, string path, ConfigUpsertResult result, bool dryRun)
    {
        var verb = result.Outcome switch
        {
            ConfigUpsertOutcome.Added => dryRun ? "would add" : "added",
            ConfigUpsertOutcome.Updated => dryRun ? "would update" : "updated",
            ConfigUpsertOutcome.Removed => dryRun ? "would remove" : "removed",
            ConfigUpsertOutcome.Unchanged => "unchanged",
            ConfigUpsertOutcome.NotPresent => "not present",
            _ => "failed",
        };

        return result.Error is null
            ? $"{name}: {verb} — {path}"
            : $"{name}: {verb} — {path} ({result.Error})";
    }
}
