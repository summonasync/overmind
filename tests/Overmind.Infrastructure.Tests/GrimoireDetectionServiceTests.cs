using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Grimoire;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class GrimoireDetectionServiceTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly string _grimoireDirectory = Path.Combine(
        Path.GetTempPath(), $"overmind-grimoire-{Guid.NewGuid():N}");

    public GrimoireDetectionServiceTests()
    {
        Directory.CreateDirectory(_grimoireDirectory);
    }

    [Fact]
    public async Task DetectAsync_BinaryMissingEverywhere_ReturnsNotInstalled()
    {
        var service = Build(
            new FakeLocator(null),
            new FakeCli(new GrimoireVersionProbe(true, "1.0.0")),
            new FakeHealth(true));

        var result = await service.DetectAsync(CancellationToken.None);

        result.Status.ShouldBe(GrimoireStatus.NotInstalled);
        result.BinaryPath.ShouldBeNull();
        result.Version.ShouldBeNull();
        result.Port.ShouldBeNull();
    }

    [Fact]
    public async Task DetectAsync_VersionProbeAnswered_ReturnsInstalled()
    {
        var service = Build(
            new FakeLocator("/usr/local/bin/grimoire"),
            new FakeCli(new GrimoireVersionProbe(true, null)),
            new FakeHealth(false));

        var result = await service.DetectAsync(CancellationToken.None);

        result.Status.ShouldBe(GrimoireStatus.Installed);
        result.BinaryPath.ShouldBe("/usr/local/bin/grimoire");
        result.Version.ShouldBeNull();
    }

    [Fact]
    public async Task DetectAsync_HealthEndpointAnswering_ReturnsRunning()
    {
        WriteConfig(4770);
        var health = new FakeHealth(true);
        var service = Build(
            new FakeLocator("/usr/local/bin/grimoire"),
            new FakeCli(new GrimoireVersionProbe(true, "1.4.2")),
            health);

        var result = await service.DetectAsync(CancellationToken.None);

        result.Status.ShouldBe(GrimoireStatus.Running);
        result.Version.ShouldBe("1.4.2");
        result.Port.ShouldBe(4770);
        health.RequestedPort.ShouldBe(4770);
    }

    [Fact]
    public async Task DetectAsync_HealthEndpointSilent_ReturnsInstalledWithConfiguredPort()
    {
        WriteConfig(4770);
        var service = Build(
            new FakeLocator("/usr/local/bin/grimoire"),
            new FakeCli(new GrimoireVersionProbe(true, "1.4.2")),
            new FakeHealth(false));

        var result = await service.DetectAsync(CancellationToken.None);

        result.Status.ShouldBe(GrimoireStatus.Installed);
        result.Port.ShouldBe(4770);
    }

    [Fact]
    public async Task DetectAsync_ConfigFileMissing_KeepsPortUnknown()
    {
        var service = Build(
            new FakeLocator("/usr/local/bin/grimoire"),
            new FakeCli(new GrimoireVersionProbe(true, "1.4.2")),
            new FakeHealth(false));

        var result = await service.DetectAsync(CancellationToken.None);

        result.Status.ShouldBe(GrimoireStatus.Installed);
        result.Port.ShouldBeNull();
    }

    [Fact]
    public async Task DetectAsync_VersionProbeFails_ReturnsNotInstalled()
    {
        var service = Build(
            new FakeLocator("/usr/local/bin/grimoire"),
            new FakeCli(new GrimoireVersionProbe(false, null)),
            new FakeHealth(true));

        var result = await service.DetectAsync(CancellationToken.None);

        result.Status.ShouldBe(GrimoireStatus.NotInstalled);
        result.BinaryPath.ShouldBeNull();
    }

    [Fact]
    public async Task DetectAsync_OverrideSettingForwardedToLocatorAndProbe()
    {
        await using var context = _temp.CreateContext();
        await new AppSettingsService(context).SetAsync(
            SettingKeys.GrimoireOverridePath, "/opt/grimoire/grimoire", CancellationToken.None);
        var locator = new FakeLocator("/opt/grimoire/grimoire");
        var cli = new FakeCli(new GrimoireVersionProbe(true, null));
        var service = new GrimoireDetectionService(
            new AppSettingsService(_temp.CreateContext()), locator, cli, new FakeHealth(false),
            new GrimoireConfigReader(_grimoireDirectory));

        var result = await service.DetectAsync(CancellationToken.None);

        locator.ReceivedOverride.ShouldBe("/opt/grimoire/grimoire");
        cli.RequestedBinaryPath.ShouldBe("/opt/grimoire/grimoire");
        result.BinaryPath.ShouldBe("/opt/grimoire/grimoire");
    }

    private GrimoireDetectionService Build(
        IGrimoireBinaryLocator locator,
        IGrimoireCli cli,
        IGrimoireHealthClient health)
    {
        return new GrimoireDetectionService(
            new AppSettingsService(_temp.CreateContext()), locator, cli, health,
            new GrimoireConfigReader(_grimoireDirectory));
    }

    private void WriteConfig(int port)
    {
        File.WriteAllText(
            Path.Combine(_grimoireDirectory, "config.json"),
            $$"""{ "serve": { "port": {{port}} } }""");
    }

    public void Dispose()
    {
        _temp.Dispose();
        Directory.Delete(_grimoireDirectory, recursive: true);
    }

    private sealed class FakeLocator(string? located) : IGrimoireBinaryLocator
    {
        public string? ReceivedOverride { get; private set; }

        public string? Locate(string? overridePath)
        {
            ReceivedOverride = overridePath;
            return located;
        }
    }

    private sealed class FakeCli(GrimoireVersionProbe probe) : IGrimoireCli
    {
        public string? RequestedBinaryPath { get; private set; }

        public Task<GrimoireVersionProbe> ProbeVersionAsync(string binaryPath, CancellationToken ct)
        {
            RequestedBinaryPath = binaryPath;
            return Task.FromResult(probe);
        }
    }

    private sealed class FakeHealth(bool healthy) : IGrimoireHealthClient
    {
        public int? RequestedPort { get; private set; }

        public Task<bool> IsHealthyAsync(int port, CancellationToken ct)
        {
            RequestedPort = port;
            return Task.FromResult(healthy);
        }
    }
}
