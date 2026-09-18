using System.Text.Json.Nodes;
using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class InstallCommandTests : IDisposable
{
    private static readonly string[] AllAgentNames = ["kimi-code", "claude", "codex", "gemini", "qwen", "antigravity"];

    private readonly string _home = Path.Combine(Path.GetTempPath(), $"overmind-install-{Guid.NewGuid():N}");
    private readonly string _pathDir = Path.Combine(Path.GetTempPath(), $"overmind-install-path-{Guid.NewGuid():N}");
    private readonly string _cwd = Path.Combine(Path.GetTempPath(), $"overmind-install-cwd-{Guid.NewGuid():N}");
    private readonly string _binary = Path.Combine(Path.GetTempPath(), $"overmind-install-bin-{Guid.NewGuid():N}", "overmind-mcp");

    public InstallCommandTests()
    {
        Directory.CreateDirectory(_home);
        Directory.CreateDirectory(_pathDir);
        Directory.CreateDirectory(_cwd);
    }

    public void Dispose()
    {
        Directory.Delete(_home, recursive: true);
        Directory.Delete(_pathDir, recursive: true);
        Directory.Delete(_cwd, recursive: true);
    }

    private AgentPathEnvironment Env(IReadOnlyDictionary<string, string>? extra = null)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["PATH"] = _pathDir };
        if (extra is not null)
        {
            foreach (var pair in extra)
            {
                variables[pair.Key] = pair.Value;
            }
        }

        return new AgentPathEnvironment { HomeDirectory = _home, Variables = variables };
    }

    private async Task<(int Exit, string Out, string Err)> RunAsync(
        InstallOptions options, AgentPathEnvironment? environment = null, string? workingDirectory = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var command = new InstallCommand(environment ?? Env(), workingDirectory ?? _cwd, _binary, output, error);
        var exit = await command.RunAsync(options, CancellationToken.None);
        return (exit, output.ToString(), error.ToString());
    }

    private static string[] Lines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [Fact]
    public async Task DryRunAll_SixWouldAddLines_NothingWritten()
    {
        var (exit, output, error) = await RunAsync(new InstallOptions { All = true, DryRun = true });

        exit.ShouldBe(0);
        error.ShouldBeEmpty();
        var lines = Lines(output);
        lines.ShouldContain("dry run: no config files are written.");
        foreach (var name in AllAgentNames)
        {
            lines.ShouldContain(line => line.StartsWith($"{name}: would add — ", StringComparison.Ordinal));
        }

        lines.ShouldContain(line => line.StartsWith("summary: 6 processed, 6 added,", StringComparison.Ordinal));
        Directory.GetFiles(_home, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task DefaultSelection_DetectedAgentsInstalled_OthersSkipped()
    {
        Directory.CreateDirectory(Path.Combine(_home, ".claude"));
        File.WriteAllText(Path.Combine(_pathDir, "gemini"), "#!/bin/sh\n");

        var (exit, output, _) = await RunAsync(new InstallOptions());

        exit.ShouldBe(0);
        var lines = Lines(output);
        lines.ShouldContain(line => line.StartsWith("claude: added — ", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("gemini: added — ", StringComparison.Ordinal));
        foreach (var name in new[] { "kimi-code", "codex", "qwen", "antigravity" })
        {
            lines.ShouldContain(line => line.StartsWith($"{name}: skipped (not detected) — ", StringComparison.Ordinal));
        }

        File.Exists(Path.Combine(_home, ".claude.json")).ShouldBeTrue();
        File.Exists(Path.Combine(_home, ".gemini", "settings.json")).ShouldBeTrue();
    }

    [Fact]
    public async Task SecondRun_AllUnchanged()
    {
        await RunAsync(new InstallOptions { All = true });
        var (exit, output, _) = await RunAsync(new InstallOptions { All = true });

        exit.ShouldBe(0);
        var lines = Lines(output);
        foreach (var name in AllAgentNames)
        {
            lines.ShouldContain(line => line.StartsWith($"{name}: unchanged — ", StringComparison.Ordinal));
        }

        lines.ShouldContain("summary: 6 processed, 0 added, 0 updated, 6 unchanged, 0 removed, 0 not present, 0 skipped, 0 failed");
    }

    [Fact]
    public async Task DryRunAfterRealRun_AlreadyRegisteredReportedUnchanged()
    {
        await RunAsync(new InstallOptions { All = true });
        var (exit, output, _) = await RunAsync(new InstallOptions { All = true, DryRun = true });

        exit.ShouldBe(0);
        output.ShouldNotContain("would add");
        Lines(output).Count(line => line.EndsWith(": unchanged — " + Path.Combine(_home, ".kimi-code", "mcp.json"), StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task AgentsSubset_OnlySelectedAgentsWritten()
    {
        var (exit, output, _) = await RunAsync(new InstallOptions { Agents = ["kimi-code", "codex"] });

        exit.ShouldBe(0);
        var lines = Lines(output);
        lines.ShouldContain(line => line.StartsWith("kimi-code: added — ", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("codex: added — ", StringComparison.Ordinal));
        output.ShouldNotContain("claude:");
        lines.ShouldContain("summary: 2 processed, 2 added, 0 updated, 0 unchanged, 0 removed, 0 not present, 0 skipped, 0 failed");

        var kimiConfig = JsonNode.Parse(File.ReadAllText(Path.Combine(_home, ".kimi-code", "mcp.json")))!;
        kimiConfig["mcpServers"]!["overmind"]!["command"]!.GetValue<string>()
            .ShouldBe(Path.GetFullPath(_binary));

        var codexConfig = File.ReadAllText(Path.Combine(_home, ".codex", "config.toml"));
        codexConfig.ShouldContain("[mcp_servers.overmind]");
        codexConfig.ShouldContain($"command = \"{Path.GetFullPath(_binary)}\"");
    }

    [Fact]
    public async Task Remove_OnlyOvermindKeyRemoved_OtherKeysPreserved()
    {
        var claudeConfigPath = Path.Combine(_home, ".claude.json");
        File.WriteAllText(claudeConfigPath,
            """{ "theme": "dark", "mcpServers": { "filesystem": { "command": "npx" }, "overmind": { "command": "/old/overmind-mcp" } }, "permissions": { "allow": [] } }""");

        var (exit, output, _) = await RunAsync(new InstallOptions { Agents = ["claude"], Remove = true });

        exit.ShouldBe(0);
        Lines(output).ShouldContain(line => line.StartsWith("claude: removed — ", StringComparison.Ordinal));

        var root = JsonNode.Parse(File.ReadAllText(claudeConfigPath))!;
        root["mcpServers"]!["overmind"].ShouldBeNull();
        root["mcpServers"]!["filesystem"]!["command"]!.GetValue<string>().ShouldBe("npx");
        root["theme"]!.GetValue<string>().ShouldBe("dark");

        var backup = Directory.GetFiles(_home, "*.bak").ShouldHaveSingleItem();
        File.ReadAllText(backup).ShouldContain("/old/overmind-mcp");
    }

    [Fact]
    public async Task RemoveNotPresent_ReportsNotPresent_Exit0()
    {
        var (exit, output, _) = await RunAsync(new InstallOptions { All = true, Remove = true });

        exit.ShouldBe(0);
        var lines = Lines(output);
        foreach (var name in AllAgentNames)
        {
            lines.ShouldContain(line => line.StartsWith($"{name}: not present — ", StringComparison.Ordinal));
        }

        lines.ShouldContain("summary: 6 processed, 0 added, 0 updated, 0 unchanged, 0 removed, 6 not present, 0 skipped, 0 failed");
        Directory.GetFiles(_home, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task DryRunRemove_SaysWouldRemove_FileUnchanged()
    {
        var claudeConfigPath = Path.Combine(_home, ".claude.json");
        File.WriteAllText(claudeConfigPath, """{ "mcpServers": { "overmind": { "command": "/old/overmind-mcp" } } }""");

        var (exit, output, _) = await RunAsync(new InstallOptions { Agents = ["claude"], Remove = true, DryRun = true });

        exit.ShouldBe(0);
        Lines(output).ShouldContain(line => line.StartsWith("claude: would remove — ", StringComparison.Ordinal));
        File.ReadAllText(claudeConfigPath).ShouldContain("/old/overmind-mcp");
    }

    [Fact]
    public async Task ProjectScope_WritesUnderGitRoot_NotUnderHome()
    {
        Directory.CreateDirectory(Path.Combine(_cwd, ".git"));

        var (exit, output, _) = await RunAsync(new InstallOptions { All = true, Project = true });

        exit.ShouldBe(0);
        var lines = Lines(output);
        lines.ShouldContain(line => line.StartsWith("kimi-code: added — " + _cwd, StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("claude: unchanged — " + Path.Combine(_cwd, ".mcp.json"), StringComparison.Ordinal));

        JsonNode.Parse(File.ReadAllText(Path.Combine(_cwd, ".mcp.json")))!["mcpServers"]!["overmind"].ShouldNotBeNull();
        File.Exists(Path.Combine(_cwd, ".codex", "config.toml")).ShouldBeTrue();
        File.Exists(Path.Combine(_cwd, ".gemini", "settings.json")).ShouldBeTrue();
        File.Exists(Path.Combine(_cwd, ".qwen", "settings.json")).ShouldBeTrue();
        File.Exists(Path.Combine(_cwd, ".agents", "mcp_config.json")).ShouldBeTrue();
        Directory.GetFiles(_home, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task ProjectScopeWithoutGitRoot_ClearErrorExit1()
    {
        var (exit, output, error) = await RunAsync(new InstallOptions { All = true, Project = true });

        exit.ShouldBe(1);
        output.ShouldBeEmpty();
        error.ShouldContain("--project requires a git repository");
        Directory.GetFiles(_home, "*", SearchOption.AllDirectories).ShouldBeEmpty();
        Directory.GetFiles(_cwd, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task InvalidExistingConfigs_FailuresReported_OthersProceed()
    {
        File.WriteAllText(Path.Combine(_home, ".claude.json"), "not json {");
        Directory.CreateDirectory(Path.Combine(_home, ".codex"));
        File.WriteAllText(Path.Combine(_home, ".codex", "config.toml"), "[[");

        var (exit, output, _) = await RunAsync(new InstallOptions { Agents = ["claude", "codex", "gemini"] });

        exit.ShouldBe(1);
        var lines = Lines(output);
        lines.ShouldContain(line => line.StartsWith("claude: failed — ", StringComparison.Ordinal) && line.Contains("Invalid JSON", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("codex: failed — ", StringComparison.Ordinal) && line.Contains("Invalid TOML", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("gemini: added — ", StringComparison.Ordinal));
        lines.ShouldContain("summary: 3 processed, 1 added, 0 updated, 0 unchanged, 0 removed, 0 not present, 0 skipped, 2 failed");
    }

    [Fact]
    public async Task EngineIoError_AgentFailed_OthersProceed_Exit1()
    {
        var claudeConfigPath = Path.Combine(_home, ".claude.json");
        Directory.CreateDirectory(claudeConfigPath);

        var (exit, output, _) = await RunAsync(new InstallOptions { Agents = ["claude", "gemini"] });

        exit.ShouldBe(1);
        var lines = Lines(output);
        var claudeLine = lines.Single(line => line.StartsWith("claude: failed — ", StringComparison.Ordinal));
        claudeLine.ShouldContain(claudeConfigPath);
        claudeLine.ShouldEndWith(")");
        lines.ShouldContain(line => line.StartsWith("gemini: added — ", StringComparison.Ordinal));
        lines.ShouldContain("summary: 2 processed, 1 added, 0 updated, 0 unchanged, 0 removed, 0 not present, 0 skipped, 1 failed");
    }

    [Fact]
    public async Task EnvOverrideHome_RespectedForUserScope()
    {
        var overrideDir = Path.Combine(Path.GetTempPath(), $"overmind-install-override-{Guid.NewGuid():N}");
        Directory.CreateDirectory(overrideDir);

        try
        {
            var environment = Env(new Dictionary<string, string> { ["KIMI_CODE_HOME"] = overrideDir });
            var (exit, output, _) = await RunAsync(new InstallOptions { Agents = ["kimi-code"] }, environment);

            exit.ShouldBe(0);
            Lines(output).ShouldContain(line => line.StartsWith($"kimi-code: added — {overrideDir}", StringComparison.Ordinal));
            File.Exists(Path.Combine(overrideDir, "mcp.json")).ShouldBeTrue();
            File.Exists(Path.Combine(_home, ".kimi-code", "mcp.json")).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(overrideDir, recursive: true);
        }
    }
}
