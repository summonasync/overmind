using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Grimoire;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class GrimoireIntegrationServiceTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly string _grimoireDirectory = Path.Combine(
        Path.GetTempPath(), $"overmind-grimoire-{Guid.NewGuid():N}");

    public GrimoireIntegrationServiceTests()
    {
        Directory.CreateDirectory(_grimoireDirectory);
    }

    [Fact]
    public async Task GetStatusAsync_FreshSettings_ComposesSnapshotWithoutEngineRun()
    {
        var runner = new FakeRunner();
        var service = Build(runner, new FakeLocator(null), new FakeCli(new GrimoireVersionProbe(false, null)));

        var snapshot = await service.GetStatusAsync(CancellationToken.None);

        snapshot.Detection.Status.ShouldBe(GrimoireStatus.NotInstalled);
        snapshot.BookName.ShouldBe(string.Empty);
        snapshot.OverridePath.ShouldBe(string.Empty);
        snapshot.Wired.ShouldBeFalse();
        snapshot.InstallChannel.ShouldBe(string.Empty);
        runner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetStatusAsync_WiredSettingRead_ReportsTrue()
    {
        await using (var context = _temp.CreateContext())
        {
            await new AppSettingsService(context).SetAsync(
                SettingKeys.GrimoireWired, GrimoireIntegrationService.WiredTrue, CancellationToken.None);
        }

        var service = Build(
            new FakeRunner(), new FakeLocator(null), new FakeCli(new GrimoireVersionProbe(false, null)));

        var snapshot = await service.GetStatusAsync(CancellationToken.None);

        snapshot.Wired.ShouldBeTrue();
    }

    [Fact]
    public async Task WireAsync_Success_RunsEngineWithFlagAndSetsWired()
    {
        var stdout = EngineOutput("claude", "grimoire added", "/home/u/.claude.json", null);
        var runner = new FakeRunner(new GrimoireEngineRunResult(true, 0, stdout, string.Empty));
        var service = Build(
            runner, new FakeLocator("/usr/bin/grimoire"), new FakeCli(new GrimoireVersionProbe(true, "1.4.2")));

        var result = await service.WireAsync("owner-book", null, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Wired.ShouldBeTrue();
        result.Error.ShouldBeNull();
        runner.Calls.Count.ShouldBe(1);
        runner.Calls[0].ShouldBe(["install", "--with-grimoire"]);
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);
        (await settings.GetAsync(SettingKeys.GrimoireBookName, CancellationToken.None)).ShouldBe("owner-book");
        (await settings.GetAsync(SettingKeys.GrimoireWired, CancellationToken.None))
            .ShouldBe(GrimoireIntegrationService.WiredTrue);
    }

    [Fact]
    public async Task WireAsync_EngineExitOne_WiredStaysFalseAndOutcomesSurface()
    {
        var stdout = string.Join(
            '\n',
            EngineOutput("claude", "grimoire added", "/home/u/.claude.json", null),
            EngineOutput("codex", "grimoire failed", "/home/u/.codex/config.toml", "config locked"));
        var runner = new FakeRunner(new GrimoireEngineRunResult(true, 1, stdout, string.Empty));
        var service = Build(
            runner, new FakeLocator("/usr/bin/grimoire"), new FakeCli(new GrimoireVersionProbe(true, "1.4.2")));

        var result = await service.WireAsync("owner-book", null, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Wired.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Agents.Count.ShouldBe(2);
        result.Agents[0].Outcome.ShouldBe("added");
        result.Agents[1].Outcome.ShouldBe("failed");
        result.Agents[1].Error.ShouldBe("config locked");
        await using var context = _temp.CreateContext();
        (await new AppSettingsService(context).HasAsync(SettingKeys.GrimoireWired, CancellationToken.None))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task WireAsync_EngineNotStarted_ReturnsHonestErrorWithoutWiring()
    {
        var runner = new FakeRunner();
        var service = Build(
            runner, new FakeLocator("/usr/bin/grimoire"), new FakeCli(new GrimoireVersionProbe(true, "1.4.2")));

        var result = await service.WireAsync("owner-book", null, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Wired.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("Overmind CLI was not found");
        runner.Calls.Count.ShouldBe(1);
        await using var context = _temp.CreateContext();
        (await new AppSettingsService(context).HasAsync(SettingKeys.GrimoireWired, CancellationToken.None))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task WireAsync_GrimoireNotDetected_DoesNotRunEngine()
    {
        var runner = new FakeRunner();
        var service = Build(runner, new FakeLocator(null), new FakeCli(new GrimoireVersionProbe(false, null)));

        var result = await service.WireAsync("owner-book", null, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Wired.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("not found");
        runner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task WireAsync_OverridePathProvided_PersistedBeforeDetection()
    {
        var runner = new FakeRunner(new GrimoireEngineRunResult(true, 0, string.Empty, string.Empty));
        var service = Build(
            runner, new FakeLocator("/opt/grimoire/grimoire"), new FakeCli(new GrimoireVersionProbe(true, null)));

        await service.WireAsync("owner-book", "/opt/grimoire/grimoire", CancellationToken.None);

        await using var context = _temp.CreateContext();
        (await new AppSettingsService(context).GetAsync(SettingKeys.GrimoireOverridePath, CancellationToken.None))
            .ShouldBe("/opt/grimoire/grimoire");
    }

    [Fact]
    public async Task WireAsync_Success_ParsesSkippedAgents()
    {
        var stdout = string.Join(
            '\n',
            EngineOutput("claude", "grimoire added", "/home/u/.claude.json", null),
            SkippedLine("codex", "/home/u/.codex/config.toml"),
            EngineOutput("kimi-code", "grimoire unchanged", "/home/u/.kimi/config.json", null));
        var runner = new FakeRunner(new GrimoireEngineRunResult(true, 0, stdout, string.Empty));
        var service = Build(
            runner, new FakeLocator("/usr/bin/grimoire"), new FakeCli(new GrimoireVersionProbe(true, "1.4.2")));

        var result = await service.WireAsync("owner-book", null, CancellationToken.None);

        result.Agents.Count.ShouldBe(3);
        result.Agents[0].Outcome.ShouldBe("added");
        result.Agents[1].Outcome.ShouldBe("skipped");
        result.Agents[1].ConfigPath.ShouldBe("/home/u/.codex/config.toml");
        result.Agents[2].Outcome.ShouldBe("unchanged");
    }

    [Fact]
    public async Task AssistedInstallAsync_NoChannel_ReturnsDegradedInstructions()
    {
        var runner = new FakeRunner();
        var service = Build(runner, new FakeLocator(null), new FakeCli(new GrimoireVersionProbe(false, null)));

        var result = await service.AssistedInstallAsync(null, CancellationToken.None);

        result.Degraded.ShouldBeTrue();
        result.Success.ShouldBeFalse();
        result.Detection.ShouldBeNull();
        result.Instructions.ShouldNotBeEmpty();
        result.Error.ShouldBeNull();
        runner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task AssistedInstallAsync_ChannelProvided_PersistsChannelAndRunsCommand()
    {
        var runner = new FakeRunner(new GrimoireEngineRunResult(true, 0, "installed", string.Empty));
        WriteConfig(4770);
        var service = Build(
            runner,
            new FakeLocator("/usr/bin/grimoire"),
            new FakeCli(new GrimoireVersionProbe(true, "1.4.2")),
            new FakeHealth(true));

        var result = await service.AssistedInstallAsync(
            "dotnet tool install -g Grimoire.Tool", CancellationToken.None);

        result.Degraded.ShouldBeFalse();
        result.Success.ShouldBeTrue();
        result.Detection.ShouldNotBeNull();
        result.Detection.Status.ShouldBe(GrimoireStatus.Running);
        result.Error.ShouldBeNull();
        runner.Calls.Count.ShouldBe(1);
        runner.Calls[0].ShouldBe(["dotnet", "tool", "install", "-g", "Grimoire.Tool"]);
        await using var context = _temp.CreateContext();
        (await new AppSettingsService(context).GetAsync(SettingKeys.GrimoireInstallChannel, CancellationToken.None))
            .ShouldBe("dotnet tool install -g Grimoire.Tool");
    }

    [Fact]
    public async Task AssistedInstallAsync_ChannelFails_SurfacesErrorAndDetection()
    {
        var runner = new FakeRunner(
            new GrimoireEngineRunResult(true, 1, string.Empty, "nuget feed unreachable"));
        var service = Build(runner, new FakeLocator(null), new FakeCli(new GrimoireVersionProbe(false, null)));

        var result = await service.AssistedInstallAsync("dotnet tool install -g Grimoire.Tool", CancellationToken.None);

        result.Degraded.ShouldBeFalse();
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("exited with code 1");
        result.Error.ShouldContain("nuget feed unreachable");
        result.Detection.ShouldNotBeNull();
        result.Detection.Status.ShouldBe(GrimoireStatus.NotInstalled);
        result.Instructions.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AssistedInstallAsync_ChannelSucceedsButBinaryMissing_ReportsHonestState()
    {
        var runner = new FakeRunner(new GrimoireEngineRunResult(true, 0, "installed", string.Empty));
        var service = Build(runner, new FakeLocator(null), new FakeCli(new GrimoireVersionProbe(false, null)));

        var result = await service.AssistedInstallAsync("dotnet tool install -g Grimoire.Tool", CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("still not detectable");
        result.Detection.ShouldNotBeNull();
        result.Detection.Status.ShouldBe(GrimoireStatus.NotInstalled);
    }

    [Fact]
    public async Task DisconnectAsync_Success_RunsRemoveFlagAndClearsWired()
    {
        await using (var context = _temp.CreateContext())
        {
            await new AppSettingsService(context).SetAsync(
                SettingKeys.GrimoireWired, GrimoireIntegrationService.WiredTrue, CancellationToken.None);
        }

        var runner = new FakeRunner(new GrimoireEngineRunResult(true, 0, "removed", string.Empty));
        var service = Build(
            runner, new FakeLocator("/usr/bin/grimoire"), new FakeCli(new GrimoireVersionProbe(true, "1.4.2")));

        var result = await service.DisconnectAsync(CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Wired.ShouldBeFalse();
        result.Error.ShouldBeNull();
        runner.Calls.Count.ShouldBe(1);
        runner.Calls[0].ShouldBe(["install", "--remove-grimoire"]);
        await using var verifyContext = _temp.CreateContext();
        (await new AppSettingsService(verifyContext).GetAsync(SettingKeys.GrimoireWired, CancellationToken.None))
            .ShouldBe(GrimoireIntegrationService.WiredFalse);
    }

    [Fact]
    public async Task DisconnectAsync_EngineFailure_WiredStaysTrue()
    {
        await using (var context = _temp.CreateContext())
        {
            await new AppSettingsService(context).SetAsync(
                SettingKeys.GrimoireWired, GrimoireIntegrationService.WiredTrue, CancellationToken.None);
        }

        var runner = new FakeRunner(new GrimoireEngineRunResult(true, 1, string.Empty, "config locked"));
        var service = Build(
            runner, new FakeLocator("/usr/bin/grimoire"), new FakeCli(new GrimoireVersionProbe(true, "1.4.2")));

        var result = await service.DisconnectAsync(CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Wired.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("config locked");
        await using var verifyContext = _temp.CreateContext();
        (await new AppSettingsService(verifyContext).GetAsync(SettingKeys.GrimoireWired, CancellationToken.None))
            .ShouldBe(GrimoireIntegrationService.WiredTrue);
    }

    private GrimoireIntegrationService Build(
        FakeRunner runner,
        FakeLocator locator,
        FakeCli cli,
        FakeHealth? health = null)
    {
        var detection = new GrimoireDetectionService(
            new AppSettingsService(_temp.CreateContext()),
            locator,
            cli,
            health ?? new FakeHealth(false),
            new GrimoireConfigReader(_grimoireDirectory));

        return new GrimoireIntegrationService(new AppSettingsService(_temp.CreateContext()), detection, runner);
    }

    private void WriteConfig(int port)
    {
        File.WriteAllText(
            Path.Combine(_grimoireDirectory, "config.json"),
            $$"""{ "serve": { "port": {{port}} } }""");
    }

    private static string EngineOutput(
        string agent, string verb, string path, string? error) =>
        error is null
            ? $"{agent}: {verb} — {path}"
            : $"{agent}: {verb} — {path} ({error})";

    private static string SkippedLine(string agent, string path) =>
        $"{agent}: skipped (not detected) — {path}";

    public void Dispose()
    {
        _temp.Dispose();
        Directory.Delete(_grimoireDirectory, recursive: true);
    }

    private sealed class FakeRunner(params GrimoireEngineRunResult[] results) : IGrimoireEngineRunner
    {
        private int _index;

        public List<IReadOnlyList<string>> Calls { get; } = [];

        public Task<GrimoireEngineRunResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct)
        {
            Calls.Add(arguments);
            var result = _index < results.Length ? results[_index] : GrimoireEngineRunResult.NotStarted;
            _index++;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeLocator(string? located) : IGrimoireBinaryLocator
    {
        public string? Locate(string? overridePath) => located;
    }

    private sealed class FakeCli(GrimoireVersionProbe probe) : IGrimoireCli
    {
        public Task<GrimoireVersionProbe> ProbeVersionAsync(string binaryPath, CancellationToken ct) =>
            Task.FromResult(probe);
    }

    private sealed class FakeHealth(bool healthy) : IGrimoireHealthClient
    {
        public Task<bool> IsHealthyAsync(int port, CancellationToken ct) => Task.FromResult(healthy);
    }
}
