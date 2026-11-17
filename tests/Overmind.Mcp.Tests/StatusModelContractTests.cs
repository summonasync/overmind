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
    public void Quickstart_CoversTheClaimsWorkflow()
    {
        var text = OnboardQuickstart.Text;

        text.ShouldContain("planning_claim_feature the moment you take it into active work");
        text.ShouldContain("before the first feature mutation");
        text.ShouldContain("sub-agents inherit the lock through the shared adapter session");
        text.ShouldContain("must not claim or unclaim themselves");
        text.ShouldContain("the error names the holder");
        text.ShouldContain("stale or ended → take-over is legitimate");
        text.ShouldContain("Claims release themselves — a dead adapter process");
        text.ShouldContain("unclaim when you hand off or close the wave");
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

        description.ShouldContain("logNote is mandatory");
        ContractsDoc.Section("planning_set_status").ShouldContain("logNote is mandatory on every move");
    }

    [Fact]
    public async Task PlanningSetStatusDescription_StatesTheFreeFormTransitionModel()
    {
        var description = await ToolDescription("planning_set_status");

        description.ShouldContain("Moves an artifact status");
        ContractsDoc.Section("planning_set_status").ShouldContain("Transitions are free-form");
        ContractsDoc.Section("planning_set_status").ShouldContain("no enforced sequence");
    }

    [Fact]
    public async Task PlanningSetStatusDescription_WarnsAboutUntickedAcOnDoneWithoutBlocking()
    {
        var description = await ToolDescription("planning_set_status");

        description.ShouldContain("tasks todo, inProgress, review, done or blocked");
        ContractsDoc.Section("planning_set_status").ShouldContain("non-blocking warning counting them");
    }

    [Fact]
    public void PlanningSetStatusDescription_DocumentsTheValidationProofGate()
    {
        var section = ContractsDoc.Section("planning_set_status");

        section.ShouldContain("carries the same proof requirement as");
        section.ShouldContain("newer than the artifact's last content update");
        section.ShouldContain("ValidationProofRequired");
        section.ShouldContain("Task and result moves never require proof");
    }

    [Fact]
    public void PlanningSetStatusDescription_StatesTheReviewReEvaluationOnValidated()
    {
        var section = ContractsDoc.Section("planning_set_status");

        section.ShouldContain("re-evaluates the review invariant");
        section.ShouldContain("exactly one [auto] promotion line");
    }

    [Fact]
    public void PlanningSetStatusDescription_DocumentsTheAppendedLogLineShape()
    {
        var section = ContractsDoc.Section("planning_set_status");

        section.ShouldContain("appends one line to the artifact's <log> section");
        section.ShouldContain("- YYYY-MM-DD [agent] — <logNote>");
    }

    [Fact]
    public async Task PlanningUpsertArtifactDescription_SaysLogNoteIsNotNeededOnCreate()
    {
        var response = await Driver.ListToolsAsync();

        var tool = response["result"]!["tools"]!.AsArray()
            .Single(t => t!["name"]!.GetValue<string>() == "planning_upsert_artifact");
        tool!["inputSchema"]!["properties"]!["logNote"]!["description"]!.GetValue<string>()
            .ShouldBe("Required when editing a validated artifact.");

        ContractsDoc.Section("planning_upsert_artifact")
            .ShouldContain("Editing a validated artifact requires logNote — creating one does not");
    }

    [Fact]
    public async Task PlanningUpsertArtifactDescription_CarriesDraftCreateAndValidationPath()
    {
        var description = await ToolDescription("planning_upsert_artifact");

        description.ShouldContain("Creates an artifact or replaces its whole body");
        ContractsDoc.Section("planning_upsert_artifact").ShouldContain("created draft");
        ContractsDoc.Section("planning_upsert_artifact").ShouldContain("validation goes through planning_validate");
    }

    [Theory]
    [InlineData("planning_append_log", "the cheapest write")]
    [InlineData("planning_patch_artifact", "The middle-tier write")]
    [InlineData("planning_upsert_artifact", "the most expensive write")]
    public async Task WriteToolDescriptions_StateTheirCostTierVerbatim(string toolName, string tierPhrase)
    {
        var description = await ToolDescription(toolName);

        description.ShouldContain(tierPhrase);
    }

    [Fact]
    public async Task PlanningEditLogDescription_StatesItsPriceInTheContractsDoc()
    {
        var description = await ToolDescription("planning_edit_log");

        description.ShouldContain("newest log line");
        ContractsDoc.Section("planning_edit_log").ShouldContain("one line changes, the body is not resent");
    }

    [Fact]
    public void PlanningPatchArtifactDescription_DocumentsTheIdempotentTick()
    {
        var section = ContractsDoc.Section("planning_patch_artifact");

        section.ShouldContain("already in the requested state is a successful no-op");
        section.ShouldContain("already ticked — no change");
    }

    [Fact]
    public void PlanningPatchArtifactDescription_DocumentsTheAppendOnlyLog()
    {
        var section = ContractsDoc.Section("planning_patch_artifact");

        section.ShouldContain("The log section is append-only");
        section.ShouldContain("a 400 pointing at planning_append_log");
    }

    private static string[] AllowedStatusNames(PlanningArtifactKind kind) =>
        PlanningArtifact.AllowedStatuses(kind)
            .Select(status => status.ToString().ToLowerInvariant())
            .ToArray();

    private static string[] ListedStatusNames(string text, PlanningArtifactKind kind)
    {
        var markers = kind == PlanningArtifactKind.Task
            ? new[] { "task:", "tasks " }
            : new[] { "spec/plan/result:", "spec/plan/result take " };
        var marker = markers.FirstOrDefault(candidate => text.Contains(candidate, StringComparison.Ordinal));
        var start = marker is null ? -1 : text.IndexOf(marker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"status listing for kind '{kind}' is missing from: {text}");

        var listing = text[(start + marker!.Length)..];
        var ends = new[]
        {
            listing.IndexOf(';'),
            listing.IndexOf('.'),
            listing.IndexOf(", tasks", StringComparison.Ordinal),
        };
        var end = ends.Where(offset => offset >= 0).DefaultIfEmpty(-1).Min();
        if (end >= 0)
        {
            listing = listing[..end];
        }

        return listing
            .Replace(" or ", ", ")
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
