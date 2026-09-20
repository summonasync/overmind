using System.Text.Json.Nodes;
using Overmind.Core;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class StatusModelContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        RegisterPlanningRoutes(stub);
    }

    [Fact]
    public void Quickstart_MentionsEveryFeatureStatusName()
    {
        var text = OnboardQuickstart.Text;

        foreach (var status in Enum.GetNames<PlanningFeatureStatus>())
        {
            text.ShouldContain(status, Case.Insensitive);
        }
    }

    [Theory]
    [InlineData(PlanningArtifactKind.Spec)]
    [InlineData(PlanningArtifactKind.Plan)]
    [InlineData(PlanningArtifactKind.Result)]
    [InlineData(PlanningArtifactKind.Task)]
    public void Quickstart_PerKindStatusListing_MatchesAllowedStatuses(PlanningArtifactKind kind)
    {
        var listed = ListedStatusNames(OnboardQuickstart.Text, kind);

        listed.ShouldBe(AllowedStatusNames(kind), ignoreOrder: true);
    }

    [Theory]
    [InlineData(PlanningArtifactKind.Spec)]
    [InlineData(PlanningArtifactKind.Plan)]
    [InlineData(PlanningArtifactKind.Result)]
    [InlineData(PlanningArtifactKind.Task)]
    public async Task PlanningSetStatusDescription_QuotesAllowedStatusesPerKind(PlanningArtifactKind kind)
    {
        var description = await ToolDescription("planning_set_status");

        var listed = ListedStatusNames(description, kind);

        listed.ShouldBe(AllowedStatusNames(kind), ignoreOrder: true);
    }

    [Fact]
    public async Task PlanningSetStatusDescription_NeverClaimsDoneForTheResultKind()
    {
        var description = await ToolDescription("planning_set_status");

        ListedStatusNames(description, PlanningArtifactKind.Result).ShouldNotContain("done");
    }

    [Fact]
    public void Quickstart_NeverClaimsDoneForTheResultKind()
    {
        ListedStatusNames(OnboardQuickstart.Text, PlanningArtifactKind.Result).ShouldNotContain("done");
    }

    [Fact]
    public async Task PlanningSetStatusDescription_CarriesTheLogNoteMandatoryPhrase()
    {
        var description = await ToolDescription("planning_set_status");

        description.ShouldContain("logNote is mandatory on every move");
    }

    [Fact]
    public async Task PlanningUpsertArtifactDescription_CarriesDraftCreateAndValidationPath()
    {
        var description = await ToolDescription("planning_upsert_artifact");

        description.ShouldContain("created draft", Case.Insensitive);
        description.ShouldContain("validation goes through planning_validate", Case.Insensitive);
    }

    private static string[] AllowedStatusNames(PlanningArtifactKind kind) =>
        PlanningArtifact.AllowedStatuses(kind)
            .Select(status => status.ToString().ToLowerInvariant())
            .ToArray();

    private static string[] ListedStatusNames(string text, PlanningArtifactKind kind)
    {
        var marker = kind == PlanningArtifactKind.Task ? "task:" : "spec/plan/result:";
        var start = text.IndexOf(marker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"status listing for kind '{kind}' is missing from: {text}");

        var listing = text[(start + marker.Length)..];
        var end = listing.IndexOfAny([';', '.']);
        if (end >= 0)
        {
            listing = listing[..end];
        }

        return listing
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(status => status.ToLowerInvariant())
            .ToArray();
    }

    private async Task<string> ToolDescription(string toolName)
    {
        var response = await Driver.ListToolsAsync();

        var tool = response["result"]!["tools"]!.AsArray()
            .Single(t => t!["name"]!.GetValue<string>() == toolName);
        return tool!["description"]!.GetValue<string>();
    }
}
