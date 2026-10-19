using System.Text.Json.Nodes;
using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class InstallCommandGrimoireTests : IDisposable
{
    private const string BinaryPath = "/opt/overmind/bin/overmind-mcp";

    private const string JsonFixture = """
        {
          "theme": "dark",
          "mcpServers": {
            "filesystem": {
              "command": "npx"
            }
          }
        }
        """;

    private const string JsonFixtureWithOvermindAndGrimoire = """
        {
          "theme": "dark",
          "mcpServers": {
            "filesystem": {
              "command": "npx"
            },
            "overmind": {
              "command": "/opt/overmind/bin/overmind-mcp"
            },
            "grimoire": {
              "command": "grimoire",
              "args": ["mcp"]
            }
          }
        }
        """;

    private const string JsonFixtureWithOvermindOnly = """
        {
          "theme": "dark",
          "mcpServers": {
            "filesystem": {
              "command": "npx"
            },
            "overmind": {
              "command": "/opt/overmind/bin/overmind-mcp"
            }
          }
        }
        """;

    private readonly string _home = Path.Combine(Path.GetTempPath(), $"overmind-install-grim-{Guid.NewGuid():N}");
    private readonly string _pathDir = Path.Combine(Path.GetTempPath(), $"overmind-install-grim-path-{Guid.NewGuid():N}");
    private readonly string _cwd = Path.Combine(Path.GetTempPath(), $"overmind-install-grim-cwd-{Guid.NewGuid():N}");

    public InstallCommandGrimoireTests()
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

    private string ClaudeConfigPath => Path.Combine(_home, ".claude.json");

    private AgentPathEnvironment Env()
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["PATH"] = _pathDir };
        return new AgentPathEnvironment { HomeDirectory = _home, Variables = variables };
    }

    private async Task<(int Exit, string Out, string Err)> RunAsync(InstallOptions options)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var command = new InstallCommand(Env(), _cwd, BinaryPath, output, error);
        var exit = await command.RunAsync(options, CancellationToken.None);
        return (exit, output.ToString(), error.ToString());
    }

    private static string[] Lines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [Fact]
    public async Task WithGrimoire_WritesBothManagedKeysForSelectedAgents()
    {
        var (exit, output, error) = await RunAsync(new InstallOptions { Agents = ["claude", "codex"], WithGrimoire = true });

        exit.ShouldBe(0);
        error.ShouldBeEmpty();

        var claudeRoot = JsonNode.Parse(File.ReadAllText(ClaudeConfigPath))!;
        claudeRoot["mcpServers"]!["overmind"]!["command"]!.GetValue<string>().ShouldBe(BinaryPath);
        claudeRoot["mcpServers"]!["grimoire"]!["command"]!.GetValue<string>().ShouldBe("grimoire");
        var grimoireArgs = ((JsonArray)claudeRoot["mcpServers"]!["grimoire"]!["args"]!)
            .Select(node => node!.GetValue<string>())
            .ToList();
        grimoireArgs.ShouldBe(["mcp"]);

        var codexConfig = File.ReadAllText(Path.Combine(_home, ".codex", "config.toml"));
        codexConfig.ShouldContain("[mcp_servers.overmind]");
        codexConfig.ShouldContain("[mcp_servers.grimoire]");
        codexConfig.ShouldContain("args = [\"mcp\"]");

        var lines = Lines(output);
        lines.ShouldContain(line => line.StartsWith("claude: added — ", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("claude: grimoire added — ", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("codex: added — ", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("codex: grimoire added — ", StringComparison.Ordinal));
        lines.ShouldContain("summary: 2 processed, 2 added, 0 updated, 0 unchanged, 0 removed, 0 not present, 0 skipped, 0 failed" +
                            "; grimoire: 2 added, 0 updated, 0 unchanged, 0 removed, 0 not present, 0 failed");
    }

    [Fact]
    public async Task WithGrimoire_ReRun_BothKeysUnchanged_NoNewBackups()
    {
        await File.WriteAllTextAsync(ClaudeConfigPath, JsonFixture);
        Directory.CreateDirectory(Path.Combine(_home, ".codex"));
        await File.WriteAllTextAsync(Path.Combine(_home, ".codex", "config.toml"), "model = \"gpt-5-codex\"\n");

        await RunAsync(new InstallOptions { Agents = ["claude", "codex"], WithGrimoire = true });
        var claudeAfterFirst = File.ReadAllText(ClaudeConfigPath);
        var codexAfterFirst = File.ReadAllText(Path.Combine(_home, ".codex", "config.toml"));

        var (exit, output, _) = await RunAsync(new InstallOptions { Agents = ["claude", "codex"], WithGrimoire = true });

        exit.ShouldBe(0);
        File.ReadAllText(ClaudeConfigPath).ShouldBe(claudeAfterFirst);
        File.ReadAllText(Path.Combine(_home, ".codex", "config.toml")).ShouldBe(codexAfterFirst);

        var lines = Lines(output);
        lines.ShouldContain(line => line.StartsWith("claude: unchanged — ", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("claude: grimoire unchanged — ", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("codex: grimoire unchanged — ", StringComparison.Ordinal));
        lines.ShouldContain("summary: 2 processed, 0 added, 0 updated, 2 unchanged, 0 removed, 0 not present, 0 skipped, 0 failed" +
                            "; grimoire: 0 added, 0 updated, 2 unchanged, 0 removed, 0 not present, 0 failed");
        Directory.GetFiles(_home, "*.bak", SearchOption.AllDirectories).Length.ShouldBe(2);
    }

    [Fact]
    public async Task WithGrimoire_Mutation_CreatesBackupWithOriginalContent()
    {
        File.WriteAllText(ClaudeConfigPath, JsonFixture);

        await RunAsync(new InstallOptions { Agents = ["claude"], WithGrimoire = true });

        var backup = Directory.GetFiles(_home, "*.bak", SearchOption.AllDirectories).ShouldHaveSingleItem();
        File.ReadAllText(backup).ShouldBe(JsonFixture);
    }

    [Fact]
    public async Task WithGrimoire_DryRun_PrintsGrimoirePlan_WritesNothing()
    {
        var (exit, output, error) = await RunAsync(
            new InstallOptions { Agents = ["claude", "codex"], WithGrimoire = true, DryRun = true });

        exit.ShouldBe(0);
        error.ShouldBeEmpty();
        Directory.GetFiles(_home, "*", SearchOption.AllDirectories).ShouldBeEmpty();

        var lines = Lines(output);
        lines.ShouldContain(line => line.StartsWith("claude: would add — ", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("claude: grimoire would add — ", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("codex: grimoire would add — ", StringComparison.Ordinal));
        lines.ShouldContain("summary: 2 processed, 2 added, 0 updated, 0 unchanged, 0 removed, 0 not present, 0 skipped, 0 failed" +
                            "; grimoire: 2 added, 0 updated, 0 unchanged, 0 removed, 0 not present, 0 failed");
    }

    [Fact]
    public async Task WithGrimoire_InvalidConfig_FailsBothOperations_OthersProceed_Exit1()
    {
        File.WriteAllText(ClaudeConfigPath, "not json {");
        Directory.CreateDirectory(Path.Combine(_home, ".codex"));

        var (exit, output, _) = await RunAsync(new InstallOptions { Agents = ["claude", "codex"], WithGrimoire = true });

        exit.ShouldBe(1);
        var lines = Lines(output);
        lines.ShouldContain(line => line.StartsWith("claude: failed — ", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("claude: grimoire failed — ", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("codex: added — ", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("codex: grimoire added — ", StringComparison.Ordinal));
        lines.ShouldContain("summary: 2 processed, 1 added, 0 updated, 0 unchanged, 0 removed, 0 not present, 0 skipped, 1 failed" +
                            "; grimoire: 1 added, 0 updated, 0 unchanged, 0 removed, 0 not present, 1 failed");
    }

    [Fact]
    public async Task WithoutGrimoireFlags_NoGrimoireLinesOrSummarySection()
    {
        var (exit, output, _) = await RunAsync(new InstallOptions { Agents = ["claude"] });

        exit.ShouldBe(0);
        output.ShouldNotContain("grimoire");
        File.ReadAllText(ClaudeConfigPath).ShouldNotContain("\"grimoire\"");
    }

    [Fact]
    public async Task RemoveGrimoire_RemovesOnlyGrimoireKey_OvermindAndOthersUntouched()
    {
        File.WriteAllText(ClaudeConfigPath, JsonFixtureWithOvermindAndGrimoire);

        var (exit, output, _) = await RunAsync(new InstallOptions { Agents = ["claude"], RemoveGrimoire = true });

        exit.ShouldBe(0);

        var root = JsonNode.Parse(File.ReadAllText(ClaudeConfigPath))!;
        root["mcpServers"]!["grimoire"].ShouldBeNull();
        root["mcpServers"]!["overmind"]!["command"]!.GetValue<string>().ShouldBe(BinaryPath);
        root["mcpServers"]!["filesystem"]!["command"]!.GetValue<string>().ShouldBe("npx");
        root["theme"]!.GetValue<string>().ShouldBe("dark");

        var backup = Directory.GetFiles(_home, "*.bak", SearchOption.AllDirectories).ShouldHaveSingleItem();
        File.ReadAllText(backup).ShouldBe(JsonFixtureWithOvermindAndGrimoire);

        var lines = Lines(output);
        lines.Length.ShouldBe(2);
        lines[0].ShouldStartWith("claude: grimoire removed — ");
        lines[1].ShouldBe("summary: 1 processed, 0 added, 0 updated, 0 unchanged, 0 removed, 0 not present, 0 skipped, 0 failed" +
                          "; grimoire: 0 added, 0 updated, 0 unchanged, 1 removed, 0 not present, 0 failed");
    }

    [Fact]
    public async Task RemoveGrimoire_GrimoireAbsent_LeavesFileAndOvermindUntouched()
    {
        File.WriteAllText(ClaudeConfigPath, JsonFixtureWithOvermindOnly);
        var before = File.ReadAllText(ClaudeConfigPath);

        var (exit, output, _) = await RunAsync(new InstallOptions { Agents = ["claude"], RemoveGrimoire = true });

        exit.ShouldBe(0);
        File.ReadAllText(ClaudeConfigPath).ShouldBe(before);
        Directory.GetFiles(_home, "*.bak", SearchOption.AllDirectories).ShouldBeEmpty();

        var lines = Lines(output);
        lines.Length.ShouldBe(2);
        lines[0].ShouldStartWith("claude: grimoire not present — ");
        lines[1].ShouldBe("summary: 1 processed, 0 added, 0 updated, 0 unchanged, 0 removed, 0 not present, 0 skipped, 0 failed" +
                          "; grimoire: 0 added, 0 updated, 0 unchanged, 0 removed, 1 not present, 0 failed");
    }

    [Fact]
    public async Task RemoveGrimoire_DryRun_SaysWouldRemove_WritesNothing()
    {
        File.WriteAllText(ClaudeConfigPath, JsonFixtureWithOvermindAndGrimoire);
        var before = File.ReadAllText(ClaudeConfigPath);

        var (exit, output, _) = await RunAsync(
            new InstallOptions { Agents = ["claude"], RemoveGrimoire = true, DryRun = true });

        exit.ShouldBe(0);
        File.ReadAllText(ClaudeConfigPath).ShouldBe(before);
        Directory.GetFiles(_home, "*.bak", SearchOption.AllDirectories).ShouldBeEmpty();
        Lines(output).ShouldContain(line => line.StartsWith("claude: grimoire would remove — ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WithGrimoireAndOnboard_WritesKeysAndBlockWithBoundaryParagraph()
    {
        Directory.CreateDirectory(Path.Combine(_cwd, ".git"));
        Directory.CreateDirectory(Path.Combine(_home, ".claude"));

        var (exit, output, _) = await RunAsync(new InstallOptions { Agents = ["claude"], WithGrimoire = true, Onboard = true });

        exit.ShouldBe(0);

        var root = JsonNode.Parse(File.ReadAllText(ClaudeConfigPath))!;
        root["mcpServers"]!["overmind"].ShouldNotBeNull();
        root["mcpServers"]!["grimoire"].ShouldNotBeNull();

        var block = await File.ReadAllTextAsync(Path.Combine(_cwd, "CLAUDE.md"));
        block.ShouldContain(OnboardBlockContentTests.DomainBoundarySentence);
        Lines(output).ShouldContain(line => line.StartsWith("claude: onboard block created — ", StringComparison.Ordinal));
    }
}
