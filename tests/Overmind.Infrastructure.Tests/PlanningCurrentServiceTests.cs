using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningCurrentServiceTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;

    public PlanningCurrentServiceTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();

        var project = Project.Create($"planning-current-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.SaveChanges();
        _projectId = project.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task MissingProject_ThrowsNotFound()
    {
        var exception = await Should.ThrowAsync<PlanningException>(
            () => Service().GetCurrentAsync(Guid.NewGuid(), "feat"));

        exception.Kind.ShouldBe(PlanningFailureKind.NotFound);
        exception.Code.ShouldBe(PlanningFailureCode.MissingProject);
    }

    [Fact]
    public async Task MissingFeature_ThrowsNotFound()
    {
        var exception = await Should.ThrowAsync<PlanningException>(
            () => Service().GetCurrentAsync(_projectId, "nope"));

        exception.Kind.ShouldBe(PlanningFailureKind.NotFound);
        exception.Code.ShouldBe(PlanningFailureCode.MissingFeature);
    }

    [Fact]
    public async Task OpenFeatureBlocker_StopsNamingOnlyTheUnclosed()
    {
        await FeatureAsync("feat-a");
        await FeatureAsync("feat-b");
        await FeatureAsync("feat-c");
        await Dependencies().AddDependencyAsync(_projectId, "feat-a", "feat-b");
        await Dependencies().AddDependencyAsync(_projectId, "feat-a", "feat-c");
        await DemoteToReviewAsync("feat-c");

        var result = await Service().GetCurrentAsync(_projectId, "feat-a");

        result.State.ShouldBe("stop");
        result.Actionable.ShouldBeNull();
        result.Reason.ShouldBe(
            "STOP: feature 'feat-a' is blocked by unclosed features: feat-b (draft). " +
            "Reads stay open; task issuance stops until every blocker closes (done or review). " +
            "Tasks marked independent at upsert still issue while the blocker holds.");
        result.Feature.Slug.ShouldBe("feat-a");
    }

    [Fact]
    public async Task OpenBlocker_IndependentHeadTask_IssuesItWithBypassReason()
    {
        var feature = await FeatureAsync("feat-a");
        await FeatureAsync("feat-b");
        await Dependencies().AddDependencyAsync(_projectId, "feat-a", "feat-b");
        var head = await TaskAsync(feature.Id, 0);
        head.SetIndependent(true);
        await TaskAsync(feature.Id, 1);
        await _context.SaveChangesAsync();

        var result = await Service().GetCurrentAsync(_projectId, "feat-a");

        result.State.ShouldBe("task");
        result.Actionable!.Id.ShouldBe(head.Id);
        result.Reason.ShouldContain("feat-b (draft)");
        result.Reason.ShouldContain("marked independent — the author opted in at upsert");
        result.Reason.ShouldContain("task-level blockedBy edges keep their force");
    }

    [Fact]
    public async Task OpenBlocker_NonIndependentHead_StopsUnchanged()
    {
        var feature = await FeatureAsync("feat-a");
        await FeatureAsync("feat-b");
        await Dependencies().AddDependencyAsync(_projectId, "feat-a", "feat-b");
        await TaskAsync(feature.Id, 0);

        var result = await Service().GetCurrentAsync(_projectId, "feat-a");

        result.State.ShouldBe("stop");
        result.Actionable.ShouldBeNull();
        result.Reason.ShouldEndWith("Tasks marked independent at upsert still issue while the blocker holds.");
    }

    [Fact]
    public async Task OpenBlocker_IndependentTaskWithOwnUnclosedEdge_Waits()
    {
        var feature = await FeatureAsync("feat-a");
        await FeatureAsync("feat-b");
        await Dependencies().AddDependencyAsync(_projectId, "feat-a", "feat-b");
        var tail = await TaskAsync(feature.Id, 1);
        var head = await TaskAsync(feature.Id, 0);
        head.SetIndependent(true);
        head.ReplaceBlockedEdges([tail.Id]);
        await _context.SaveChangesAsync();

        var result = await Service().GetCurrentAsync(_projectId, "feat-a");

        result.State.ShouldBe("stop");
        result.Actionable.ShouldBeNull();
        result.Reason.ShouldContain("blocked by unclosed features");
    }

    [Fact]
    public async Task ClosedBlockersOnly_BehavesAsUnblocked()
    {
        var feature = await FeatureAsync("feat-a");
        await FeatureAsync("feat-b");
        await Dependencies().AddDependencyAsync(_projectId, "feat-a", "feat-b");
        await DemoteToReviewAsync("feat-b");

        var result = await Service().GetCurrentAsync(_projectId, "feat-a");

        result.State.ShouldBe("empty");
        result.Reason.ShouldBe(
            "No tasks yet — decompose the validated plan via planning_upsert_tasks: each task an execution contract (goal, scope, anti-scope, acceptance-criteria, refs).");
    }

    [Fact]
    public async Task GateOn_WithoutSpec_StopsPinningTheApprovalFlow()
    {
        await FeatureAsync("feat");
        await TurnGateOnAsync();

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.State.ShouldBe("stop");
        result.Actionable.ShouldBeNull();
        result.Reason.ShouldBe(
            "STOP: spec awaits human validation — it cannot be approved through MCP. " +
            "Send overmind_notify(kind=approval, featureSlug=feat) and wait for the human's reply — validation is never agent-approvable.");
    }

    [Fact]
    public async Task GateOn_DraftSpec_StopsWithTheSpecReason()
    {
        var feature = await FeatureAsync("feat");
        await DraftSpecAsync(feature.Id);
        await TurnGateOnAsync();

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.State.ShouldBe("stop");
        result.Reason.ShouldBe(
            "STOP: spec awaits human validation — it cannot be approved through MCP. " +
            "Send overmind_notify(kind=approval, featureSlug=feat) and wait for the human's reply — validation is never agent-approvable.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GateOn_ValidatedSpec_PlanMissingOrDraft_StopsWithThePlanReason(bool draftPlan)
    {
        var feature = await FeatureAsync("feat");
        var spec = await DraftSpecAsync(feature.Id);
        if (draftPlan)
        {
            await DraftPlanAsync(feature.Id);
        }

        spec.Validate();
        await _context.SaveChangesAsync();
        await TurnGateOnAsync();

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.State.ShouldBe("stop");
        result.Reason.ShouldBe(
            "STOP: plan awaits human validation — it cannot be approved through MCP. " +
            "Send overmind_notify(kind=approval, featureSlug=feat) and wait for the human's reply — validation is never agent-approvable.");
    }

    [Fact]
    public async Task GateOn_SpecAndPlanValidated_EmptyTellsToDecompose()
    {
        var feature = await FeatureAsync("feat");
        var spec = await DraftSpecAsync(feature.Id);
        var plan = await DraftPlanAsync(feature.Id);
        spec.Validate();
        plan.Validate();
        await _context.SaveChangesAsync();
        await TurnGateOnAsync();

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.State.ShouldBe("empty");
        result.Actionable.ShouldBeNull();
        result.Reason.ShouldBe(
            "No tasks yet — decompose the validated plan via planning_upsert_tasks: each task an execution contract (goal, scope, anti-scope, acceptance-criteria, refs).");
    }

    [Fact]
    public async Task GateOff_SkipsTheSpecPlanStops()
    {
        var feature = await FeatureAsync("feat");
        await DraftSpecAsync(feature.Id);
        await DraftPlanAsync(feature.Id);

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.State.ShouldBe("empty");
        result.Reason.ShouldBe(
            "No tasks yet — decompose the validated plan via planning_upsert_tasks: each task an execution contract (goal, scope, anti-scope, acceptance-criteria, refs).");
    }

    [Fact]
    public async Task AllTasksDone_WithoutResult_TellsToWriteIt()
    {
        var feature = await FeatureAsync("feat");
        await SettleTasksAsync(feature, ("task 1", PlanningArtifactStatus.Done));

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.State.ShouldBe("empty");
        result.Actionable.ShouldBeNull();
        result.Reason.ShouldBe("All tasks are done — write the result artifact via planning_upsert_artifact(kind=result).");
    }

    [Fact]
    public async Task AllTasksDone_DraftResult_TellsToValidateIt()
    {
        var feature = await FeatureAsync("feat");
        await SettleTasksAsync(feature, ("task 1", PlanningArtifactStatus.Done));
        _context.PlanningArtifacts.Add(
            PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Result, "close-out"));
        await _context.SaveChangesAsync();

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.Reason.ShouldBe(
            "Result artifact drafted — validate it via the proof flow: " +
            "send overmind_notify(kind=approval, featureSlug=feat) and wait for the human's reply.");
    }

    [Fact]
    public async Task AllTasksDone_ValidatedResult_TellsToCloseTheFeature()
    {
        var feature = await FeatureAsync("feat");
        await SettleTasksAsync(feature, ("task 1", PlanningArtifactStatus.Done));
        var resultArtifact = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Result, "close-out");
        _context.PlanningArtifacts.Add(resultArtifact);
        resultArtifact.Validate();
        await _context.SaveChangesAsync();

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.Reason.ShouldBe("Result validated — closing the feature needs the human's done tap on the board.");
    }

    [Fact]
    public async Task ReturnsTheFirstOpenTaskByOrder()
    {
        var feature = await FeatureAsync("feat");
        await SettleTasksAsync(feature, ("task 1", PlanningArtifactStatus.Done), ("task 2", PlanningArtifactStatus.Todo));

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.State.ShouldBe("task");
        result.Reason.ShouldBeNull();
        result.Actionable!.Order.ShouldBe(2);
        result.Warnings.ShouldBe(["spec missing", "plan not validated"]);
        result.BlockedStates.ShouldNotBeNull();
        result.BlockedStates![2].Blocked.ShouldBeFalse();
    }

    [Fact]
    public async Task SkipsBlockedTask_ForTheNextActionable()
    {
        var feature = await FeatureAsync("feat");
        var blocker = await TaskAsync(feature.Id, 1);
        var held = await TaskAsync(feature.Id, 2);
        await TaskAsync(feature.Id, 3);
        held.ReplaceBlockedEdges([blocker.Id]);
        blocker.SetStatus(PlanningArtifactStatus.Blocked);
        await _context.SaveChangesAsync();

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.State.ShouldBe("task");
        result.Reason.ShouldBeNull();
        result.Actionable!.Order.ShouldBe(3);
    }

    [Fact]
    public async Task EveryActionableTaskBlocked_StopsWithTheUnclosedChain()
    {
        var feature = await FeatureAsync("feat");
        var blocker = await TaskAsync(feature.Id, 1);
        var held = await TaskAsync(feature.Id, 2);
        held.ReplaceBlockedEdges([blocker.Id]);
        blocker.SetStatus(PlanningArtifactStatus.Blocked);
        await _context.SaveChangesAsync();

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.State.ShouldBe("stop");
        result.Actionable.ShouldBeNull();
        result.Reason.ShouldBe(
            "STOP: every actionable task is blocked by unclosed tasks: task 2 blocked by [1]. " +
            "Reads stay open; task issuance stops until every blocker closes (done or review).");
    }

    [Fact]
    public async Task EveryActionableTaskBlocked_MultiCandidateChainNamesUnclosedOnly()
    {
        var feature = await FeatureAsync("feat");
        var closed = await TaskAsync(feature.Id, 1);
        var mixed = await TaskAsync(feature.Id, 2);
        var open = await TaskAsync(feature.Id, 3);
        var chained = await TaskAsync(feature.Id, 4);
        mixed.ReplaceBlockedEdges([closed.Id, open.Id]);
        chained.ReplaceBlockedEdges([open.Id]);
        closed.SetStatus(PlanningArtifactStatus.Done);
        open.SetStatus(PlanningArtifactStatus.Blocked);
        await _context.SaveChangesAsync();

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.State.ShouldBe("stop");
        result.Actionable.ShouldBeNull();
        result.Reason.ShouldBe(
            "STOP: every actionable task is blocked by unclosed tasks: " +
            "task 2 blocked by [3], task 4 blocked by [3]. " +
            "Reads stay open; task issuance stops until every blocker closes (done or review).");
    }

    [Fact]
    public async Task TheNextTaskBlocked_ReturnsTaskWithReason()
    {
        var feature = await FeatureAsync("feat");
        await SettleTasksAsync(
            feature,
            ("task 1", PlanningArtifactStatus.Done),
            ("task 2", PlanningArtifactStatus.Blocked),
            ("task 3", PlanningArtifactStatus.Blocked));

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.State.ShouldBe("task");
        result.Actionable!.Order.ShouldBe(2);
        result.Actionable.Status.ShouldBe(PlanningArtifactStatus.Blocked);
        result.Reason.ShouldBe("The next task is blocked.");
        result.Warnings.ShouldBe(["spec missing", "plan not validated"]);
        result.BlockedStates.ShouldBeNull();
    }

    [Fact]
    public async Task OnlyReviewTasksLeft_ReturnsHeld()
    {
        var feature = await FeatureAsync("feat");
        await SettleTasksAsync(feature, ("task 1", PlanningArtifactStatus.Review), ("task 2", PlanningArtifactStatus.Review));

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.State.ShouldBe("held");
        result.Actionable.ShouldBeNull();
        result.Reason.ShouldBe("2 in review — finish the reviews, then close each task with planning_set_status.");
    }

    [Fact]
    public async Task WorkableTodoBeatsHeld()
    {
        var feature = await FeatureAsync("feat");
        await SettleTasksAsync(feature, ("task 1", PlanningArtifactStatus.Review), ("task 2", PlanningArtifactStatus.Todo));

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.State.ShouldBe("task");
        result.Reason.ShouldBeNull();
        result.Actionable!.Order.ShouldBe(2);
    }

    [Fact]
    public async Task ValidatedSpecAndPlan_ClearTheWarnings()
    {
        var feature = await FeatureAsync("feat");
        var spec = await DraftSpecAsync(feature.Id);
        var plan = await DraftPlanAsync(feature.Id);
        spec.Validate();
        plan.Validate();
        await _context.SaveChangesAsync();
        await TaskAsync(feature.Id, 1);

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.State.ShouldBe("task");
        result.Warnings.ShouldBeNull();
    }

    [Fact]
    public async Task OpenDirectives_FlowIntoTheFeatureView()
    {
        await FeatureAsync("feat");
        await Directives().AppendAsync(
            _projectId, "feat", "HOLD: shared file inbound",
            new DirectiveAuthor("Marshal", "wave-marshal", Guid.NewGuid()));

        var result = await Service().GetCurrentAsync(_projectId, "feat");

        result.Feature.Status.ShouldBe(PlanningFeatureStatus.Draft);
        result.Feature.OpenDirectives.Select(d => d.Text).ShouldBe(["HOLD: shared file inbound"]);
    }

    private PlanningCurrentService Service() => new(
        _context,
        new PlanningPolicy(_context, new AppSettingsService(_context)),
        new FeatureDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)),
        new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)),
        new DirectiveService(_context));

    private FeatureDependencyService Dependencies() =>
        new(_context, TestPlanningDispatcher.Create(_context, _materializer));

    private DirectiveService Directives() => new(_context);

    private async Task<PlanningFeature> FeatureAsync(string slug)
    {
        var feature = PlanningFeature.Create(_projectId, slug, slug, "body");
        _context.PlanningFeatures.Add(feature);
        await _context.SaveChangesAsync();
        return feature;
    }

    private async Task<PlanningArtifact> TaskAsync(Guid featureId, int order)
    {
        var task = PlanningArtifact.Create(featureId, PlanningArtifactKind.Task, $"task {order}", order);
        _context.PlanningArtifacts.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }

    private async Task<PlanningArtifact> DraftSpecAsync(Guid featureId)
    {
        var spec = PlanningArtifact.Create(featureId, PlanningArtifactKind.Spec, "spec");
        _context.PlanningArtifacts.Add(spec);
        await _context.SaveChangesAsync();
        return spec;
    }

    private async Task<PlanningArtifact> DraftPlanAsync(Guid featureId)
    {
        var plan = PlanningArtifact.Create(featureId, PlanningArtifactKind.Plan, "plan");
        _context.PlanningArtifacts.Add(plan);
        await _context.SaveChangesAsync();
        return plan;
    }

    private async Task SettleTasksAsync(PlanningFeature feature, params (string Body, PlanningArtifactStatus Status)[] tasks)
    {
        for (var i = 0; i < tasks.Length; i++)
        {
            var task = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Task, tasks[i].Body, order: i + 1);
            task.SetStatus(tasks[i].Status);
            _context.PlanningArtifacts.Add(task);
        }

        await _context.SaveChangesAsync();
    }

    private async Task DemoteToReviewAsync(string slug)
    {
        var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == slug);
        feature.SetStatus(PlanningFeatureStatus.Review);
        await _context.SaveChangesAsync();
    }

    private async Task TurnGateOnAsync()
    {
        var project = await _context.Projects.SingleAsync(p => p.Id == _projectId);
        project.SetPlanningWorkflow(PlanningMaterialization.Inherit, HumanValidationOverride.On);
        await _context.SaveChangesAsync();
    }
}
