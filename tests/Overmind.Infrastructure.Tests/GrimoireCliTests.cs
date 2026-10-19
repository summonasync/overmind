using System.Diagnostics;
using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class GrimoireCliTests
{
    [Fact]
    public async Task ProbeVersionAsync_ScriptPrintsVersion_ParsesToken()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var path = WriteScript("#!/bin/sh\necho \"grimoire 1.4.2\"\n");
        try
        {
            var probe = await new GrimoireCli().ProbeVersionAsync(path, CancellationToken.None);

            probe.Started.ShouldBeTrue();
            probe.Version.ShouldBe("1.4.2");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ProbeVersionAsync_UsageExitCode_StillConfirmsPresence()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var path = WriteScript("#!/bin/sh\necho \"grimoire 1.4.2\"\nexit 2\n");
        try
        {
            var probe = await new GrimoireCli().ProbeVersionAsync(path, CancellationToken.None);

            probe.Started.ShouldBeTrue();
            probe.Version.ShouldBe("1.4.2");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ProbeVersionAsync_OutputWithoutVersion_ReturnsNullVersion()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var path = WriteScript("#!/bin/sh\necho \"hello world\"\n");
        try
        {
            var probe = await new GrimoireCli().ProbeVersionAsync(path, CancellationToken.None);

            probe.Started.ShouldBeTrue();
            probe.Version.ShouldBeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ProbeVersionAsync_BinaryMissing_ReturnsNotStarted()
    {
        var path = Path.Combine(Path.GetTempPath(), $"grimoire-missing-{Guid.NewGuid():N}");

        var probe = await new GrimoireCli().ProbeVersionAsync(path, CancellationToken.None);

        probe.Started.ShouldBeFalse();
        probe.Version.ShouldBeNull();
    }

    [Fact]
    public async Task ProbeVersionAsync_TimeoutElapsed_ReturnsNotStartedFast()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var path = WriteScript("#!/bin/sh\nsleep 30\n");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var probe = await new GrimoireCli(TimeSpan.FromMilliseconds(300))
                .ProbeVersionAsync(path, CancellationToken.None);

            stopwatch.Stop();
            probe.Started.ShouldBeFalse();
            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ProbeVersionAsync_CallerCancelledBeforeCall_Throws()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var path = WriteScript("#!/bin/sh\nsleep 5\n");
        try
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Should.ThrowAsync<OperationCanceledException>(
                () => new GrimoireCli().ProbeVersionAsync(path, cts.Token));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteScript(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"grimoire-cli-{Guid.NewGuid():N}.sh");
        File.WriteAllText(path, content);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return path;
    }
}
