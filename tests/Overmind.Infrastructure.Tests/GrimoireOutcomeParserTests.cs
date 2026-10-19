using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class GrimoireOutcomeParserTests
{
    [Fact]
    public void Parse_EngineOutput_ExtractsGrimoireAndSkippedLinesOnly()
    {
        var output = string.Join('\n',
            "claude: added — /home/u/.claude.json",
            "claude: grimoire added — /home/u/.claude.json",
            "codex: skipped (not detected) — /home/u/.codex/config.toml",
            "kimi-code: grimoire unchanged — /home/u/.kimi/config.json",
            "summary: 3 processed, 1 added, 0 updated, 1 unchanged, 0 removed, 0 not present, 1 skipped, 0 failed");

        var outcomes = GrimoireOutcomeParser.Parse(output);

        outcomes.Count.ShouldBe(3);
        outcomes[0].Agent.ShouldBe("claude");
        outcomes[0].Outcome.ShouldBe("added");
        outcomes[0].ConfigPath.ShouldBe("/home/u/.claude.json");
        outcomes[0].Error.ShouldBeNull();
        outcomes[1].Agent.ShouldBe("codex");
        outcomes[1].Outcome.ShouldBe("skipped");
        outcomes[1].ConfigPath.ShouldBe("/home/u/.codex/config.toml");
        outcomes[2].Agent.ShouldBe("kimi-code");
        outcomes[2].Outcome.ShouldBe("unchanged");
    }

    [Fact]
    public void Parse_FailureLine_SurfacesError()
    {
        var output = "codex: grimoire failed — /home/u/.codex/config.toml (config file is locked)";

        var outcomes = GrimoireOutcomeParser.Parse(output);

        outcomes.Count.ShouldBe(1);
        outcomes[0].Outcome.ShouldBe("failed");
        outcomes[0].ConfigPath.ShouldBe("/home/u/.codex/config.toml");
        outcomes[0].Error.ShouldBe("config file is locked");
    }

    [Fact]
    public void Parse_PathContainingBraces_KeepsErrorSplit()
    {
        var output = "claude: grimoire updated — /home/u (2026)/.claude.json (busy)";

        var outcomes = GrimoireOutcomeParser.Parse(output);

        outcomes[0].ConfigPath.ShouldBe("/home/u (2026)/.claude.json");
        outcomes[0].Error.ShouldBe("busy");
    }

    [Fact]
    public void Parse_VerbWithoutDetail_KeepsOutcomeWithoutPath()
    {
        var outcomes = GrimoireOutcomeParser.Parse("claude: grimoire not present");

        outcomes[0].Outcome.ShouldBe("not present");
        outcomes[0].ConfigPath.ShouldBeNull();
    }

    [Fact]
    public void Parse_EmptyOutput_ReturnsEmpty()
    {
        GrimoireOutcomeParser.Parse(string.Empty).ShouldBeEmpty();
        GrimoireOutcomeParser.Parse("   \n  ").ShouldBeEmpty();
    }
}
