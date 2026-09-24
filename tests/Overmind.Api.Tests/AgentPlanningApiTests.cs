using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentPlanningApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AgentPlanningApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Upsert_SpecFirstCallCreatesSecondCallVersions()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var first = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"), new { Kind = "spec", Body = "v1" });
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await first.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        created!.Version.ShouldBe(1);

        var second = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"), new { Kind = "spec", Body = "v2" });
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await second.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        updated!.Version.ShouldBe(2);

        var versions = await _client.GetFromJsonAsync<VersionJson[]>(
            $"/api/planning/artifacts/{created.Id}/versions", JsonOptions);
        versions.ShouldNotBeNull();
        versions!.Length.ShouldBe(1);
        versions[0].Version.ShouldBe(1);
        versions[0].Body.ShouldBe("v1");
    }

    [Fact]
    public async Task Upsert_FrozenSpecWithoutLogNote_Returns409()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "v1" });
        await FreezeViaHumanDoorAsync(spec.Id);

        var response = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"), new { Kind = "spec", Body = "v2" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("ChangeNoteRequired");
        problem.Detail!.ShouldContain("logNote");
    }

    [Fact]
    public async Task Upsert_FrozenSpecWithLogNote_Returns200AndResetsToDraft()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "v1" });
        await FreezeViaHumanDoorAsync(spec.Id);

        var response = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"), new { Kind = "spec", Body = "v2", LogNote = "unfreezing" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        updated!.Status.ShouldBe("draft");
        updated.FrozenAt.ShouldBeNull();
        updated.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Upsert_TaskByExplicitOrder_CreatesDistinctAndUpdatesInPlace()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var first = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task A", Order = 5 });
        var second = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task B", Order = 7 });
        first.Id.ShouldNotBe(second.Id);

        var updateResponse = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"), new { Kind = "task", Body = "task A v2", Order = 5 });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        updated!.Id.ShouldBe(first.Id);
        updated.Version.ShouldBe(2);

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/agent/projects/{project.Id}/planning/features/feat", JsonOptions);
        var tasks = feature!.Artifacts.Where(a => a.Kind == "task").ToArray();
        tasks.Length.ShouldBe(2);
    }

    [Fact]
    public async Task Upsert_TaskWithBrief_RoundTripsThroughTheAgentDoor()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var task = await CreateAgentArtifactAsync(project.Id, "feat",
            new { Kind = "task", Body = "task A", Brief = "Wire the polling hook" });

        task.Brief.ShouldBe("Wire the polling hook");
    }

    [Theory]
    [InlineData("spec")]
    [InlineData("plan")]
    [InlineData("result")]
    public async Task Upsert_BriefOnNonTaskKind_Returns400(string kind)
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"), new { Kind = kind, Body = "# Doc", Brief = "not allowed" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("BriefNotAllowedForKind");
    }

    [Fact]
    public async Task Validate_GateOn_Returns409HumanValidationRequired_WritesNothing()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await SetValidationGateAsync(project.Id, on: true);
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });

        var response = await _client.PostAsync(AgentValidateUrl(spec.Id), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("HumanValidationRequired");
        problem.Detail.ShouldBe(
            "STOP: the spec of feature 'feat' awaits human validation — ask the user to approve it in the Overmind UI.");

        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{spec.Id}", JsonOptions);
        artifact!.Status.ShouldBe("draft");
        artifact.FrozenAt.ShouldBeNull();
    }

    [Fact]
    public async Task Validate_ProjectOffBeatsGlobalOn_Returns200Validated()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var settings = await _client.PutAsJsonAsync(
            "/api/settings", new { PlanningRequireHumanValidationDefault = "on" });
        settings.StatusCode.ShouldBe(HttpStatusCode.OK);
        await SetValidationGateAsync(project.Id, on: false);

        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });

        var response = await _client.PostAsync(AgentValidateUrl(spec.Id), null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{spec.Id}", JsonOptions);
        artifact!.Status.ShouldBe("validated");
        artifact.FrozenAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Validate_GateOn_AsMigratedWithLogNote_ValidatesAndLeavesMarkerLine()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await SetValidationGateAsync(project.Id, on: true);
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });

        var response = await _client.PostAsJsonAsync(
            AgentValidateUrl(spec.Id),
            new { AsMigrated = true, LogNote = "frozen v9 on disk, user-approved" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{spec.Id}", JsonOptions);
        artifact!.Status.ShouldBe("validated");
        artifact.FrozenAt.ShouldNotBeNull();
        artifact.Body.ShouldContain("[agent] — validated as migrated — pre-approved on disk: frozen v9 on disk, user-approved");
    }

    [Fact]
    public async Task Validate_GateOn_AsMigratedWithoutLogNote_Returns400AndWritesNothing()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await SetValidationGateAsync(project.Id, on: true);
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });

        var response = await _client.PostAsJsonAsync(AgentValidateUrl(spec.Id), new { AsMigrated = true });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("MigratedNoteRequired");
        problem.Detail.ShouldContain("logNote");

        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{spec.Id}", JsonOptions);
        artifact!.Status.ShouldBe("draft");
        artifact.FrozenAt.ShouldBeNull();
    }

    [Fact]
    public async Task Current_GateOn_NoSpec_ReturnsStop()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await SetValidationGateAsync(project.Id, on: true);

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("stop");
        current.Artifact.ShouldBeNull();
        current.Reason.ShouldBe("STOP: spec awaits human validation — it cannot be approved through MCP.");
        current.Feature.Slug.ShouldBe("feat");
    }

    [Fact]
    public async Task Current_GateOn_SpecDraft_ReturnsStop()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await SetValidationGateAsync(project.Id, on: true);
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("stop");
        current.Reason.ShouldBe("STOP: spec awaits human validation — it cannot be approved through MCP.");
    }

    [Fact]
    public async Task Current_GateOn_SpecValidatedPlanMissing_ReturnsStop()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await SetValidationGateAsync(project.Id, on: true);
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });
        await FreezeViaHumanDoorAsync(spec.Id);

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("stop");
        current.Artifact.ShouldBeNull();
        current.Reason.ShouldBe("STOP: plan awaits human validation — it cannot be approved through MCP.");
    }

    [Fact]
    public async Task Current_GateOn_PlanDraft_ReturnsStop()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await SetValidationGateAsync(project.Id, on: true);
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });
        await FreezeViaHumanDoorAsync(spec.Id);
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "plan", Body = "# Plan" });

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("stop");
        current.Reason.ShouldBe("STOP: plan awaits human validation — it cannot be approved through MCP.");
    }

    [Fact]
    public async Task Current_GateOn_SpecAndPlanValidatedNoTasks_ReturnsEmpty()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await SetValidationGateAsync(project.Id, on: true);
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });
        await FreezeViaHumanDoorAsync(spec.Id);
        var plan = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "plan", Body = "# Plan" });
        await FreezeViaHumanDoorAsync(plan.Id);

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("empty");
        current.Artifact.ShouldBeNull();
        current.Reason.ShouldBe("No tasks yet — create them from the validated plan.");
    }

    [Fact]
    public async Task Current_GateOff_SkipsSpecPlanStop()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("empty");
        current.Reason.ShouldBe("No tasks yet — create them from the validated plan.");
    }

    [Fact]
    public async Task Current_AllTasksDone_ReturnsEmpty()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var first = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1", Order = 1 });
        var second = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 2", Order = 2 });
        await SetTaskStatusAsync(first.Id, "done");
        await SetTaskStatusAsync(second.Id, "done");

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("empty");
        current.Artifact.ShouldBeNull();
        current.Reason.ShouldBe("All tasks are done — write the result artifact.");
    }

    [Fact]
    public async Task Current_ReturnsFirstOpenTaskByOrder()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var first = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1", Order = 1 });
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 2", Order = 2 });
        await SetTaskStatusAsync(first.Id, "done");

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("task");
        current.Reason.ShouldBeNull();
        current.Artifact.ShouldNotBeNull();
        current.Artifact!.Order.ShouldBe(2);
        current.Artifact.Status.ShouldBe("todo");
    }

    [Fact]
    public async Task Current_SkipsBlockedTask_ForLaterActionable()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var blocked = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1", Order = 1 });
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 2", Order = 2 });
        await SetTaskStatusAsync(blocked.Id, "blocked");

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("task");
        current.Reason.ShouldBeNull();
        current.Artifact!.Order.ShouldBe(2);
    }

    [Fact]
    public async Task Current_AllRemainingBlocked_ReturnsBlockedWithReason()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var first = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1", Order = 1 });
        var second = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 2", Order = 2 });
        var third = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 3", Order = 3 });
        await SetTaskStatusAsync(first.Id, "done");
        await SetTaskStatusAsync(second.Id, "blocked");
        await SetTaskStatusAsync(third.Id, "blocked");

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("task");
        current.Artifact!.Order.ShouldBe(2);
        current.Reason.ShouldBe("The next task is blocked.");
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("ready")]
    [InlineData("inProgress")]
    [InlineData("review")]
    public async Task SetFeatureStatus_DoneFromNonDoneStatus_AppliesAndLogsProvenance(string start)
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await BringToStatusViaHumanDoorAsync(project.Id, "feat", start);

        var response = await _client.PostAsJsonAsync(
            AgentFeatureStatusUrl(project.Id, "feat"),
            new { Status = "done", LogNote = "by user request in-session" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var feature = await response.Content.ReadFromJsonAsync<FeatureJson>(JsonOptions);
        feature!.Status.ShouldBe("done");
        feature.Body.ShouldContain("by user request in-session");
    }

    [Fact]
    public async Task SetFeatureStatus_ReviewRollbackToInProgress_Applies()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await BringToStatusViaHumanDoorAsync(project.Id, "feat", "review");

        var response = await _client.PostAsJsonAsync(
            AgentFeatureStatusUrl(project.Id, "feat"),
            new { Status = "inProgress", LogNote = "review findings need fixes" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var feature = await response.Content.ReadFromJsonAsync<FeatureJson>(JsonOptions);
        feature!.Status.ShouldBe("inProgress");
        feature.Body.ShouldContain("review findings need fixes");
    }

    [Fact]
    public async Task SetFeatureStatus_AlreadyAtTarget_ReturnsOkWithoutAnotherLogLine()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await BringToStatusViaHumanDoorAsync(project.Id, "feat", "inProgress");

        var response = await _client.PostAsJsonAsync(
            AgentFeatureStatusUrl(project.Id, "feat"),
            new { Status = "inProgress", LogNote = "by user request in-session" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var feature = await response.Content.ReadFromJsonAsync<FeatureJson>(JsonOptions);
        feature!.Status.ShouldBe("inProgress");
        feature.Body.ShouldNotContain("by user request in-session");
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("ready")]
    [InlineData("review")]
    public async Task SetFeatureStatus_NonAgentTarget_Returns400Typed(string target)
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentFeatureStatusUrl(project.Id, "feat"),
            new { Status = target, LogNote = "by user request in-session" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("FeatureStatusNotAllowedForAgent");
        problem.Detail!.ShouldContain("done");
        problem.Detail!.ShouldContain("inProgress");
    }

    [Fact]
    public async Task SetFeatureStatus_UnknownStatusValue_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentFeatureStatusUrl(project.Id, "feat"),
            new { Status = "banana", LogNote = "by user request in-session" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetFeatureStatus_MissingLogNote_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentFeatureStatusUrl(project.Id, "feat"),
            new { Status = "done", LogNote = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetFeatureStatus_TerminalDone_ReopenAttempt_Returns400CarryingCurrentStatus()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await BringToStatusViaHumanDoorAsync(project.Id, "feat", "done");

        var response = await _client.PostAsJsonAsync(
            AgentFeatureStatusUrl(project.Id, "feat"),
            new { Status = "inProgress", LogNote = "reopen by user request in-session" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("InvalidFeatureTransition");
        problem.Detail!.ShouldContain("from 'done'");
    }

    [Fact]
    public async Task SetFeatureStatus_UnknownFeature_Returns404()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PostAsJsonAsync(
            AgentFeatureStatusUrl(project.Id, "missing"),
            new { Status = "done", LogNote = "by user request in-session" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_ReplaceSection_ReturnsD7ProjectionAndRewritesTheBody()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "spec",
            Body = "# Spec\n\n<goal>\nOld goal.\n</goal>\n\n<scope>\nEverything.\n</scope>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(spec.Id),
            new { Op = "replace_section", Section = "goal", Content = "New goal." });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(raw).RootElement.EnumerateObject().Select(p => p.Name).ToArray()
            .ShouldBe(["artifactId", "kind", "section", "version", "bodyChars"]);

        var patch = await response.Content.ReadFromJsonAsync<PatchResultJson>(JsonOptions);
        patch!.ArtifactId.ShouldBe(spec.Id);
        patch.Kind.ShouldBe("spec");
        patch.Section.ShouldBe("goal");
        patch.Version.ShouldBe(2);

        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{spec.Id}", JsonOptions);
        artifact!.Body.ShouldContain("New goal.");
        artifact.Body.ShouldNotContain("Old goal.");
        artifact.Version.ShouldBe(2);
        patch.BodyChars.ShouldBe(artifact.Body.Length);
    }

    [Fact]
    public async Task Patch_TickAcByIndex_MarksTheItemAndProjectsTheAcSection()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Task\n\n<acceptance-criteria>\n- [ ] first thing\n- [ ] second thing\n</acceptance-criteria>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(task.Id),
            new { Op = "tick_ac", AcIndex = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var patch = await response.Content.ReadFromJsonAsync<PatchResultJson>(JsonOptions);
        patch!.ArtifactId.ShouldBe(task.Id);
        patch.Kind.ShouldBe("task");
        patch.Section.ShouldBe("acceptance-criteria");
        patch.Version.ShouldBe(2);

        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{task.Id}", JsonOptions);
        artifact!.Body.ShouldContain("- [ ] first thing");
        artifact.Body.ShouldContain("- [x] second thing");
    }

    [Fact]
    public async Task Patch_TickAcByText_MarksTheExactMatch()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Task\n\n<acceptance-criteria>\n- [ ] wire the endpoint\n- [ ] write the docs\n</acceptance-criteria>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(task.Id),
            new { Op = "tick_ac", AcText = "write the docs" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{task.Id}", JsonOptions);
        artifact!.Body.ShouldContain("- [x] write the docs");
        artifact.Body.ShouldContain("- [ ] wire the endpoint");
    }

    [Fact]
    public async Task Patch_UnknownSection_Returns400ListingPresentSections()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "spec",
            Body = "# Spec\n\n<goal>\nOld goal.\n</goal>\n\n<scope>\nEverything.\n</scope>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(spec.Id),
            new { Op = "replace_section", Section = "banana", Content = "nope" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("PatchSectionNotFound");
        problem.Detail!.ShouldContain("Sections present");
        problem.Detail!.ShouldContain("goal");
        problem.Detail!.ShouldContain("scope");
    }

    [Fact]
    public async Task Patch_LogSection_Returns400PointingAtAppendLog()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "spec",
            Body = "# Spec\n\n<goal>\nOld goal.\n</goal>\n\n<log>\n- 2026-09-01 — seeded\n</log>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(spec.Id),
            new { Op = "replace_section", Section = "log", Content = "- 2026-09-22 — forged history" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("PatchLogSectionNotReplaceable");
        problem.Detail!.ShouldContain("append-only");
        problem.Detail!.ShouldContain("planning_append_log");
        problem.Detail!.ShouldContain("planning_upsert_artifact");

        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{spec.Id}", JsonOptions);
        artifact!.Version.ShouldBe(1);
        artifact.Body.ShouldContain("- 2026-09-01 — seeded");
        artifact.Body.ShouldNotContain("forged history");
    }

    [Fact]
    public async Task Patch_TickAcOutOfRange_Returns400ListingVisibleItems()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Task\n\n<acceptance-criteria>\n- [ ] first thing\n- [ ] second thing\n</acceptance-criteria>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(task.Id),
            new { Op = "tick_ac", AcIndex = 7 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("TickItemNotFound");
        problem.Detail!.ShouldContain("0: [ ] first thing");
        problem.Detail!.ShouldContain("1: [ ] second thing");
    }

    [Fact]
    public async Task Patch_AmbiguousTickText_Returns400SuggestingTheIndex()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Task\n\n<acceptance-criteria>\n- [ ] ship it\n- [ ] ship it\n</acceptance-criteria>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(task.Id),
            new { Op = "tick_ac", AcText = "ship it" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("TickTextAmbiguous");
        problem.Detail!.ShouldContain("tick by index instead");
    }

    [Fact]
    public async Task Patch_ItemWithoutCheckbox_Returns400NotCheckable()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Task\n\n<acceptance-criteria>\n- a plain line\n</acceptance-criteria>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(task.Id),
            new { Op = "tick_ac", AcIndex = 0 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("TickItemNotCheckable");
        problem.Detail!.ShouldContain("cannot be ticked");
    }

    [Theory]
    [InlineData("replace_section", "   ", "content", null, null)]
    [InlineData("replace_section", "goal", "   ", null, null)]
    [InlineData("replace_section", "goal", null, null, null)]
    [InlineData("tick_ac", null, null, null, "")]
    [InlineData("tick_ac", null, null, null, "   ")]
    [InlineData("tick_ac", null, null, null, null)]
    [InlineData("tick_ac", null, null, 0, "first thing")]
    [InlineData("tick_ac", "goal", "content", null, null)]
    [InlineData("banana", "goal", "content", null, null)]
    [InlineData(null, "goal", "content", null, null)]
    public async Task Patch_MalformedShape_Returns400InvalidPatchRequest_Never500(
        string? op, string? section, string? content, int? acIndex, string? acText)
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "spec",
            Body = "# Spec\n\n<goal>\nOld goal.\n</goal>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(spec.Id),
            new { Op = op, Section = section, Content = content, AcIndex = acIndex, AcText = acText });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("InvalidPatchRequest");
    }

    [Fact]
    public async Task Read_ReturnsMetadataAndFullBodyByDefault()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Wire the API\n\n<goal>\nExpose the endpoints.\n</goal>\n\n<obstacles>\nNone yet.\n</obstacles>\n",
            Order = 2,
            Brief = "Wire the endpoints"
        });

        var response = await _client.GetAsync(AgentReadUrl(task.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(raw).RootElement.EnumerateObject().Select(p => p.Name).ToArray()
            .ShouldBe(["id", "kind", "status", "version", "order", "updatedAt", "brief", "body"]);

        var read = JsonSerializer.Deserialize<ArtifactReadJson>(raw, JsonOptions);
        read!.Id.ShouldBe(task.Id);
        read.Kind.ShouldBe("task");
        read.Status.ShouldBe("todo");
        read.Version.ShouldBe(1);
        read.Order.ShouldBe(2);
        read.UpdatedAt.ShouldNotBe(default);
        read.Brief.ShouldBe("Wire the endpoints");
        read.Body.ShouldBe("# Wire the API\n\n<goal>\nExpose the endpoints.\n</goal>\n\n<obstacles>\nNone yet.\n</obstacles>\n");
        read.MissingSections.ShouldBeNull();
    }

    [Theory]
    [InlineData("goal,obstacles")]
    [InlineData("goal obstacles")]
    [InlineData("goal\tobstacles")]
    public async Task Read_Sections_NarrowsTheBodyToExactlyThoseSections(string sections)
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Task\n\n<goal>\nModel it.\n</goal>\n\n<obstacles>\nA hard one.\n</obstacles>\n\n<acceptance-criteria>\n- [ ] ship it\n</acceptance-criteria>\n"
        });

        var response = await _client.GetAsync(
            $"{AgentReadUrl(task.Id)}?sections={Uri.EscapeDataString(sections)}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(raw).RootElement.EnumerateObject().Select(p => p.Name).ToArray()
            .ShouldBe(["id", "kind", "status", "version", "order", "updatedAt", "brief", "body", "missingSections"]);

        var read = JsonSerializer.Deserialize<ArtifactReadJson>(raw, JsonOptions);
        read!.Body.ShouldBe("<goal>\nModel it.\n</goal>\n<obstacles>\nA hard one.\n</obstacles>");
        read.MissingSections.ShouldBe([]);
    }

    [Fact]
    public async Task Read_SectionsWithAbsentNames_ReportsMissingSectionsSoftly()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Task\n\n<goal>\nModel it.\n</goal>\n"
        });

        var mixed = await _client.GetAsync($"{AgentReadUrl(task.Id)}?sections=goal,banana");
        mixed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var mixedRead = JsonSerializer.Deserialize<ArtifactReadJson>(
            await mixed.Content.ReadAsStringAsync(), JsonOptions);
        mixedRead!.Body.ShouldBe("<goal>\nModel it.\n</goal>");
        mixedRead.MissingSections.ShouldBe(["banana"]);

        var allMissing = await _client.GetAsync($"{AgentReadUrl(task.Id)}?sections=nope");
        allMissing.StatusCode.ShouldBe(HttpStatusCode.OK);
        var allMissingRead = JsonSerializer.Deserialize<ArtifactReadJson>(
            await allMissing.Content.ReadAsStringAsync(), JsonOptions);
        allMissingRead!.Body.ShouldBe("");
        allMissingRead.MissingSections.ShouldBe(["nope"]);
    }

    [Fact]
    public async Task Read_UnknownArtifact_Returns404()
    {
        var project = await CreateProjectAsync();
        var missing = Guid.NewGuid();

        var response = await _client.GetAsync(AgentReadUrl(missing));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("Not Found");
        problem.Detail.ShouldBe($"Artifact '{missing}' was not found.");
    }

    [Fact]
    public async Task BoardAndFeature_SameJsonShapeAsHumanDoor()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1" });

        var agentBoard = await GetRawAsync($"/api/agent/projects/{project.Id}/planning/features");
        var humanBoard = await GetRawAsync($"/api/projects/{project.Id}/planning/features");
        PropertyNames(agentBoard).ShouldBe(PropertyNames(humanBoard));

        var agentFeature = await GetRawAsync($"/api/agent/projects/{project.Id}/planning/features/feat");
        var humanFeature = await GetRawAsync($"/api/projects/{project.Id}/planning/features/feat");
        PropertyNames(agentFeature).ShouldBe(PropertyNames(humanFeature));

        var agentParsed = JsonDocument.Parse(agentFeature).RootElement;
        agentParsed.GetProperty("slug").GetString().ShouldBe("feat");
        agentParsed.GetProperty("artifacts").GetArrayLength().ShouldBe(2);
    }

    private static string AgentArtifactsUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts";

    private static string AgentValidateUrl(Guid artifactId) =>
        $"/api/agent/planning/artifacts/{artifactId}/validate";

    private static string AgentFeatureStatusUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/status";

    private static string AgentPatchUrl(Guid artifactId) =>
        $"/api/agent/planning/artifacts/{artifactId}/patch";

    private static string AgentReadUrl(Guid artifactId) =>
        $"/api/agent/planning/artifacts/{artifactId}";

    private async Task BringToStatusViaHumanDoorAsync(Guid projectId, string slug, string target)
    {
        string[] path = target switch
        {
            "draft" => [],
            "ready" => ["ready"],
            "inProgress" => ["inProgress"],
            "review" => ["inProgress", "review"],
            "done" => ["inProgress", "review", "done"],
            _ => throw new ArgumentException($"unknown status '{target}'"),
        };

        foreach (var step in path)
        {
            var response = await _client.PutAsJsonAsync(
                $"/api/projects/{projectId}/planning/features/{slug}",
                new { Status = step });
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    private async Task<CurrentJson> GetCurrentAsync(Guid projectId, string slug)
    {
        var response = await _client.GetAsync($"/api/agent/projects/{projectId}/planning/features/{slug}/current");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var current = await response.Content.ReadFromJsonAsync<CurrentJson>(JsonOptions);
        current.ShouldNotBeNull();
        return current!;
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var name = $"agent-planning-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task<FeatureJson> CreateAgentFeatureAsync(Guid projectId, string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var feature = await response.Content.ReadFromJsonAsync<FeatureJson>(JsonOptions);
        feature.ShouldNotBeNull();
        return feature!;
    }

    private async Task<ArtifactJson> CreateAgentArtifactAsync(Guid projectId, string slug, object body)
    {
        var response = await _client.PostAsJsonAsync(AgentArtifactsUrl(projectId, slug), body);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var artifact = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        artifact.ShouldNotBeNull();
        return artifact!;
    }

    private async Task SetValidationGateAsync(Guid projectId, bool on)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/workflow",
            new { RequireHumanValidation = on ? "on" : "off" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task SetTaskStatusAsync(Guid artifactId, string status)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/planning/artifacts/{artifactId}/status",
            new { Status = status, LogNote = $"marking {status}" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task FreezeViaHumanDoorAsync(Guid artifactId)
    {
        var response = await _client.PostAsync($"/api/planning/artifacts/{artifactId}/validate", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<string> GetRawAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    private static List<string> PropertyNames(string json)
    {
        var names = new List<string>();
        Walk(JsonDocument.Parse(json).RootElement, names);
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static void Walk(JsonElement element, List<string> names)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    names.Add(property.Name);
                    Walk(property.Value, names);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, names);
                }
                break;
        }
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record FeatureJson(
        string Slug, string Title, string Body, string Status,
        int Version, DateTime CreatedAt, DateTime UpdatedAt, ArtifactJson[] Artifacts);

    public sealed record ArtifactJson(
        Guid Id, string Kind, string Status, string Review,
        int Order, string Body, DateTime? FrozenAt, int Version,
        DateTime CreatedAt, DateTime UpdatedAt, LogLineJson[] Log, string? Brief = null);

    public sealed record LogLineJson(string? Date, string Text);

    public sealed record VersionJson(int Version, string Body, DateTime CreatedAt);

    public sealed record CurrentJson(
        string State, CurrentFeatureJson Feature, ArtifactJson? Artifact, string? Reason);

    public sealed record CurrentFeatureJson(string Slug, string Title, string Status);

    public sealed record PatchResultJson(
        Guid ArtifactId, string Kind, string Section, int Version, int BodyChars);

    public sealed record ArtifactReadJson(
        Guid Id, string Kind, string Status, int Version, int Order,
        DateTime UpdatedAt, string? Brief, string Body, string[]? MissingSections = null);

    public sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
