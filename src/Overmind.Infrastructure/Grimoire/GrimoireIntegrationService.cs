using Overmind.Infrastructure.Configuration;

namespace Overmind.Infrastructure.Grimoire;

public sealed class GrimoireIntegrationService
{
    public const string WiredTrue = "true";
    public const string WiredFalse = "false";

    private const string InstallSubcommand = "install";
    private const string WithGrimoireFlag = "--with-grimoire";
    private const string RemoveGrimoireFlag = "--remove-grimoire";
    private const int ErrorSnippetLength = 300;

    private readonly AppSettingsService _settings;
    private readonly GrimoireDetectionService _detection;
    private readonly IGrimoireEngineRunner _engine;

    public GrimoireIntegrationService(
        AppSettingsService settings,
        GrimoireDetectionService detection,
        IGrimoireEngineRunner engine)
    {
        _settings = settings;
        _detection = detection;
        _engine = engine;
    }

    public async Task<GrimoireStatusSnapshot> GetStatusAsync(CancellationToken ct)
    {
        var detection = await _detection.DetectAsync(ct);
        return await BuildSnapshotAsync(detection, ct);
    }

    public async Task<GrimoireWireResult> WireAsync(string bookName, string? overridePath, CancellationToken ct)
    {
        await _settings.SetAsync(SettingKeys.GrimoireBookName, bookName.Trim(), ct);
        if (overridePath is not null)
        {
            await _settings.SetAsync(SettingKeys.GrimoireOverridePath, overridePath.Trim(), ct);
        }

        var detection = await _detection.DetectAsync(ct);
        if (detection.Status == GrimoireStatus.NotInstalled)
        {
            return new GrimoireWireResult(
                false, await IsWiredAsync(ct), [],
                "Grimoire binary was not found on PATH or via the override path; nothing was wired.");
        }

        var run = await _engine.RunAsync([InstallSubcommand, WithGrimoireFlag], ct);
        if (!run.Started)
        {
            return new GrimoireWireResult(
                false, await IsWiredAsync(ct), [],
                "Overmind CLI was not found next to the host or on PATH; nothing was wired.");
        }

        var agents = GrimoireOutcomeParser.Parse(run.StandardOutput);
        if (!run.Succeeded)
        {
            return new GrimoireWireResult(false, await IsWiredAsync(ct), agents, DescribeFailure(run));
        }

        await _settings.SetAsync(SettingKeys.GrimoireWired, WiredTrue, ct);
        return new GrimoireWireResult(true, true, agents, null);
    }

    public async Task<GrimoireAssistedInstallResult> AssistedInstallAsync(string? channel, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(channel))
        {
            await _settings.SetAsync(SettingKeys.GrimoireInstallChannel, channel.Trim(), ct);
        }

        var resolved = (await _settings.GetOrDefaultAsync(
                SettingKeys.GrimoireInstallChannel,
                SettingDefaults.GrimoireInstallChannel,
                ct))
            .Trim();

        if (resolved.Length == 0)
        {
            return new GrimoireAssistedInstallResult(
                true, false, null, GrimoireManualInstructions.Steps, null);
        }

        var run = await _engine.RunAsync(SplitCommand(resolved), ct);
        if (!run.Succeeded)
        {
            return new GrimoireAssistedInstallResult(
                false, false, await _detection.DetectAsync(ct),
                GrimoireManualInstructions.Steps, DescribeFailure(run));
        }

        var detection = await _detection.DetectAsync(ct);
        if (detection.Status == GrimoireStatus.NotInstalled)
        {
            return new GrimoireAssistedInstallResult(
                false, false, detection, GrimoireManualInstructions.Steps,
                "The install channel finished, but the grimoire binary is still not detectable.");
        }

        return new GrimoireAssistedInstallResult(false, true, detection, [], null);
    }

    public async Task<GrimoireDisconnectResult> DisconnectAsync(CancellationToken ct)
    {
        var run = await _engine.RunAsync([InstallSubcommand, RemoveGrimoireFlag], ct);
        if (!run.Started)
        {
            return new GrimoireDisconnectResult(
                false, await IsWiredAsync(ct),
                "Overmind CLI was not found next to the host or on PATH; agents were not unwired.");
        }

        if (!run.Succeeded)
        {
            return new GrimoireDisconnectResult(false, await IsWiredAsync(ct), DescribeFailure(run));
        }

        await _settings.SetAsync(SettingKeys.GrimoireWired, WiredFalse, ct);
        return new GrimoireDisconnectResult(true, false, null);
    }

    private async Task<GrimoireStatusSnapshot> BuildSnapshotAsync(
        GrimoireDetectionResult detection, CancellationToken ct)
    {
        var bookName = await _settings.GetOrDefaultAsync(
            SettingKeys.GrimoireBookName, SettingDefaults.GrimoireBookName, ct);
        var overridePath = await _settings.GetOrDefaultAsync(
            SettingKeys.GrimoireOverridePath, SettingDefaults.GrimoireOverridePath, ct);
        var channel = await _settings.GetOrDefaultAsync(
            SettingKeys.GrimoireInstallChannel, SettingDefaults.GrimoireInstallChannel, ct);

        return new GrimoireStatusSnapshot(
            detection, bookName, overridePath, await IsWiredAsync(ct), channel);
    }

    private async Task<bool> IsWiredAsync(CancellationToken ct)
    {
        var wired = await _settings.GetOrDefaultAsync(SettingKeys.GrimoireWired, SettingDefaults.GrimoireWired, ct);
        return string.Equals(wired?.Trim(), WiredTrue, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> SplitCommand(string command) => command.Split(
        ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string DescribeFailure(GrimoireEngineRunResult run)
    {
        if (run.ExitCode is null)
        {
            return "The engine command did not finish within the time limit.";
        }

        var stderr = run.StandardError.Trim();
        if (stderr.Length == 0)
        {
            return $"The engine command exited with code {run.ExitCode}.";
        }

        if (stderr.Length > ErrorSnippetLength)
        {
            stderr = stderr[^ErrorSnippetLength..];
        }

        return $"The engine command exited with code {run.ExitCode}: {stderr}";
    }
}
