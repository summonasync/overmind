using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class InstallCommandZeroWireTests : IDisposable
{
    private const string BinaryPath = "/opt/overmind/bin/overmind-mcp";

    private const string JsonFixture = """
        {
          "theme": "dark",
          "mcpServers": {
            "filesystem": {
              "command": "npx",
              "args": ["-y", "@modelcontextprotocol/server-filesystem", "/tmp"]
            }
          },
          "projects": {
            "/home/user/Projects/demo": {
              "allowedTools": ["Bash", "Edit"]
            }
          }
        }
        """;

    private const string TomlFixture = """
        # user preferences — must survive
        model = "gpt-5-codex"

        [mcp_servers.context7]
        command = "npx"
        args = ["-y", "@upstash/context7-mcp"]

        [sandbox]
        writable_roots = ["/home/user/tmp"]
        """;

    private const string PinnedJsonAfterInstall = """
        {
          "theme": "dark",
          "mcpServers": {
            "filesystem": {
              "command": "npx",
              "args": [
                "-y",
                "@modelcontextprotocol/server-filesystem",
                "/tmp"
              ]
            },
            "overmind": {
              "command": "/opt/overmind/bin/overmind-mcp"
            }
          },
          "projects": {
            "/home/user/Projects/demo": {
              "allowedTools": [
                "Bash",
                "Edit"
              ]
            }
          }
        }
        """;

    private const string PinnedTomlAfterInstall = """
        # user preferences — must survive
        model = "gpt-5-codex"

        [mcp_servers]
        [mcp_servers.context7]
        command = "npx"
        args = ["-y", "@upstash/context7-mcp"]

        [mcp_servers.overmind]
        command = "/opt/overmind/bin/overmind-mcp"
        [sandbox]
        writable_roots = ["/home/user/tmp"]
        """;

    private const string PinnedJsonAfterRemove = """
        {
          "theme": "dark",
          "mcpServers": {
            "filesystem": {
              "command": "npx",
              "args": [
                "-y",
                "@modelcontextprotocol/server-filesystem",
                "/tmp"
              ]
            }
          },
          "projects": {
            "/home/user/Projects/demo": {
              "allowedTools": [
                "Bash",
                "Edit"
              ]
            }
          }
        }
        """;

    private const string PinnedStdoutInstall = """
        claude: added — {HOME}/.claude.json
        codex: added — {HOME}/.codex/config.toml
        summary: 2 processed, 2 added, 0 updated, 0 unchanged, 0 removed, 0 not present, 0 skipped, 0 failed
        """;

    private const string PinnedStdoutRerun = """
        claude: unchanged — {HOME}/.claude.json
        codex: unchanged — {HOME}/.codex/config.toml
        summary: 2 processed, 0 added, 0 updated, 2 unchanged, 0 removed, 0 not present, 0 skipped, 0 failed
        """;

    private const string PinnedStdoutDryRun = """
        dry run: no config files are written.
        claude: unchanged — {HOME}/.claude.json
        codex: unchanged — {HOME}/.codex/config.toml
        summary: 2 processed, 0 added, 0 updated, 2 unchanged, 0 removed, 0 not present, 0 skipped, 0 failed
        """;

    private const string PinnedStdoutRemove = """
        claude: removed — {HOME}/.claude.json
        summary: 1 processed, 0 added, 0 updated, 0 unchanged, 1 removed, 0 not present, 0 skipped, 0 failed
        """;

    private readonly string _home = Path.Combine(Path.GetTempPath(), $"overmind-zerowire-{Guid.NewGuid():N}");
    private readonly string _pathDir = Path.Combine(Path.GetTempPath(), $"overmind-zerowire-path-{Guid.NewGuid():N}");
    private readonly string _cwd = Path.Combine(Path.GetTempPath(), $"overmind-zerowire-cwd-{Guid.NewGuid():N}");

    public InstallCommandZeroWireTests()
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

    private async Task<(int Exit, string Out)> RunAsync(InstallOptions options)
    {
        var output = new StringWriter();
        var command = new InstallCommand(Env(), _cwd, BinaryPath, output, TextWriter.Null);
        var exit = await command.RunAsync(options, CancellationToken.None);
        return (exit, output.ToString());
    }

    private string Pin(string pinnedText)
    {
        return pinnedText.Replace("{HOME}", _home, StringComparison.Ordinal);
    }

    private async Task SeedAsync()
    {
        await File.WriteAllTextAsync(ClaudeConfigPath, JsonFixture);
        Directory.CreateDirectory(Path.Combine(_home, ".codex"));
        await File.WriteAllTextAsync(CodexConfigPath, TomlFixture);
    }

    [Fact]
    public async Task Install_OvermindOnly_ConfigBytesAndStdout_MatchPinnedReference()
    {
        await SeedAsync();

        var (exit, output) = await RunAsync(new InstallOptions { Agents = ["claude", "codex"] });

        exit.ShouldBe(0);
        output.ShouldBe(Pin(PinnedStdoutInstall) + "\n");
        (await File.ReadAllTextAsync(ClaudeConfigPath)).ShouldBe(PinnedJsonAfterInstall + "\n");
        (await File.ReadAllTextAsync(CodexConfigPath)).ShouldBe(PinnedTomlAfterInstall + "\n");

        var backups = Directory.GetFiles(_home, "*.bak", SearchOption.AllDirectories).OrderBy(path => path).ToList();
        backups.Count.ShouldBe(2);
        (await File.ReadAllTextAsync(backups[0])).ShouldBe(JsonFixture);
        (await File.ReadAllTextAsync(backups[1])).ShouldBe(TomlFixture);
    }

    [Fact]
    public async Task Rerun_OvermindOnly_StdoutPinned_BytesStable()
    {
        await SeedAsync();
        await RunAsync(new InstallOptions { Agents = ["claude", "codex"] });
        var jsonAfterInstall = await File.ReadAllTextAsync(ClaudeConfigPath);
        var tomlAfterInstall = await File.ReadAllTextAsync(CodexConfigPath);

        var (exit, output) = await RunAsync(new InstallOptions { Agents = ["claude", "codex"] });

        exit.ShouldBe(0);
        output.ShouldBe(Pin(PinnedStdoutRerun) + "\n");
        (await File.ReadAllTextAsync(ClaudeConfigPath)).ShouldBe(jsonAfterInstall);
        (await File.ReadAllTextAsync(CodexConfigPath)).ShouldBe(tomlAfterInstall);
        Directory.GetFiles(_home, "*.bak", SearchOption.AllDirectories).Length.ShouldBe(2);
    }

    [Fact]
    public async Task DryRun_OvermindOnly_StdoutPinned_BytesStable()
    {
        await SeedAsync();
        await RunAsync(new InstallOptions { Agents = ["claude", "codex"] });

        var (exit, output) = await RunAsync(new InstallOptions { Agents = ["claude", "codex"], DryRun = true });

        exit.ShouldBe(0);
        output.ShouldBe(Pin(PinnedStdoutDryRun) + "\n");
        (await File.ReadAllTextAsync(ClaudeConfigPath)).ShouldBe(PinnedJsonAfterInstall + "\n");
        (await File.ReadAllTextAsync(CodexConfigPath)).ShouldBe(PinnedTomlAfterInstall + "\n");
    }

    [Fact]
    public async Task Remove_OvermindOnly_ConfigBytesAndStdout_MatchPinnedReference()
    {
        await SeedAsync();
        await RunAsync(new InstallOptions { Agents = ["claude", "codex"] });

        var (exit, output) = await RunAsync(new InstallOptions { Agents = ["claude"], Remove = true });

        exit.ShouldBe(0);
        output.ShouldBe(Pin(PinnedStdoutRemove) + "\n");
        (await File.ReadAllTextAsync(ClaudeConfigPath)).ShouldBe(PinnedJsonAfterRemove + "\n");
        (await File.ReadAllTextAsync(CodexConfigPath)).ShouldBe(PinnedTomlAfterInstall + "\n");

        var backups = Directory.GetFiles(_home, "*.bak", SearchOption.AllDirectories).ToList();
        backups.Count.ShouldBe(3);
        var claudeBackupContents = backups
            .Where(path => Path.GetFileName(path).StartsWith(".claude.json", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToList();
        claudeBackupContents.Count.ShouldBe(2);
        claudeBackupContents.ShouldContain(content => content.Contains(BinaryPath, StringComparison.Ordinal));
        claudeBackupContents.ShouldContain(content => content == JsonFixture);
    }
}
