using Overmind.Mcp.Registration;
using Shouldly;
using Tomlyn;
using Tomlyn.Model;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class TomlConfigUpsertEngineGrimoireTests : IDisposable
{
    private const string CommandPath = "/home/user/apps/overmind/mcp/overmind-mcp";

    private const string CodexUserConfig = """
        model = "gpt-5-codex"

        [mcp_servers.context7]
        command = "npx"

        [sandbox]
        writable_roots = ["/home/user/tmp"]
        """;

    private const string ConfigWithMatchingGrimoire = """
        model = "gpt-5-codex"

        [mcp_servers.grimoire]
        command = "grimoire"
        args = ["mcp"]
        """;

    private const string ConfigWithForeignGrimoire = """
        [mcp_servers.grimoire]
        command = "grimoire"
        args = ["serve"]
        """;

    private const string ConfigWithAllThreeServers = """
        model = "gpt-5-codex"

        [mcp_servers.overmind]
        command = "/opt/overmind/bin/overmind-mcp"

        [mcp_servers.grimoire]
        command = "grimoire"
        args = ["mcp"]

        [mcp_servers.context7]
        command = "npx"
        """;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"overmind-toml-grim-{Guid.NewGuid():N}");

    public TomlConfigUpsertEngineGrimoireTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    private string ConfigPath => Path.Combine(_dir, "config.toml");

    private static TomlConfigUpsertEngine Engine(ConfigBackupTracker? tracker = null)
    {
        return new TomlConfigUpsertEngine(tracker ?? new ConfigBackupTracker());
    }

    private string[] BackupFiles()
    {
        return Directory.GetFiles(_dir, "*.bak", SearchOption.AllDirectories);
    }

    [Fact]
    public async Task Upsert_Grimoire_AddsCommandAndArgs_PreservesOtherTables()
    {
        File.WriteAllText(ConfigPath, CodexUserConfig);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);

        var model = Toml.ToModel(File.ReadAllText(ConfigPath));
        model["model"].ShouldBe("gpt-5-codex");
        var servers = model["mcp_servers"].ShouldBeOfType<TomlTable>();
        var grimoire = servers["grimoire"].ShouldBeOfType<TomlTable>();
        grimoire["command"].ShouldBe("grimoire");
        var args = grimoire["args"].ShouldBeOfType<TomlArray>();
        args.Count.ShouldBe(1);
        args[0].ShouldBe("mcp");
        ((TomlTable)servers["context7"])["command"].ShouldBe("npx");
        servers.ContainsKey("overmind").ShouldBeFalse();
        BackupFiles().Length.ShouldBe(1);
    }

    [Fact]
    public async Task Upsert_Grimoire_SecondRun_UnchangedAndBytesStable()
    {
        File.WriteAllText(ConfigPath, CodexUserConfig);
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
    public async Task Upsert_Grimoire_ExistingOvermindTable_StaysIntact()
    {
        const string fixture = """
            model = "gpt-5-codex"

            [mcp_servers.overmind]
            command = "/opt/overmind/bin/overmind-mcp"
            """;
        File.WriteAllText(ConfigPath, fixture);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);

        var servers = Toml.ToModel(File.ReadAllText(ConfigPath))["mcp_servers"].ShouldBeOfType<TomlTable>();
        ((TomlTable)servers["overmind"])["command"].ShouldBe("/opt/overmind/bin/overmind-mcp");
        ((TomlTable)servers["grimoire"])["command"].ShouldBe("grimoire");
    }

    [Fact]
    public async Task Upsert_Grimoire_WrongArgs_ReturnsUpdatedAndFixesArgs()
    {
        File.WriteAllText(ConfigPath, ConfigWithForeignGrimoire);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Updated);

        var grimoire = Toml.ToModel(File.ReadAllText(ConfigPath))["mcp_servers"].ShouldBeOfType<TomlTable>()["grimoire"].ShouldBeOfType<TomlTable>();
        var args = grimoire["args"].ShouldBeOfType<TomlArray>();
        args.Count.ShouldBe(1);
        args[0].ShouldBe("mcp");
        BackupFiles().Length.ShouldBe(1);
    }

    [Fact]
    public async Task Upsert_Grimoire_MissingFileInMissingDirectory_CreatesConfig()
    {
        var nested = Path.Combine(_dir, "fresh-home", ".codex", "config.toml");

        var result = await Engine().UpsertAsync(nested, CommandPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);

        var raw = File.ReadAllText(nested);
        raw.Contains("[mcp_servers.grimoire]", StringComparison.Ordinal).ShouldBeTrue();
        raw.Contains("args = [\"mcp\"]", StringComparison.Ordinal).ShouldBeTrue();
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Upsert_Grimoire_DryRunOnExistingFile_ReturnsAddedAndWritesNothing()
    {
        File.WriteAllText(ConfigPath, CodexUserConfig);
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
    public async Task Upsert_Grimoire_MalformedToml_ReturnsFailedAndLeavesFileUntouched()
    {
        const string malformed = "[mcp_servers.grimoire\ncommand = \"grimoire\"\n";
        File.WriteAllText(ConfigPath, malformed);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Failed);
        result.Error.ShouldNotBeNullOrWhiteSpace();
        File.ReadAllText(ConfigPath).ShouldBe(malformed);
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Remove_Grimoire_RemovesOnlyGrimoireTable()
    {
        File.WriteAllText(ConfigPath, ConfigWithAllThreeServers);

        var result = await Engine().RemoveAsync(ConfigPath, dryRun: false, CancellationToken.None, ManagedServerKey.Grimoire);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Removed);

        var model = Toml.ToModel(File.ReadAllText(ConfigPath));
        var servers = model["mcp_servers"].ShouldBeOfType<TomlTable>();
        servers.ContainsKey("grimoire").ShouldBeFalse();
        ((TomlTable)servers["overmind"])["command"].ShouldBe("/opt/overmind/bin/overmind-mcp");
        ((TomlTable)servers["context7"])["command"].ShouldBe("npx");
        model["model"].ShouldBe("gpt-5-codex");
        BackupFiles().Length.ShouldBe(1);
    }

    [Fact]
    public async Task Remove_Grimoire_AbsentKey_ReturnsNotPresentAndWritesNothing()
    {
        File.WriteAllText(ConfigPath, CodexUserConfig);
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
