namespace Overmind.Mcp.Registration;

public sealed class InstallCommand
{
    private readonly AgentPathEnvironment _environment;
    private readonly string _workingDirectory;
    private readonly string _binaryPath;
    private readonly TextWriter _output;
    private readonly TextWriter _errorOutput;
    private readonly IReadOnlyDictionary<AgentConfigFormat, IConfigUpsertEngine> _engines;
    private readonly OnboardBlockWriter _onboardWriter;
    private readonly OnboardProjectResolver? _projectResolver;

    public InstallCommand(
        AgentPathEnvironment environment,
        string workingDirectory,
        string binaryPath,
        TextWriter output,
        TextWriter errorOutput)
        : this(environment, workingDirectory, binaryPath, output, errorOutput, projectResolver: null)
    {
    }

    public InstallCommand(
        AgentPathEnvironment environment,
        string workingDirectory,
        string binaryPath,
        TextWriter output,
        TextWriter errorOutput,
        OnboardProjectResolver? projectResolver)
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
        _projectResolver = projectResolver;

        var backupTracker = new ConfigBackupTracker();
        _onboardWriter = new OnboardBlockWriter(backupTracker);
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

        string? onboardRoot = null;
        if (options.Onboard)
        {
            onboardRoot = GitRootResolver.FindRoot(_workingDirectory) ?? _workingDirectory;
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

        var selection = PlanSelection(options).ToList();
        foreach (var (agent, selected) in selection)
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

        OnboardRunSummary? onboard = null;
        if (options.Onboard)
        {
            onboard = await RunOnboardingAsync(selection, onboardRoot!, options, cancellationToken);
        }

        var summary =
            $"summary: {processed} processed, {added} added, {updated} updated, {unchanged} unchanged, " +
            $"{removed} removed, {notPresent} not present, {skipped} skipped, {failed} failed";
        if (onboard is not null)
        {
            summary +=
                $"; onboard: {onboard.Created} created, {onboard.Updated} updated, {onboard.Unchanged} unchanged, " +
                $"{onboard.Failed} failed, {onboard.Conflicts} conflict warning(s)";
        }

        await _output.WriteLineAsync(summary);

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

    private async Task<OnboardRunSummary> RunOnboardingAsync(
        IReadOnlyList<(AgentDefinition Agent, bool Selected)> selection,
        string repoRoot,
        InstallOptions options,
        CancellationToken cancellationToken)
    {
        var resolver = _projectResolver ?? OnboardProjectResolver.FromEnvironment();
        var projectInfo = await resolver.ResolveAsync(repoRoot, cancellationToken);
        if (projectInfo is null)
        {
            await _errorOutput.WriteLineAsync(
                "warning: overmind host is unreachable — the onboard block will use placeholders; " +
                "re-run 'overmind-mcp install --onboard' once the host is running to embed the real project id.");
        }

        var block = OnboardBlockContent.Render(projectInfo);
        var summary = new OnboardRunSummary();

        foreach (var (agent, selected) in selection)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!selected)
            {
                continue;
            }

            var filePath = AgentInstructionFile.ResolvePath(agent, repoRoot);
            try
            {
                var result = await _onboardWriter.WriteAsync(filePath, block, options.DryRun, cancellationToken);
                await ReportConflictsAsync(agent.Name, result);
                summary.Add(result.Outcome, result.Conflicts.Count);
                await _output.WriteLineAsync(FormatOnboardLine(agent.Name, result, options.DryRun));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                summary.Failed++;
                await _output.WriteLineAsync($"{agent.Name}: onboard failed — {filePath} ({ex.Message})");
            }
        }

        return summary;
    }

    private async Task ReportConflictsAsync(string name, OnboardWriteResult result)
    {
        if (result.Conflicts.Count == 0)
        {
            return;
        }

        await _errorOutput.WriteLineAsync(
            $"warning: {name}: {result.FilePath} prescribes file-based ceremony outside the managed block (left untouched):");
        foreach (var conflict in result.Conflicts)
        {
            await _errorOutput.WriteLineAsync($"  line {conflict.LineNumber}: {conflict.Text}");
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

    private static string FormatOnboardLine(string name, OnboardWriteResult result, bool dryRun)
    {
        var verb = result.Outcome switch
        {
            OnboardWriteOutcome.Created => dryRun ? "would create onboard block" : "onboard block created",
            OnboardWriteOutcome.Updated => dryRun ? "would update onboard block" : "onboard block updated",
            _ => "onboard block unchanged",
        };

        return $"{name}: {verb} — {result.FilePath}";
    }

    private sealed class OnboardRunSummary
    {
        public int Created { get; private set; }
        public int Updated { get; private set; }
        public int Unchanged { get; private set; }
        public int Failed { get; set; }
        public int Conflicts { get; private set; }

        public void Add(OnboardWriteOutcome outcome, int conflictCount)
        {
            Conflicts += conflictCount;
            switch (outcome)
            {
                case OnboardWriteOutcome.Created:
                    Created++;
                    break;
                case OnboardWriteOutcome.Updated:
                    Updated++;
                    break;
                default:
                    Unchanged++;
                    break;
            }
        }
    }
}
