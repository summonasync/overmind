using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class McpProfileRegistrationTests
{
    internal const int JournalDescriptionMax = 320;

    [Fact]
    public void JournalProfile_RegistersExactlyTheJournalFamily()
    {
        using var provider = Build(McpProfile.Journal);

        var toolNames = provider.GetServices<McpServerTool>()
            .Select(t => t.ProtocolTool.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        toolNames.ShouldBe(
            ToolDescriptionScanner.ExpectedJournalToolNames.OrderBy(n => n, StringComparer.Ordinal).ToList(),
            "the journal profile exposes exactly the journal family — zero planning tools ride along");

        toolNames.Intersect(ToolDescriptionScanner.ExpectedToolNames).ToList()
            .ShouldBeEmpty("journal-only tools must never leak into the frozen planning inventory");

        provider.GetServices<McpServerResource>().ToList().ShouldBeEmpty();
        provider.GetServices<McpServerPrompt>().ToList().ShouldBeEmpty();
    }

    [Fact]
    public void PlanningProfile_RegistersTheFrozenToolInventory()
    {
        using var provider = Build(McpProfile.Planning);

        var toolNames = provider.GetServices<McpServerTool>()
            .Select(t => t.ProtocolTool.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        toolNames.ShouldBe(
            ToolDescriptionScanner.ExpectedToolNames.OrderBy(n => n, StringComparer.Ordinal).ToList(),
            "the planning profile must stay byte-identical to today's registered surface");

        provider.GetServices<McpServerResource>().ShouldNotBeEmpty();
        provider.GetServices<McpServerPrompt>().ShouldNotBeEmpty();
    }

    [Fact]
    public void JournalDescriptions_FitTheFamilyCap()
    {
        var journal = ToolDescriptionScanner.Scan()
            .Tools.Where(t => t.ToolName.StartsWith("journal_", StringComparison.Ordinal))
            .ToList();

        journal.Count.ShouldBe(ToolDescriptionScanner.ExpectedJournalToolNames.Length,
            "the journal cap pin walked the wrong family — run Scan_SeesTheWholeToolSurfaceOnce first");

        foreach (var tool in journal)
        {
            tool.DescriptionChars.ShouldBeLessThanOrEqualTo(JournalDescriptionMax,
                $"{tool.ToolName} description is {tool.DescriptionChars} chars — the journal door is written for cheap agents");

            tool.Parameters
                .Where(p => p.Chars > DescriptionBudgets.ParameterDescriptionMax)
                .ToList()
                .ShouldBeEmpty($"{tool.ToolName} carries an over-budget parameter description");
        }
    }

    [Fact]
    public void JournalDescriptions_NameDoorCapsAndLoopback()
    {
        var descriptions = JournalDescriptions();

        foreach (var (name, text) in descriptions)
        {
            text.Contains("Loopback-only").ShouldBeTrue($"{name} must state the loopback-only fact (spec R4)");
        }

        descriptions["journal_capture"].ShouldContain("POST /api/entries");
        descriptions["journal_capture"].ShouldContain("500");
        descriptions["journal_search"].ShouldContain("GET /api/search");
        descriptions["journal_search"].ShouldContain("1..100");
        descriptions["journal_read"].ShouldContain("GET /api/entries/");
        descriptions["journal_list"].ShouldContain("GET /api/projects/");
        descriptions["journal_list"].ShouldContain("limit");
        descriptions["journal_append"].ShouldContain("/append");
        descriptions["journal_append"].ShouldContain("2000");
        descriptions["journal_resolve_links"].ShouldContain("resolve-links");
        descriptions["journal_resolve_links"].ShouldContain("2000");
    }

    private static Dictionary<string, string> JournalDescriptions() =>
        typeof(JournalTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => (
                Name: method.GetCustomAttribute<McpServerToolAttribute>()?.Name ?? method.Name,
                Text: method.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description ?? string.Empty))
            .ToDictionary(pair => pair.Name, pair => pair.Text);

    private static ServiceProvider Build(McpProfile profile)
    {
        var services = new ServiceCollection();
        services.AddMcpServer().WithProfileSurface(profile);
        return services.BuildServiceProvider();
    }
}
