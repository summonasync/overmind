using Overmind.Infrastructure.Configuration;

namespace Overmind.Infrastructure.Grimoire;

public sealed class GrimoireDetectionService
{
    private readonly AppSettingsService _settings;
    private readonly IGrimoireBinaryLocator _locator;
    private readonly IGrimoireCli _cli;
    private readonly IGrimoireHealthClient _health;
    private readonly GrimoireConfigReader _configReader;

    public GrimoireDetectionService(
        AppSettingsService settings,
        IGrimoireBinaryLocator locator,
        IGrimoireCli cli,
        IGrimoireHealthClient health,
        GrimoireConfigReader? configReader = null)
    {
        _settings = settings;
        _locator = locator;
        _cli = cli;
        _health = health;
        _configReader = configReader ?? new GrimoireConfigReader();
    }

    public async Task<GrimoireDetectionResult> DetectAsync(CancellationToken ct)
    {
        var overridePath = await _settings.GetOrDefaultAsync(
            SettingKeys.GrimoireOverridePath, SettingDefaults.GrimoireOverridePath, ct);
        var binaryPath = _locator.Locate(overridePath);
        if (binaryPath is null)
        {
            return new GrimoireDetectionResult(GrimoireStatus.NotInstalled, null, null, null);
        }

        var probe = await _cli.ProbeVersionAsync(binaryPath, ct);
        if (!probe.Started)
        {
            return new GrimoireDetectionResult(GrimoireStatus.NotInstalled, null, null, null);
        }

        var port = _configReader.Read().Port;
        if (port is int listening && await _health.IsHealthyAsync(listening, ct))
        {
            return new GrimoireDetectionResult(GrimoireStatus.Running, binaryPath, probe.Version, port);
        }

        return new GrimoireDetectionResult(GrimoireStatus.Installed, binaryPath, probe.Version, port);
    }
}
