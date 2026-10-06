using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class WorkflowTeachingContractTests : McpSessionTestBase
{
    private static readonly Regex ToolLikeToken = new(
        @"\b(?:overmind|planning|issues)_[a-z_]+|list_projects|list_entries|read_entry",
        RegexOptions.Compiled);

    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        stub.RouteGet("/api/health", Canned.HealthJson);
        stub.RouteGet("/api/projects", "[]");
    }

    [Fact]
    public void Quickstart_TeachesEveryCeremonyStageWithItsTool()
    {
        var text = OnboardQuickstart.Text;

        text.ShouldContain("planning_create_feature");
        text.ShouldContain("planning_upsert_artifact kind=spec");
        text.ShouldContain("planning_upsert_artifact kind=plan");
        text.ShouldContain("planning_upsert_tasks");
        text.ShouldContain("each task an execution contract");
        text.ShouldContain("a sub-agent receives ONLY its own task artifact via planning_read_artifact");
        text.ShouldContain("the work loop — planning_current_task");
        text.ShouldContain("planning_set_status with a logNote");
        text.ShouldContain("review before a task closes");
        text.ShouldContain("planning_upsert_artifact kind=result");
    }

    [Fact]
    public void Quickstart_TeachesTheValidationProofFlowGate()
    {
        var text = OnboardQuickstart.Text;

        text.ShouldContain("the human validation gate — overmind_notify(kind=approval, featureSlug)");
        text.ShouldContain("wait for the human's reply, then planning_validate");
        text.ShouldContain("every edit voids the reply");
    }

    [Fact]
    public void Quickstart_NamesTheHumanDoneTap()
    {
        OnboardQuickstart.Text.ShouldContain("the done tap belongs to the human in the Overmind UI");
    }

    [Fact]
    public void Quickstart_KeepsTheOnboardingStepsUnchanged()
    {
        var text = OnboardQuickstart.Text;

        text.ShouldContain(
            "1. overmind_onboard — resolves your project from the reported cwd and returns the project id, board summary, and this quickstart.");
        text.ShouldContain("2. planning_list_features — the full board view; pick the feature to work on from planning_task_queue");
        text.ShouldContain(
            "3. planning_current_task — returns the single next task, or a stop/empty reason. Obey STOP lines; ask the user instead of working around them.");
    }

    [Fact]
    public void Quickstart_TeachesTheCeremonyStagesInExecutionOrder()
    {
        var text = OnboardQuickstart.Text;

        var create = RequirePhrase(text, "planning_create_feature");
        var spec = RequirePhrase(text, "planning_upsert_artifact kind=spec");
        var gate = RequirePhrase(text, "overmind_notify(kind=approval, featureSlug)");
        var reply = RequirePhrase(text, "wait for the human's reply");
        var validate = RequirePhrase(text, "then planning_validate; every edit voids the reply");
        var plan = RequirePhrase(text, "planning_upsert_artifact kind=plan");
        var tasks = RequirePhrase(text, "planning_upsert_tasks");
        var subAgentRead = RequirePhrase(text, "a sub-agent receives ONLY its own task artifact via planning_read_artifact");
        var loop = RequirePhrase(text, "the work loop — planning_current_task");
        var setStatus = RequirePhrase(text, "planning_set_status with a logNote");
        var result = RequirePhrase(text, "planning_upsert_artifact kind=result");

        create.ShouldBeLessThan(spec);
        spec.ShouldBeLessThan(gate);
        gate.ShouldBeLessThan(reply);
        reply.ShouldBeLessThan(validate);
        validate.ShouldBeLessThan(plan);
        plan.ShouldBeLessThan(tasks);
        tasks.ShouldBeLessThan(subAgentRead);
        subAgentRead.ShouldBeLessThan(loop);
        loop.ShouldBeLessThan(setStatus);
        setStatus.ShouldBeLessThan(result);
    }

    [Fact]
    public void Quickstart_MirrorsTheStatusModelPins()
    {
        var text = OnboardQuickstart.Text;

        text.ShouldContain("Feature lifecycle: draft → ready → inProgress → review → done.");
        text.ShouldContain("Review invariant: review ⟺ spec+plan validated AND all tasks done.");
        text.ShouldContain("Editing a validated spec or plan demotes it to draft");
        text.ShouldContain("planning_set_feature_status reaches done from ANY non-done status; done is terminal.");
        text.ShouldContain("Artifact statuses — spec/plan/result: draft, validated; task: todo, inProgress, review, done, blocked.");
        text.ShouldContain("Log lines carry [agent]/[ui]/[auto]/[import] markers saying who moved a status.");
        text.ShouldContain("planning_claim_feature the moment you take it into active work");
    }

    [Fact]
    public async Task Quickstart_TeachesNoPhantomToolNames()
    {
        var live = await LiveToolNamesAsync(Driver);

        AssertNoPhantomTools(OnboardQuickstart.Text, live, "quickstart");
    }

    [Fact]
    public async Task SessionBootstrapFallback_TeachesNoPhantomToolNames()
    {
        var prompt = await Driver.GetPromptAsync("session_bootstrap");
        var live = await LiveToolNamesAsync(Driver);

        AssertNoPhantomTools(PromptText(prompt), live, "session_bootstrap fallback branch");
    }

    [Fact]
    public async Task SessionBootstrapResolvedProject_TeachesNoPhantomToolNames()
    {
        using var driver = McpProcessDriver.Start(Stub.BaseUrl, new Dictionary<string, string>
        {
            ["OVERMIND_PROJECT_ID"] = Canned.ProjectId.ToString()
        });
        await driver.InitializeAsync();

        var prompt = await driver.GetPromptAsync("session_bootstrap");
        var live = await LiveToolNamesAsync(driver);

        AssertNoPhantomTools(PromptText(prompt), live, "session_bootstrap resolved-project branch");
    }

    private static int RequirePhrase(string text, string phrase)
    {
        text.ShouldContain(phrase);
        return text.IndexOf(phrase, StringComparison.Ordinal);
    }

    private static string PromptText(JsonNode prompt) =>
        prompt["result"]!["messages"]!.AsArray().First()!["content"]!["text"]!.GetValue<string>();

    private static async Task<HashSet<string>> LiveToolNamesAsync(McpProcessDriver driver)
    {
        var response = await driver.ListToolsAsync();

        return response["result"]!["tools"]!.AsArray()
            .Select(tool => tool!["name"]!.GetValue<string>())
            .ToHashSet();
    }

    private static void AssertNoPhantomTools(string text, IReadOnlySet<string> live, string surface)
    {
        var taught = ToolLikeToken.Matches(text)
            .Select(match => match.Value)
            .Distinct()
            .ToList();
        taught.ShouldNotBeEmpty($"{surface} must teach at least one tool name");

        var phantoms = taught.Where(tool => !live.Contains(tool)).ToList();
        phantoms.ShouldBeEmpty($"{surface} teaches tool names missing from the live tools list: {string.Join(", ", phantoms)}");
    }
}
