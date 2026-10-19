using System.Text.Json.Nodes;
using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class JsonConfigUpsertEngineGrimoireTests : IDisposable
{
    private const string CommandPath = "/home/user/apps/overmind/mcp/overmind-mcp";

    private const string ConfigWithFilesystem = """
        {
          "theme": "dark",
          "mcpServers": {
            "filesystem": {
              "command": "npx"
            }
          }
        }
        """;

    private const string ConfigWithFilesystemAndOvermind = """
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

    private const string ConfigWithForeignGrimoire = """
        {
          "mcpServers": {
            "grimoire": {
              "command": "grimoire",
              "args": ["serve"]
            }
          }
        }
        """;

    private const string ConfigWithMatchingGrimoire = """
        {
          "mcpServers": {
            "grimoire": {
              "command": "grimoire",
              "args": ["mcp"]
            }
          }
        }
        """;

    private const string ConfigWithAllThreeServers = """
        {
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

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"overmind-json-grim-{Guid.NewGuid():N}");

    public JsonConfigUpsertEngineGrimoireTests()
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
    public async Task Upsert_Grimoire_AddsCommandAndArgs_PreservesOtherServers()
    {
        File.WriteAllText(ConfigPath, ConfigWithFilesystem);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);

        var written = JsonNode.Parse(File.ReadAllText(ConfigPath))!;
        written["mcpServers"]!["grimoire"]!["command"]!.GetValue<string>().ShouldBe("grimoire");
        var grimoireArgs = ((JsonArray)written["mcpServers"]!["grimoire"]!["args"]!)
            .Select(node => node!.GetValue<string>())
            .ToList();
        grimoireArgs.ShouldBe(["mcp"]);
        written["mcpServers"]!["filesystem"]!["command"]!.GetValue<string>().ShouldBe("npx");
        written["mcpServers"]!["overmind"].ShouldBeNull();
        BackupFiles().Length.ShouldBe(1);
    }

    [Fact]
    public async Task Upsert_Grimoire_SecondRun_UnchangedAndBytesStable()
    {
        File.WriteAllText(ConfigPath, ConfigWithFilesystem);
        var engine = Engine();

        var first = await engine.UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);
        var afterFirst = File.ReadAllText(ConfigPath);

        var second = await engine.UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);
        var afterSecond = File.ReadAllText(ConfigPath);

        first.Outcome.ShouldBe(ConfigUpsertOutcome.Added);
        second.Outcome.ShouldBe(ConfigUpsertOutcome.Unchanged);
        afterSecond.ShouldBe(afterFirst);
        BackupFiles().Length.ShouldBe(1);
    }

    [Fact]
    public async Task Upsert_Grimoire_MatchingEntryAlreadyPresent_UnchangedAndWritesNothing()
    {
        File.WriteAllText(ConfigPath, ConfigWithMatchingGrimoire);
        var before = File.ReadAllText(ConfigPath);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Unchanged);
        File.ReadAllText(ConfigPath).ShouldBe(before);
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Upsert_Grimoire_ExistingOvermindEntry_StaysIntact()
    {
        File.WriteAllText(ConfigPath, ConfigWithFilesystemAndOvermind);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);

        var written = JsonNode.Parse(File.ReadAllText(ConfigPath))!;
        written["mcpServers"]!["overmind"]!["command"]!.GetValue<string>().ShouldBe("/opt/overmind/bin/overmind-mcp");
        written["mcpServers"]!["grimoire"]!["command"]!.GetValue<string>().ShouldBe("grimoire");
    }

    [Fact]
    public async Task Upsert_Grimoire_WrongArgs_ReturnsUpdatedAndFixesArgs()
    {
        File.WriteAllText(ConfigPath, ConfigWithForeignGrimoire);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Updated);

        var written = JsonNode.Parse(File.ReadAllText(ConfigPath))!;
        var fixedArgs = ((JsonArray)written["mcpServers"]!["grimoire"]!["args"]!)
            .Select(node => node!.GetValue<string>())
            .ToList();
        fixedArgs.ShouldBe(["mcp"]);
        BackupFiles().Length.ShouldBe(1);
    }

    [Fact]
    public async Task Upsert_Grimoire_MissingFileInMissingDirectory_CreatesConfig()
    {
        var nested = Path.Combine(_dir, "fresh-home", ".kimi-code", "mcp.json");

        var result = await Engine().UpsertAsync(nested, CommandPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);

        var written = JsonNode.Parse(File.ReadAllText(nested))!;
        written["mcpServers"]!["grimoire"]!["command"]!.GetValue<string>().ShouldBe("grimoire");
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Upsert_Grimoire_DryRunOnExistingFile_ReturnsAddedAndWritesNothing()
    {
        File.WriteAllText(ConfigPath, ConfigWithFilesystem);
        var before = File.ReadAllText(ConfigPath);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: true, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);
        File.ReadAllText(ConfigPath).ShouldBe(before);
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Upsert_Grimoire_DryRunOnMissingFile_ReturnsAddedAndCreatesNothing()
    {
        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: true, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);
        File.Exists(ConfigPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Upsert_Grimoire_MalformedJson_ReturnsFailedAndLeavesFileUntouched()
    {
        const string malformed = "{ \"mcpServers\": ";
        File.WriteAllText(ConfigPath, malformed);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Failed);
        result.Error.ShouldNotBeNullOrWhiteSpace();
        File.ReadAllText(ConfigPath).ShouldBe(malformed);
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Remove_Grimoire_RemovesOnlyGrimoireKey()
    {
        File.WriteAllText(ConfigPath, ConfigWithAllThreeServers);

        var result = await Engine().RemoveAsync(ConfigPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Removed);

        var written = JsonNode.Parse(File.ReadAllText(ConfigPath))!;
        written["mcpServers"]!["grimoire"].ShouldBeNull();
        written["mcpServers"]!["overmind"]!["command"]!.GetValue<string>().ShouldBe("/opt/overmind/bin/overmind-mcp");
        written["mcpServers"]!["filesystem"]!["command"]!.GetValue<string>().ShouldBe("npx");
        BackupFiles().Length.ShouldBe(1);
    }

    [Fact]
    public async Task Remove_Grimoire_AbsentKey_ReturnsNotPresentAndWritesNothing()
    {
        File.WriteAllText(ConfigPath, ConfigWithFilesystemAndOvermind);
        var before = File.ReadAllText(ConfigPath);

        var result = await Engine().RemoveAsync(ConfigPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.NotPresent);
        File.ReadAllText(ConfigPath).ShouldBe(before);
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Remove_Grimoire_MissingFile_ReturnsNotPresent()
    {
        var result = await Engine().RemoveAsync(ConfigPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.NotPresent);
        File.Exists(ConfigPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Remove_Grimoire_DryRun_ReturnsRemovedAndWritesNothing()
    {
        File.WriteAllText(ConfigPath, ConfigWithAllThreeServers);
        var before = File.ReadAllText(ConfigPath);

        var result = await Engine().RemoveAsync(ConfigPath, dryRun: true, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Removed);
        File.ReadAllText(ConfigPath).ShouldBe(before);
        BackupFiles().Length.ShouldBe(0);
    }
}
