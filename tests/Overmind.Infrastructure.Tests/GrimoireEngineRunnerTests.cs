using System.Diagnostics;
using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class GrimoireEngineRunnerTests
{
    [Fact]
    public async Task RunAsync_ScriptExitZero_CapturesStdout()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var path = WriteScript("#!/bin/sh\necho \"claude: grimoire added — /home/u/.claude.json\"\n");
        try
        {
            var runner = new GrimoireEngineRunner(new FixedLocator(path));

            var result = await runner.RunAsync(["install", "--with-grimoire"], CancellationToken.None);

            result.Started.ShouldBeTrue();
            result.ExitCode.ShouldBe(0);
            result.Succeeded.ShouldBeTrue();
            result.StandardOutput.ShouldContain("grimoire added");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunAsync_ScriptExitCode_ReportsHonestFailure()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var path = WriteScript("#!/bin/sh\necho boom >&2\nexit 3\n");
        try
        {
            var runner = new GrimoireEngineRunner(new FixedLocator(path));

            var result = await runner.RunAsync(["install"], CancellationToken.None);

            result.Started.ShouldBeTrue();
            result.ExitCode.ShouldBe(3);
            result.Succeeded.ShouldBeFalse();
            result.StandardError.ShouldContain("boom");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunAsync_Timeout_ReturnsNotStartedAndKillsProcess()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var path = WriteScript("#!/bin/sh\nsleep 30\n");
        try
        {
            var runner = new GrimoireEngineRunner(new FixedLocator(path), TimeSpan.FromMilliseconds(200));

            var result = await runner.RunAsync(["install"], CancellationToken.None);

            result.Started.ShouldBeFalse();
            result.Succeeded.ShouldBeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunAsync_LocatorFindsNothing_ReturnsNotStarted()
    {
        var runner = new GrimoireEngineRunner(new FixedLocator(null));

        var result = await runner.RunAsync(["install"], CancellationToken.None);

        result.Started.ShouldBeFalse();
        result.ExitCode.ShouldBeNull();
    }

    private static string WriteScript(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"overmind-engine-{Guid.NewGuid():N}.sh");
        File.WriteAllText(path, content);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return path;
    }

    private sealed class FixedLocator(string? path) : IOvermindCliLocator
    {
        public string? Locate() => path;
    }
}
