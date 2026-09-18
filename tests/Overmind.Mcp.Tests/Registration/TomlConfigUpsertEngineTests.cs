using Overmind.Mcp.Registration;
using Shouldly;
using Tomlyn;
using Tomlyn.Model;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class TomlConfigUpsertEngineTests : IDisposable
{
    private const string CommandPath = "/home/user/apps/overmind/mcp/overmind-mcp";

    private const string CodexUserConfig = """
        # user preferences — must survive
        model = "gpt-5-codex"
        model_reasoning_effort = "high"

        [mcp_servers.context7]
        command = "npx"
        args = ["-y", "@upstash/context7-mcp"]

        [mcp_servers.github]
        url = "https://api.githubcopilot.com/mcp/"

        [sandbox]
        writable_roots = ["/home/user/tmp"]

        [projects."/home/user/Projects/demo"]
        trust_level = "trusted"
        """;

    private const string ConfigWithOvermind = """
        model = "gpt-5-codex"

        [mcp_servers.overmind]
        command = "/old/path/overmind-mcp"

        [mcp_servers.context7]
        command = "npx"
        """;

    private const string ConfigWithoutServers = """
        model = "gpt-5-codex"

        [sandbox]
        writable_roots = ["/home/user/tmp"]
        """;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"overmind-toml-upsert-{Guid.NewGuid():N}");

    public TomlConfigUpsertEngineTests()
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
    public async Task Upsert_CodexStyleConfig_PreservesUnrelatedTables()
    {
        File.WriteAllText(ConfigPath, CodexUserConfig);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);

        var model = Toml.ToModel(File.ReadAllText(ConfigPath));
        model["model"].ShouldBe("gpt-5-codex");
        model["model_reasoning_effort"].ShouldBe("high");

        var servers = model["mcp_servers"].ShouldBeOfType<TomlTable>();
        servers["overmind"].ShouldBeOfType<TomlTable>();
        ((TomlTable)servers["overmind"])["command"].ShouldBe(CommandPath);
        ((TomlTable)servers["context7"])["command"].ShouldBe("npx");

        var args = ((TomlTable)servers["context7"])["args"].ShouldBeOfType<TomlArray>();
        args.Count.ShouldBe(2);
        args[0].ShouldBe("-y");
        args[1].ShouldBe("@upstash/context7-mcp");
        ((TomlTable)servers["github"])["url"].ShouldBe("https://api.githubcopilot.com/mcp/");

        var sandbox = model["sandbox"].ShouldBeOfType<TomlTable>();
        var roots = sandbox["writable_roots"].ShouldBeOfType<TomlArray>();
        roots[0].ShouldBe("/home/user/tmp");

        var projects = model["projects"].ShouldBeOfType<TomlTable>();
        var demo = projects["/home/user/Projects/demo"].ShouldBeOfType<TomlTable>();
        demo["trust_level"].ShouldBe("trusted");

        var raw = File.ReadAllText(ConfigPath);
        raw.Contains("[mcp_servers.overmind]", StringComparison.Ordinal).ShouldBeTrue();
        raw.Contains("[mcp_servers.context7]", StringComparison.Ordinal).ShouldBeTrue();
        raw.Contains("[sandbox]", StringComparison.Ordinal).ShouldBeTrue();
        BackupFiles().Length.ShouldBe(1);
    }

    [Fact]
    public async Task Upsert_NoMcpServersSection_AddsOvermindAndPreservesSections()
    {
        File.WriteAllText(ConfigPath, ConfigWithoutServers);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);

        var model = Toml.ToModel(File.ReadAllText(ConfigPath));
        model["model"].ShouldBe("gpt-5-codex");
        var servers = model["mcp_servers"].ShouldBeOfType<TomlTable>();
        ((TomlTable)servers["overmind"])["command"].ShouldBe(CommandPath);
        var sandbox = model["sandbox"].ShouldBeOfType<TomlTable>();
        ((TomlArray)sandbox["writable_roots"])[0].ShouldBe("/home/user/tmp");
    }

    [Fact]
    public async Task Upsert_SecondRun_ReturnsUnchangedAndWritesNothing()
    {
        File.WriteAllText(ConfigPath, CodexUserConfig);
        var engine = new TomlConfigUpsertEngine(new ConfigBackupTracker());

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

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Updated);

        var model = Toml.ToModel(File.ReadAllText(ConfigPath));
        var servers = model["mcp_servers"].ShouldBeOfType<TomlTable>();
        ((TomlTable)servers["overmind"])["command"].ShouldBe(CommandPath);
        ((TomlTable)servers["context7"])["command"].ShouldBe("npx");
        model["model"].ShouldBe("gpt-5-codex");
    }

    [Fact]
    public async Task Upsert_MissingFileInMissingDirectory_CreatesConfig()
    {
        var nested = Path.Combine(_dir, "fresh-home", ".codex", "config.toml");

        var result = await Engine().UpsertAsync(nested, CommandPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Added);

        var raw = File.ReadAllText(nested);
        var model = Toml.ToModel(raw);
        var servers = model["mcp_servers"].ShouldBeOfType<TomlTable>();
        ((TomlTable)servers["overmind"])["command"].ShouldBe(CommandPath);
        raw.Contains("[mcp_servers.overmind]", StringComparison.Ordinal).ShouldBeTrue();
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Upsert_MalformedToml_ReturnsFailedAndLeavesFileUntouched()
    {
        const string malformed = "model = \"gpt-5-codex\"\n[mcp_servers.context7\ncommand = \"npx\"\n";
        File.WriteAllText(ConfigPath, malformed);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Failed);
        result.Error.ShouldNotBeNullOrWhiteSpace();
        File.ReadAllText(ConfigPath).ShouldBe(malformed);
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Upsert_ServersEntryNotTable_ReturnsFailedAndLeavesFileUntouched()
    {
        const string fixture = "mcp_servers = \"oops\"\n";
        File.WriteAllText(ConfigPath, fixture);

        var result = await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Failed);
        result.Error.ShouldNotBeNullOrWhiteSpace();
        File.ReadAllText(ConfigPath).ShouldBe(fixture);
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public async Task Upsert_CommentsPreservedOnRewrite_Pinned()
    {
        File.WriteAllText(ConfigPath, CodexUserConfig);

        await Engine().UpsertAsync(ConfigPath, CommandPath, dryRun: false, CancellationToken.None);

        var raw = File.ReadAllText(ConfigPath);
        raw.Contains("# user preferences — must survive", StringComparison.Ordinal).ShouldBeTrue();
        Toml.ToModel(raw)["model"].ShouldBe("gpt-5-codex");
    }

    [Fact]
    public async Task Upsert_DryRunOnExistingFile_ReturnsPlanAndWritesNothing()
    {
        File.WriteAllText(ConfigPath, CodexUserConfig);
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
    public async Task Remove_ExistingOvermind_RemovesOnlyOvermindTable()
    {
        File.WriteAllText(ConfigPath, ConfigWithOvermind);

        var result = await Engine().RemoveAsync(ConfigPath, dryRun: false, CancellationToken.None);

        result.Outcome.ShouldBe(ConfigUpsertOutcome.Removed);

        var model = Toml.ToModel(File.ReadAllText(ConfigPath));
        var servers = model["mcp_servers"].ShouldBeOfType<TomlTable>();
        servers.ContainsKey("overmind").ShouldBeFalse();
        ((TomlTable)servers["context7"])["command"].ShouldBe("npx");
        model["model"].ShouldBe("gpt-5-codex");
        BackupFiles().Length.ShouldBe(1);
    }

    [Fact]
    public async Task Remove_AbsentOvermind_ReturnsNotPresentAndWritesNothing()
    {
        File.WriteAllText(ConfigPath, CodexUserConfig);
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
