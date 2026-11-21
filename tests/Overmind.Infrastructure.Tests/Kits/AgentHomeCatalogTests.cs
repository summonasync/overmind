using Overmind.Core;
using Overmind.Core.Agents;
using Overmind.Infrastructure.Kits;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class AgentHomeCatalogTests
{
    [Fact]
    public void Build_covers_every_registered_agent_exactly_once()
    {
        var specs = AgentHomeCatalog.Build();

        specs.Count.ShouldBe(AgentRegistry.All.Count);
        specs.Select(s => s.AgentName).Distinct().Count().ShouldBe(specs.Count);
        AgentRegistry.All.ShouldAllBe(a => specs.Any(s => s.AgentName == a.Name));
    }

    [Fact]
    public void Build_maps_all_six_platforms_and_keeps_candidates()
    {
        var specs = AgentHomeCatalog.Build();

        specs.Select(s => s.Platform).ShouldBe(new[]
        {
            AgentPlatform.KimiCode,
            AgentPlatform.Claude,
            AgentPlatform.Codex,
            AgentPlatform.Gemini,
            AgentPlatform.Antigravity,
            AgentPlatform.Qwen
        }, true);

        specs.ShouldAllBe(s => s.ConfigDirCandidates.Length > 0);
        specs.ShouldAllBe(s => !string.IsNullOrWhiteSpace(s.EntryFileName));
    }

    [Fact]
    public void Build_carries_the_dashless_platform_mapping_for_kimi_code()
    {
        var kimi = AgentHomeCatalog.Build().Single(s => s.AgentName == "kimi-code");

        kimi.Platform.ShouldBe(AgentPlatform.KimiCode);
        kimi.ConfigDirCandidates.ShouldContain("$KIMI_CODE_HOME");
        kimi.EntryFileName.ShouldBe("AGENTS.md");
    }
}
