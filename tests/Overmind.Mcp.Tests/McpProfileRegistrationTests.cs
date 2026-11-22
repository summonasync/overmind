using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class McpProfileRegistrationTests
{
    [Fact]
    public void JournalProfile_RegistersAnEmptySurfaceWithZeroPlanningTools()
    {
        using var provider = Build(McpProfile.Journal);

        provider.GetServices<McpServerTool>()
            .Select(t => t.ProtocolTool.Name)
            .ToList()
            .ShouldBeEmpty("the journal profile must expose zero planning tools until T1/T2 land the journal family");

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

    private static ServiceProvider Build(McpProfile profile)
    {
        var services = new ServiceCollection();
        services.AddMcpServer().WithProfileSurface(profile);
        return services.BuildServiceProvider();
    }
}
