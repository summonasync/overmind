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

    [Fact]
    public void Quickstart_StatesTheReviewInvariantAndDemoteRule()
    {
        var text = OnboardQuickstart.Text;

        text.ShouldContain("review ⟺ spec+plan validated AND all tasks done");
        text.ShouldContain("pulls a review feature back to inProgress");
    }

    [Fact]
    public async Task MachineSurfaces_NeverSayFrozen()
    {
        var tools = await Driver.ListToolsAsync();
        foreach (var tool in tools["result"]!["tools"]!.AsArray())
        {
            tool!["description"]!.GetValue<string>().ShouldNotContain("frozen", Case.Insensitive);
        }

        var prompt = await Driver.GetPromptAsync("session_bootstrap");
        var promptText = prompt["result"]!["messages"]!.AsArray().First()!["content"]!["text"]!.GetValue<string>();
        promptText.ShouldNotContain("frozen", Case.Insensitive);

        OnboardQuickstart.Text.ShouldNotContain("frozen", Case.Insensitive);

        var changeNote = PlanningException.ChangeNoteRequired().Message;
        changeNote.ShouldContain("validated");
        changeNote.ShouldNotContain("frozen", Case.Insensitive);

        PlanningException.MigratedNoteRequired().Message.ShouldNotContain("frozen", Case.Insensitive);

        var guarded = PlanningArtifact.Create(Guid.NewGuid(), PlanningArtifactKind.Spec, "body");
        guarded.Validate();

        var updateGuard = Should.Throw<InvalidOperationException>(() => guarded.UpdateBody("v2"));
        updateGuard.Message.ShouldContain("validated");
        updateGuard.Message.ShouldNotContain("frozen", Case.Insensitive);

        var moveGuard = Should.Throw<InvalidOperationException>(
            () => guarded.SetStatus(PlanningArtifactStatus.Draft));
        moveGuard.Message.ShouldContain("validated");
        moveGuard.Message.ShouldNotContain("frozen", Case.Insensitive);
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
    public void Quickstart_CoversLifecycleEdgeRulesAndMarkerLegend()
    {
        var text = OnboardQuickstart.Text;

        text.ShouldContain("A done feature receiving a new artifact stays done — no transition fires.");
        text.ShouldContain("Without a plan there is no auto-promotion to review, even when all tasks are done — spec AND plan are required.");
        text.ShouldContain("Import restores statuses verbatim from the materialized files — an imported done feature was not auto-closed.");
        text.ShouldContain("planning_import_feature imports one materialized feature folder from disk in a single call — give its absolute path under the project root.");
        text.ShouldContain("Log lines carry [agent]/[ui]/[auto]/[import] markers saying who moved a status.");
    }

    [Fact]
    public async Task PlanningSetStatusDescription_CarriesTheLogNoteMandatoryPhrase()
    {
        var description = await ToolDescription("planning_set_status");

        description.ShouldContain("logNote is mandatory on every move");
    }

    [Fact]
    public async Task PlanningSetStatusDescription_StatesTheFreeFormTransitionModel()
    {
        var description = await ToolDescription("planning_set_status");

        description.ShouldContain("Transitions are free-form");
        description.ShouldContain("no enforced sequence");
    }

    [Fact]
    public async Task PlanningSetStatusDescription_WarnsAboutUntickedAcOnDoneWithoutBlocking()
    {
        var description = await ToolDescription("planning_set_status");

        description.ShouldContain("non-blocking warning");
    }

    [Fact]
    public async Task PlanningSetStatusDescription_DocumentsTheAppendedLogLineShape()
    {
        var description = await ToolDescription("planning_set_status");

        description.ShouldContain("- YYYY-MM-DD [agent] — <logNote>");
        description.ShouldContain("<log>");
    }

    [Fact]
    public async Task PlanningUpsertArtifactDescription_SaysLogNoteIsNotNeededOnCreate()
    {
        var description = await ToolDescription("planning_upsert_artifact");

        description.ShouldContain("Editing a validated artifact requires a logNote — not needed on create.");
    }

    [Fact]
    public async Task PlanningUpsertArtifactDescription_CarriesDraftCreateAndValidationPath()
    {
        var description = await ToolDescription("planning_upsert_artifact");

        description.ShouldContain("created draft", Case.Insensitive);
        description.ShouldContain("validation goes through planning_validate", Case.Insensitive);
    }

    [Theory]
    [InlineData("planning_append_log", "cheapest write: one dated line, body is not resent")]
    [InlineData("planning_patch_artifact", "middle tier: replaces one XML section or ticks one acceptance item, rest of the body untouched")]
    [InlineData("planning_upsert_artifact", "most expensive write: resends the full body")]
    public async Task WriteToolDescriptions_StateTheirCostTierVerbatim(string toolName, string tierPhrase)
    {
        var description = await ToolDescription(toolName);

        description.ShouldContain(tierPhrase);
    }

    [Fact]
    public async Task PlanningPatchArtifactDescription_DocumentsTheIdempotentTick()
    {
        var description = await ToolDescription("planning_patch_artifact");

        description.ShouldContain("no-op");
        description.ShouldContain("retried tick is safe");
    }

    [Fact]
    public async Task PlanningPatchArtifactDescription_DocumentsTheAppendOnlyLog()
    {
        var description = await ToolDescription("planning_patch_artifact");

        description.ShouldContain("append-only");
        description.ShouldContain("planning_append_log");
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
