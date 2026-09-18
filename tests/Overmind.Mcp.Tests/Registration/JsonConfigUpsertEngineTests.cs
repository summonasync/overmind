using System.Text.Json.Nodes;
using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class JsonConfigUpsertEngineTests : IDisposable
{
    private const string CommandPath = "/home/user/apps/overmind/mcp/overmind-mcp";
    private const string UpdatedCommandPath = "/home/user/apps/overmind/mcp/overmind-mcp-v2";

    private const string ClaudeUserConfig = """
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
              "allowedTools": ["Bash", "Edit"],
              "mcpServers": {
                "context7": {
                  "command": "npx",
                  "args": ["-u", "@upstash/context7-mcp"]
                }
              }
            }
          },
          "permissions": {
            "allow": ["Bash(npm run test:*)"],
            "deny": []
          }
        }
        """;

    private const string FourSpaceConfig = """
        {
            "theme": "dark",
            "mcpServers": {
                "filesystem": {
                    "command": "npx"
                }
            }
        }
        """;

    private const string ConfigWithOvermind = """
        {
          "theme": "dark",
          "mcpServers": {
            "filesystem": {
              "command": "npx"
            },
            "overmind": {
              "command": "/old/path/overmind-mcp"
            }
          }
        }
        """;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"overmind-json-upsert-{Guid.NewGuid():N}");

    public JsonConfigUpsertEngineTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    private string ConfigPath => Path.Combine(_dir, ".claude.json");

    private static JsonConfigUpsertEngine Engine(ConfigBackupTracker? tracker = null)
    {
        return new JsonConfigUpsertEngine(tracker ?? new ConfigBackupTracker());
    }

    private string[] BackupFiles()
    {
        return Directory.GetFiles(_dir, "*.bak", SearchOption.AllDirectories);
    }

    [Fact]
    public async Task Upsert_ClaudeStyleConfig_PreservesUnrelatedStructure()
    {
        File.WriteAllText(ConfigPath, ClaudeUserConfig);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);

        var original = JsonNode.Parse(ClaudeUserConfig)!;
        var written = JsonNode.Parse(File.ReadAllText(ConfigPath))!;

        JsonNode.DeepEquals(written["theme"], original["theme"]).ShouldBeTrue();
        JsonNode.DeepEquals(written["mcpServers"]!["filesystem"], original["mcpServers"]!["filesystem"]).ShouldBeTrue();
        JsonNode.DeepEquals(written["projects"], original["projects"]).ShouldBeTrue();
        JsonNode.DeepEquals(written["permissions"], original["permissions"]).ShouldBeTrue();
        written["mcpServers"]!["overmind"]!["command"]!.GetValue<string>().ShouldBe(CommandPath);

        var raw = File.ReadAllText(ConfigPath);
        raw.Contains("\n  \"mcpServers\"", StringComparison.Ordinal).ShouldBeTrue();
        BackupFiles().Length.ShouldBe(1);
    }

    [Fact]
    public async Task Upsert_FourSpaceIndentedFile_KeepsFourSpaceIndentation()
    {
        File.WriteAllText(ConfigPath, FourSpaceConfig);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);

        var written = JsonNode.Parse(File.ReadAllText(ConfigPath))!;
        written["mcpServers"]!["overmind"]!["command"]!.GetValue<string>().ShouldBe(CommandPath);
        written["mcpServers"]!["filesystem"]!["command"]!.GetValue<string>().ShouldBe("npx");

        var raw = File.ReadAllText(ConfigPath);
        raw.Contains("\n    \"mcpServers\"", StringComparison.Ordinal).ShouldBeTrue();
        raw.Contains("\n        \"filesystem\"", StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public async Task Upsert_CrlfFile_KeepsCrlfLineEndings()
    {
        File.WriteAllText(ConfigPath, ClaudeUserConfig.Replace("\n", "\r\n", StringComparison.Ordinal));

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);

        var raw = File.ReadAllText(ConfigPath);
        raw.Contains("\r\n", StringComparison.Ordinal).ShouldBeTrue();
        raw.Replace("\r\n", string.Empty, StringComparison.Ordinal).Contains('\n').ShouldBeFalse();
    }

    [Fact]
    public async Task Upsert_SecondRun_ReturnsUnchangedAndWritesNothing()
    {
        File.WriteAllText(ConfigPath, ClaudeUserConfig);
        var engine = new JsonConfigUpsertEngine(new ConfigBackupTracker());

        var first = await engine.UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None);
        var afterFirst = File.ReadAllText(ConfigPath);

        var second = await engine.UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None);
        var afterSecond = File.ReadAllText(ConfigPath);

        first.Outcome.ShouldBe(ConfigUpsertOutcome.Added);
        second.Outcome.ShouldBe(ConfigUpsertOutcome.Unchanged);
        afterSecond.ShouldBe(afterFirst);
        BackupFiles().Length.ShouldBe(1);
    }

    [Fact]
    public async Task Upsert_ExistingOvermindDifferentCommand_ReturnsUpdatedAndPreservesOtherServers()
    {
        File.WriteAllText(ConfigPath, ConfigWithOvermind);

        var result = await Engine().UpsertAsync(ConfigPath, UpdatedCommandPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Updated);

        var written = JsonNode.Parse(File.ReadAllText(ConfigPath))!;
        written["mcpServers"]!["overmind"]!["command"]!.GetValue<string>().ShouldBe(UpdatedCommandPath);
        written["mcpServers"]!["filesystem"]!["command"]!.GetValue<string>().ShouldBe("npx");
    }

    [Fact]
    public async Task Upsert_TwoMutationsInOneRun_CreateSingleBackupOfOriginal()
    {
        File.WriteAllText(ConfigPath, ClaudeUserConfig);
        var engine = new JsonConfigUpsertEngine(new ConfigBackupTracker());

        var first = await engine.UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None);
        var second = await engine.UpsertAsync(ConfigPath, UpdatedCommandPath, dryRun: false, CancellationToken.None);

        first.Outcome.ShouldBe(ConfigUpsertOutcome.Added);
        second.Outcome.ShouldBe(ConfigUpsertOutcome.Updated);
        BackupFiles().Length.ShouldBe(1);

        var backup = JsonNode.Parse(File.ReadAllText(BackupFiles()[0]))!;
        JsonNode.DeepEquals(backup["projects"], JsonNode.Parse(ClaudeUserConfig)!["projects"]).ShouldBeTrue();
    }

    [Fact]
    public async Task Upsert_MissingFileInMissingDirectory_CreatesConfigWithDefaultIndent()
    {
        var nested = Path.Combine(_dir, "fresh-home", ".kimi-code", "mcp.json");

        var result = await Engine().UpsertAsync(nested, CommandPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);

        var raw = File.ReadAllText(nested);
        var written = JsonNode.Parse(raw)!;
        written["mcpServers"]!["overmind"]!["command"]!.GetValue<string>().ShouldBe(CommandPath);
        raw.Contains("\n  \"mcpServers\"", StringComparison.Ordinal).ShouldBeTrue();
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Upsert_MalformedJson_ReturnsFailedAndLeavesFileUntouched()
    {
        const string malformed = "{ \"theme\": \"dark\", ";
        File.WriteAllText(ConfigPath, malformed);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Failed);
        result.Error.ShouldNotBeNullOrWhiteSpace();
        File.ReadAllText(ConfigPath).ShouldBe(malformed);
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Upsert_NonObjectRoot_ReturnsFailedAndLeavesFileUntouched()
    {
        const string fixture = "[1, 2, 3]";
        File.WriteAllText(ConfigPath, fixture);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Failed);
        result.Error.ShouldNotBeNullOrWhiteSpace();
        File.ReadAllText(ConfigPath).ShouldBe(fixture);
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Upsert_ServersPropertyNotObject_ReturnsFailedAndLeavesFileUntouched()
    {
        const string fixture = "{ \"mcpServers\": \"oops\" }";
        File.WriteAllText(ConfigPath, fixture);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Failed);
        result.Error.ShouldNotBeNullOrWhiteSpace();
        File.ReadAllText(ConfigPath).ShouldBe(fixture);
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Upsert_DryRunOnExistingFile_ReturnsPlanAndWritesNothing()
    {
        File.WriteAllText(ConfigPath, ClaudeUserConfig);
        var before = File.ReadAllText(ConfigPath);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: true, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);
        File.ReadAllText(ConfigPath).ShouldBe(before);
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Upsert_DryRunOnMissingFile_ReturnsAddedAndCreatesNothing()
    {
        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: true, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);
        File.Exists(ConfigPath).ShouldBeFalse();
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Remove_ExistingOvermind_RemovesOnlyOvermindKey()
    {
        File.WriteAllText(ConfigPath, ConfigWithOvermind);

        var result = await Engine().RemoveAsync(ConfigPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Removed);

        var written = JsonNode.Parse(File.ReadAllText(ConfigPath))!;
        written["mcpServers"]!["overmind"].ShouldBeNull();
        written["mcpServers"]!["filesystem"]!["command"]!.GetValue<string>().ShouldBe("npx");
        written["theme"]!.GetValue<string>().ShouldBe("dark");
        BackupFiles().Length.ShouldBe(1);
    }

    [Fact]
    public async Task Remove_AbsentOvermind_ReturnsNotPresentAndWritesNothing()
    {
        File.WriteAllText(ConfigPath, ClaudeUserConfig);
        var before = File.ReadAllText(ConfigPath);

        var result = await Engine().RemoveAsync(ConfigPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.NotPresent);
        File.ReadAllText(ConfigPath).ShouldBe(before);
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Remove_MissingFile_ReturnsNotPresent()
    {
        var result = await Engine().RemoveAsync(ConfigPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.NotPresent);
        File.Exists(ConfigPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Remove_DryRun_ReturnsRemovedAndWritesNothing()
    {
        File.WriteAllText(ConfigPath, ConfigWithOvermind);
        var before = File.ReadAllText(ConfigPath);

        var result = await Engine().RemoveAsync(ConfigPath, dryRun: true, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Removed);
        File.ReadAllText(ConfigPath).ShouldBe(before);
        BackupFiles().Length.ShouldBe(0);
    }
}
