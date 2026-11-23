using System.Text.Json.Nodes;
using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class InstallCommandJournalTests : IDisposable
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

    private const string JsonFixtureWithPlanningAndJournal = """
        {
          "theme": "dark",
          "mcpServers": {
            "filesystem": {
              "command": "npx"
            },
            "overmind": {
              "command": "/opt/overmind/bin/overmind-mcp"
            },
            "overmind-journal": {
              "command": "/opt/overmind/bin/overmind-mcp",
              "env": { "OVERMIND_MCP_PROFILE": "journal" }
            }
          }
        }
        """;

    private readonly string _home = Path.Combine(Path.GetTempPath(), $"overmind-install-journal-{Guid.NewGuid():N}");
    private readonly string _pathDir = Path.Combine(Path.GetTempPath(), $"overmind-install-journal-path-{Guid.NewGuid():N}");
    private readonly string _cwd = Path.Combine(Path.GetTempPath(), $"overmind-install-journal-cwd-{Guid.NewGuid():N}");

    public InstallCommandJournalTests()
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

    private string CodexConfigPath => Path.Combine(_home, ".codex", "config.toml");

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
    public async Task JournalProfile_Claude_WritesJournalEntryWithPinnedEnv_NoPlanningEntry()
    {
        var (exit, output, error) = await RunAsync(new InstallOptions { Agents = ["claude"], Profile = McpProfile.Journal });

        exit.ShouldBe(0);
        error.ShouldBeEmpty();

        var servers = JsonNode.Parse(File.ReadAllText(ClaudeConfigPath))!["mcpServers"]!;
        var journal = servers["overmind-journal"]!;
        journal["command"]!.GetValue<string>().ShouldBe(BinaryPath);
        journal["env"]!["OVERMIND_MCP_PROFILE"]!.GetValue<string>().ShouldBe("journal");
        journal["env"].AsObject().Count.ShouldBe(1);
        servers["overmind"].ShouldBeNull();

        Lines(output).ShouldContain(line => line.StartsWith("claude: overmind-journal added — ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task JournalProfile_Codex_WritesTomlJournalEntryWithPinnedEnv()
    {
        var (exit, _, _) = await RunAsync(new InstallOptions { Agents = ["codex"], Profile = McpProfile.Journal });

        exit.ShouldBe(0);
        var config = File.ReadAllText(CodexConfigPath);
        config.ShouldContain("[mcp_servers.overmind-journal]");
        config.ShouldContain("OVERMIND_MCP_PROFILE = \"journal\"");
        config.ShouldContain($"command = \"{BinaryPath}\"");
        config.ShouldNotContain("[mcp_servers.overmind]");
    }

    [Fact]
    public async Task JournalProfile_ReRun_UnchangedAndByteIdentical()
    {
        Directory.CreateDirectory(Path.Combine(_home, ".codex"));
        await File.WriteAllTextAsync(ClaudeConfigPath, JsonFixture);
        await File.WriteAllTextAsync(CodexConfigPath, "model = \"gpt-5-codex\"\n");

        await RunAsync(new InstallOptions { Agents = ["claude", "codex"], Profile = McpProfile.Journal });
        var claudeAfterFirst = File.ReadAllText(ClaudeConfigPath);
        var codexAfterFirst = File.ReadAllText(CodexConfigPath);

        var (exit, output, _) = await RunAsync(new InstallOptions { Agents = ["claude", "codex"], Profile = McpProfile.Journal });

        exit.ShouldBe(0);
        File.ReadAllText(ClaudeConfigPath).ShouldBe(claudeAfterFirst);
        File.ReadAllText(CodexConfigPath).ShouldBe(codexAfterFirst);

        var lines = Lines(output);
        lines.ShouldContain(line => line.StartsWith("claude: overmind-journal unchanged — ", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("codex: overmind-journal unchanged — ", StringComparison.Ordinal));
        Directory.GetFiles(_home, "*.bak", SearchOption.AllDirectories).Length.ShouldBe(2);
    }

    [Fact]
    public async Task PlanningDefault_WritesPlanningEntryOnly_NoJournalSurface()
    {
        var (exit, output, _) = await RunAsync(new InstallOptions { Agents = ["claude", "codex"] });

        exit.ShouldBe(0);
        var servers = JsonNode.Parse(File.ReadAllText(ClaudeConfigPath))!["mcpServers"]!;
        servers["overmind"]!["command"]!.GetValue<string>().ShouldBe(BinaryPath);
        servers["overmind-journal"].ShouldBeNull();

        var config = File.ReadAllText(ClaudeConfigPath);
        config.ShouldNotContain("OVERMIND_MCP_PROFILE");

        File.ReadAllText(CodexConfigPath).ShouldContain("[mcp_servers.overmind]");
        Lines(output).ShouldContain(line => line.StartsWith("claude: added — ", StringComparison.Ordinal));
        Lines(output).ShouldNotContain(line => line.Contains("overmind-journal", StringComparison.Ordinal));
    }

    [Fact]
    public async Task JournalProfileRemove_RemovesJournalEntryOnly_PlanningEntryAndOthersStay()
    {
        File.WriteAllText(ClaudeConfigPath, JsonFixtureWithPlanningAndJournal);

        var (exit, output, _) = await RunAsync(
            new InstallOptions { Agents = ["claude"], Profile = McpProfile.Journal, Remove = true });

        exit.ShouldBe(0);
        var servers = JsonNode.Parse(File.ReadAllText(ClaudeConfigPath))!["mcpServers"]!;
        servers["overmind-journal"].ShouldBeNull();
        servers["overmind"]!["command"]!.GetValue<string>().ShouldBe(BinaryPath);
        servers["filesystem"]!["command"]!.GetValue<string>().ShouldBe("npx");

        var backup = Directory.GetFiles(_home, "*.bak", SearchOption.AllDirectories).ShouldHaveSingleItem();
        File.ReadAllText(backup).ShouldBe(JsonFixtureWithPlanningAndJournal);
        Lines(output).ShouldContain(line => line.StartsWith("claude: overmind-journal removed — ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningDefaultRemove_LeavesJournalEntryUntouched()
    {
        File.WriteAllText(ClaudeConfigPath, JsonFixtureWithPlanningAndJournal);

        var (exit, output, _) = await RunAsync(new InstallOptions { Agents = ["claude"], Remove = true });

        exit.ShouldBe(0);
        var servers = JsonNode.Parse(File.ReadAllText(ClaudeConfigPath))!["mcpServers"]!;
        servers["overmind"].ShouldBeNull();
        servers["overmind-journal"]!["env"]!["OVERMIND_MCP_PROFILE"]!.GetValue<string>().ShouldBe("journal");
        servers["filesystem"].ShouldNotBeNull();
        Lines(output).ShouldContain(line => line.StartsWith("claude: removed — ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task JournalProfile_DryRun_WritesNothing()
    {
        var (exit, output, error) = await RunAsync(
            new InstallOptions { Agents = ["claude"], Profile = McpProfile.Journal, DryRun = true });

        exit.ShouldBe(0);
        error.ShouldBeEmpty();
        Directory.GetFiles(_home, "*", SearchOption.AllDirectories).ShouldBeEmpty();
        Lines(output).ShouldContain(line => line.StartsWith("claude: overmind-journal would add — ", StringComparison.Ordinal));
    }
}
