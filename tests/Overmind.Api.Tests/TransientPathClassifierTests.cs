using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

[Collection(nameof(ProcessEnvironment))]
public sealed class TransientPathClassifierTests
{
    [Theory]
    [InlineData("/tmp")]
    [InlineData("/tmp/overmind-install-grim-cwd-9f2a")]
    [InlineData("/tmp/build/session-1/work")]
    [InlineData("/var/tmp")]
    [InlineData("/var/tmp/systemd-private-1")]
    [InlineData("/run")]
    [InlineData("/run/user/1000/wayland-0")]
    public void IsTransient_SystemTempRoots_ReturnsTrue(string path) =>
        TransientPathClassifier.IsTransient(path).ShouldBeTrue();

    [Theory]
    [InlineData("/home/dev/projects/overmind")]
    [InlineData("/opt/work/app")]
    [InlineData("/srv/builds/nightly")]
    [InlineData("/tmpfoo")]
    [InlineData("/var/tmpextra")]
    [InlineData("/runcode")]
    public void IsTransient_OrdinaryPaths_ReturnsFalse(string path) =>
        TransientPathClassifier.IsTransient(path).ShouldBeFalse();

    [Fact]
    public void IsTransient_PathUnderConfiguredTempPath_ReturnsTrue() =>
        TransientPathClassifier.IsTransient(Path.Combine(Path.GetTempPath(), "scratch", "deep")).ShouldBeTrue();

    [Fact]
    public void IsTransient_RootedTmpDirEnv_ReturnsTrue()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var previous = Environment.GetEnvironmentVariable("TMPDIR");
        Environment.SetEnvironmentVariable("TMPDIR", "/var/opt/custom-tmp");
        try
        {
            TransientPathClassifier.IsTransient("/var/opt/custom-tmp").ShouldBeTrue();
            TransientPathClassifier.IsTransient("/var/opt/custom-tmp/workspace").ShouldBeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("TMPDIR", previous);
        }
    }

    [Fact]
    public void IsTransient_RelativeTmpDirEnv_Ignored()
    {
        var previous = Environment.GetEnvironmentVariable("TMPDIR");
        Environment.SetEnvironmentVariable("TMPDIR", "relative/tmp");
        try
        {
            TransientPathClassifier.IsTransient("/srv/relative/tmp/project").ShouldBeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("TMPDIR", previous);
        }
    }
}
