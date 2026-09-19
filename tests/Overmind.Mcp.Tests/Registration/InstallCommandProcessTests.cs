using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class InstallCommandProcessTests : IDisposable
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(60);

    private readonly string _home = Path.Combine(Path.GetTempPath(), $"overmind-install-proc-{Guid.NewGuid():N}");

    public InstallCommandProcessTests()
    {
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public async Task InstallDryRun_HostDown_ExitsZeroAndPrintsPlan()
    {
        var (exit, output, error) = await RunInstallAsync("install --dry-run --all");

        exit.ShouldBe(0);
        error.ShouldBeEmpty();
        output.ShouldContain("dry run: no config files are written.");
        output.ShouldContain("kimi-code: would add — ");
        output.ShouldContain("claude: would add — ");
        output.ShouldContain("codex: would add — ");
        output.ShouldContain("summary: 6 processed, 6 added,");
        error.ShouldNotContain("unreachable");
        Directory.GetFiles(_home, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task InstallUnknownFlag_ExitsTwoWithUsageOnStderr()
    {
        var (exit, output, error) = await RunInstallAsync("install --bogus");

        exit.ShouldBe(2);
        output.ShouldBeEmpty();
        error.ShouldContain("--bogus");
        error.ShouldContain("usage: overmind-mcp install");
    }

    [Fact]
    public async Task InstallOnboardWithRemove_ExitsTwoWithUsageOnStderr()
    {
        var (exit, output, error) = await RunInstallAsync("install --onboard --remove");

        exit.ShouldBe(2);
        output.ShouldBeEmpty();
        error.ShouldContain("--onboard");
        error.ShouldContain("--remove");
        error.ShouldContain("usage: overmind-mcp install");
    }

    private async Task<(int Exit, string Out, string Err)> RunInstallAsync(string arguments)
    {
        var dllPath = typeof(Overmind.Mcp.Registration.InstallCommand).Assembly.Location;

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = _home,
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(dllPath);
        foreach (var token in arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            startInfo.ArgumentList.Add(token);
        }

        startInfo.EnvironmentVariables["HOME"] = _home;
        startInfo.EnvironmentVariables["OVERMIND_API_URL"] = "http://127.0.0.1:1";

        using var process = Process.Start(startInfo)!;
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(ProcessTimeout);

        var stdout = await stdoutTask.WaitAsync(ProcessTimeout);
        var stderr = await stderrTask.WaitAsync(ProcessTimeout);
        return (process.ExitCode, stdout, stderr);
    }
}
