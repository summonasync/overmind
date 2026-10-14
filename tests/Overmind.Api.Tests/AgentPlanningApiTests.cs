using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        var frozen = await _client.GetFromJsonAsync<ArtifactJson>(AgentReadUrl(spec.Id), JsonOptions);

        var response = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"),
            new { Kind = "spec", Body = frozen!.Body.Replace("v1", "v2") });

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
        var frozen = await _client.GetFromJsonAsync<ArtifactJson>(AgentReadUrl(spec.Id), JsonOptions);

        var response = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"),
            new { Kind = "spec", Body = frozen!.Body.Replace("v1", "v2"), LogNote = "unfreezing" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        updated!.Status.ShouldBe("draft");
        updated.FrozenAt.ShouldBeNull();
        updated.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Upsert_WithLogNote_WritesTheAgentMarker()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "v1" });
        await FreezeViaHumanDoorAsync(spec.Id);
        var frozen = await _client.GetFromJsonAsync<ArtifactJson>(AgentReadUrl(spec.Id), JsonOptions);

        var response = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"),
            new { Kind = "spec", Body = frozen!.Body.Replace("v1", "v2"), LogNote = "unfreezing" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echoed = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        echoed["statusChanged"]!.GetValue<string>().ShouldBe("validated → draft");

        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(AgentReadUrl(spec.Id), JsonOptions);
        artifact!.Body.ShouldContain("[agent] — unfreezing");
        artifact.Body.ShouldContain("[auto] — artifact demoted: validated → draft");
    }

    [Fact]
    public async Task AppendLog_AgentDoor_WritesTheAgentMarker()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "v1" });

        var response = await _client.PostAsJsonAsync(
            AgentLogUrl(spec.Id), new { Note = "halfway through the wiring" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(AgentReadUrl(spec.Id), JsonOptions);
        artifact!.Body.ShouldContain("[agent] — halfway through the wiring");
    }

    [Fact]
    public async Task EditLog_AgentDoor_CorrectsTheNewestAgentLine()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "v1" });
        await PostAgentLogAsync(spec.Id, "halfway trough the wiring");

        var response = await _client.PutAsJsonAsync(
            AgentLogUrl(spec.Id), new { Text = "halfway through the wiring" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echo = await response.Content.ReadFromJsonAsync<ArtifactMutationJson>(JsonOptions);
        echo!.LastLogLine.ShouldBe("halfway through the wiring");

        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(AgentReadUrl(spec.Id), JsonOptions);
        artifact!.Body.ShouldContain("[agent] — halfway through the wiring");
        artifact.Body.ShouldNotContain("trough");
    }

    [Fact]
    public async Task EditLog_AgentDoor_WrongActor_WhenTheNewestLineIsUi()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "v1" });
        await PostHumanLogAsync(spec.Id, "board-side note");

        var response = await _client.PutAsJsonAsync(
            AgentLogUrl(spec.Id), new { Text = "agent editing the human's line" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("LogEditWrongActor");
        problem.Detail!.ShouldContain("own door marker");
    }

    [Fact]
    public async Task EditLog_AgentDoor_NotNewest_WhenANewerUiLineFollows()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "v1" });
        await PostAgentLogAsync(spec.Id, "my older note");
        await PostHumanLogAsync(spec.Id, "the human replied after");

        var response = await _client.PutAsJsonAsync(
            AgentLogUrl(spec.Id), new { Text = "reach past the newer line" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("LogEditNotNewest");
        problem.Detail!.ShouldContain("entry 1 of 2");
    }

    [Fact]
    public async Task EditLog_AgentDoor_MultilineText_ReturnsTypedRefusal()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "v1" });
        await PostAgentLogAsync(spec.Id, "line to correct");

        var response = await _client.PutAsJsonAsync(
            AgentLogUrl(spec.Id), new { Text = "corrected\r\nsmuggled line" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("LogEditMultilineText");
    }

    [Fact]
    public async Task EditLog_AgentDoor_BlankText_ReturnsValidationProblem()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "v1" });
        await PostAgentLogAsync(spec.Id, "line to correct");

        var response = await _client.PutAsJsonAsync(
            AgentLogUrl(spec.Id), new { Text = "   " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task PostAgentLogAsync(Guid artifactId, string note)
    {
        var response = await _client.PostAsJsonAsync(AgentLogUrl(artifactId), new { Note = note });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task PostHumanLogAsync(Guid artifactId, string note)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/planning/artifacts/{artifactId}/log", new { Note = note });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_WithLogNote_WritesTheAgentMarker()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "spec",
            Body = "# Spec\n\n<goal>\nOld goal.\n</goal>\n"
        });
        await FreezeViaHumanDoorAsync(spec.Id);

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(spec.Id),
            new { Op = "replace_section", Section = "goal", Content = "<goal>\nNew goal.\n</goal>", LogNote = "reopened for rework" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echoed = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        echoed["statusChanged"]!.GetValue<string>().ShouldBe("validated → draft");

        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(AgentReadUrl(spec.Id), JsonOptions);
        artifact!.Body.ShouldContain("[agent] — reopened for rework");
        artifact.Body.ShouldContain("[auto] — artifact demoted: validated → draft");
    }

    [Fact]
    public async Task Patch_DemotingValidatedSpec_OnReviewFeature_PullsFeatureBackToInProgress()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "spec",
            Body = "# Spec\n\n<goal>\nOriginal.\n</goal>\n"
        });
        var plan = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "plan", Body = "plan" });
        var task1 = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task one" });
        var task2 = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task two" });
        await FreezeViaHumanDoorAsync(spec.Id);
        await FreezeViaHumanDoorAsync(plan.Id);
        await SetTaskStatusAsync(task1.Id, "done");
        await SetTaskStatusAsync(task2.Id, "done");

        var inReview = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/agent/projects/{project.Id}/planning/features/feat", JsonOptions);
        inReview!.Status.ShouldBe("review");

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(spec.Id),
            new { Op = "replace_section", Section = "goal", Content = "<goal>\nNew goal.\n</goal>", LogNote = "reopened for rework" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echoed = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        echoed["statusChanged"]!.GetValue<string>().ShouldBe("validated → draft");
        echoed["featureStatusChanged"]!.GetValue<string>().ShouldBe("review → inProgress");

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/agent/projects/{project.Id}/planning/features/feat", JsonOptions);
        feature!.Status.ShouldBe("inProgress");
        feature.Body.ShouldContain("[auto] — feature demoted: review → inProgress (spec no longer validated)");
    }

    [Fact]
    public async Task Validate_WithRepliedApproval_SucceedsAndWritesTheProofLine()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "spec" });
        var notificationId = await NotifyAndReplyAsync(project.Id, "feat");

        var response = await _client.PostAsync(AgentValidateUrl(spec.Id), null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(AgentReadUrl(spec.Id), JsonOptions);
        artifact!.Body.ShouldContain($"[agent] — validated with human proof: notification {notificationId} replied");
    }

    [Fact]
    public async Task Validate_WithoutNotification_Returns409WithActionableDetail()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "spec" });

        var response = await _client.PostAsync(AgentValidateUrl(spec.Id), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("ValidationProofRequired");
        problem.Detail!.ShouldBe(
            "Human approval required: send overmind_notify(kind=approval, featureSlug=feat) " +
            "and wait for the human's reply in the notification center, or validate via the UI.");
    }

    [Fact]
    public async Task Validate_ArtifactEditedAfterTheReply_Returns409()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "spec",
            Body = "# Spec\n\n<goal>\nOriginal.\n</goal>\n"
        });
        await NotifyAndReplyAsync(project.Id, "feat");

        await _client.PostAsJsonAsync(
            AgentPatchUrl(spec.Id),
            new { Op = "replace_section", Section = "goal", Content = "<goal>\nAmended.\n</goal>", LogNote = "edited after the reply" });

        var response = await _client.PostAsync(AgentValidateUrl(spec.Id), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("ValidationProofRequired");
    }

    [Fact]
    public async Task Validate_ApprovalReplyOnOtherFeature_Returns409()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "spec" });
        await NotifyAndReplyAsync(project.Id, "other-feature");

        var response = await _client.PostAsync(AgentValidateUrl(spec.Id), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("ValidationProofRequired");
    }

    [Fact]
    public async Task Validate_QuestionKindReply_DoesNotCountAsProof()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "spec" });
        await NotifyAndReplyAsync(project.Id, "feat", kind: "question");

        var response = await _client.PostAsync(AgentValidateUrl(spec.Id), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("ValidationProofRequired");
    }

    [Fact]
    public async Task Validate_HumanDoor_IsFreeWithoutNotification_AndWritesTheUiLine()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "spec" });

        var response = await _client.PostAsync($"/api/planning/artifacts/{spec.Id}/validate", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(AgentReadUrl(spec.Id), JsonOptions);
        artifact!.Body.ShouldContain("[ui] — validated via UI");
    }

    [Fact]
    public async Task Validate_GateOn_StopsBeforeProofLogic()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "spec" });
        await NotifyAndReplyAsync(project.Id, "feat");
        await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planning/workflow",
            new { RequireHumanValidation = "on" });

        var response = await _client.PostAsync(AgentValidateUrl(spec.Id), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("HumanValidationRequired");
        problem.Detail!.ShouldStartWith("STOP:");
        problem.Detail.ShouldContain("awaits human validation");
    }

    private async Task<string> NotifyAndReplyAsync(Guid projectId, string featureSlug, string kind = "approval")
    {
        var create = await _client.PostAsJsonAsync("/api/agent/notifications", new
        {
            Kind = kind,
            Text = $"please approve {featureSlug}",
            ProjectId = projectId,
            FeatureSlug = featureSlug
        });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var notificationId = JsonNode.Parse(await create.Content.ReadAsStringAsync())!["id"]!.GetValue<string>();

        var reply = await _client.PostAsJsonAsync(
            $"/api/notifications/{notificationId}/reply", new { Text = "lgtm" });
        reply.StatusCode.ShouldBe(HttpStatusCode.OK);

        return notificationId;
    }

    [Fact]
    public async Task SetArtifactStatus_ManualValidatedToDraft_OnReviewFeature_PullsFeatureBackToInProgress()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "spec",
            Body = "# Spec\n\n<goal>\nOriginal.\n</goal>\n"
        });
        var plan = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "plan", Body = "plan" });
        var task1 = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task one" });
        var task2 = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task two" });
        await FreezeViaHumanDoorAsync(spec.Id);
        await FreezeViaHumanDoorAsync(plan.Id);
        await SetTaskStatusAsync(task1.Id, "done");
        await SetTaskStatusAsync(task2.Id, "done");

        var response = await _client.PostAsJsonAsync(
            AgentStatusUrl(spec.Id),
            new { Status = "draft", LogNote = "review outcome: spec rejected" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echoed = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        echoed["featureStatusChanged"]!.GetValue<string>().ShouldBe("review → inProgress");

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/agent/projects/{project.Id}/planning/features/feat", JsonOptions);
        feature!.Status.ShouldBe("inProgress");
        feature.Body.ShouldContain("[auto] — feature demoted: review → inProgress (spec no longer validated)");
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
    public async Task UpsertTasks_FiveTasksInOneCall_CreatesFiveWithSingleTransitionLine()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentTasksUrl(project.Id, "feat"),
            new
            {
                Tasks = Enumerable.Range(1, 5)
                    .Select(i => new { Body = $"# Task {i}\n\n<goal>\nDo {i}.\n</goal>" })
                    .ToArray()
            });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var batch = await response.Content.ReadFromJsonAsync<TaskBatchJson>(JsonOptions);
        batch!.Created.ShouldBe(5);
        batch.Updated.ShouldBe(0);
        batch.Artifacts.Length.ShouldBe(5);

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/agent/projects/{project.Id}/planning/features/feat", JsonOptions);
        feature!.Status.ShouldBe("ready");
        feature.Body.Split('\n').Count(line => line.Contains("artifact created → ready")).ShouldBe(1);
        feature.Artifacts.Where(a => a.Kind == "task").Select(a => a.Order).ToArray()
            .ShouldBe([0, 1, 2, 3, 4]);
    }

    [Fact]
    public async Task UpsertTasks_ValidationFailureAtIndex_Returns400NamingTheIndexAndWritesNothing()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentTasksUrl(project.Id, "feat"),
            new
            {
                Tasks = new object[]
                {
                    new { Body = "# Task one" },
                    new { Body = "" },
                    new { Body = "# Task three", Order = -1 }
                }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldContain("tasks[1]", Case.Insensitive);
        raw.ShouldContain("tasks[2]", Case.Insensitive);

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/agent/projects/{project.Id}/planning/features/feat", JsonOptions);
        feature!.Artifacts.Length.ShouldBe(0);
        feature.Status.ShouldBe("draft");
        feature.Body.ShouldNotContain("artifact created");
    }

    [Fact]
    public async Task UpsertTasks_NullItemInBatch_Returns400NamingTheIndexAndWritesNothing()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentTasksUrl(project.Id, "feat"),
            new { Tasks = new object?[] { new { Body = "# Task one" }, null } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldContain("tasks[1]", Case.Insensitive);

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/agent/projects/{project.Id}/planning/features/feat", JsonOptions);
        feature!.Artifacts.Length.ShouldBe(0);
        feature.Status.ShouldBe("draft");
        feature.Body.ShouldNotContain("artifact created");
    }

    [Fact]
    public async Task UpsertTasks_EmptyBatch_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(AgentTasksUrl(project.Id, "feat"), new { Tasks = Array.Empty<object>() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpsertTasks_ExistingOrder_UpdatesInPlaceAlongsideNewTasks()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var existing = await CreateAgentArtifactAsync(project.Id, "feat",
            new { Kind = "task", Body = "# Task one v1", Order = 0 });

        var response = await _client.PostAsJsonAsync(
            AgentTasksUrl(project.Id, "feat"),
            new
            {
                Tasks = new object[]
                {
                    new { Body = "# Task one v2", Order = 0, LogNote = "rewritten from the plan" },
                    new { Body = "# Task two", Brief = "Wire the polling hook" }
                }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var batch = await response.Content.ReadFromJsonAsync<TaskBatchJson>(JsonOptions);
        batch!.Created.ShouldBe(1);
        batch.Updated.ShouldBe(1);

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/agent/projects/{project.Id}/planning/features/feat", JsonOptions);
        var tasks = feature!.Artifacts.Where(a => a.Kind == "task").OrderBy(a => a.Order).ToArray();
        tasks.Length.ShouldBe(2);
        tasks[0].Id.ShouldBe(existing.Id);
        tasks[0].Version.ShouldBe(2);
        tasks[0].Body.ShouldContain("Task one v2");
        tasks[1].Order.ShouldBe(1);
        tasks[1].Brief.ShouldBe("Wire the polling hook");
    }

    [Fact]
    public async Task UpsertTasks_UnknownFeature_Returns404()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PostAsJsonAsync(
            AgentTasksUrl(project.Id, "missing"),
            new { Tasks = new object[] { new { Body = "# Task" } } });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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
        await NotifyAndReplyAsync(project.Id, "feat");

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
        problem.Detail!.ShouldContain("logNote");

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
        current.Reason.ShouldBe(
            "STOP: spec awaits human validation — it cannot be approved through MCP. " +
            "Send overmind_notify(kind=approval, featureSlug=feat) and wait for the human's reply — validation is never agent-approvable.");
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
        current.Reason.ShouldBe(
            "STOP: spec awaits human validation — it cannot be approved through MCP. " +
            "Send overmind_notify(kind=approval, featureSlug=feat) and wait for the human's reply — validation is never agent-approvable.");
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
        current.Reason.ShouldBe(
            "STOP: plan awaits human validation — it cannot be approved through MCP. " +
            "Send overmind_notify(kind=approval, featureSlug=feat) and wait for the human's reply — validation is never agent-approvable.");
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
        current.Reason.ShouldBe(
            "STOP: plan awaits human validation — it cannot be approved through MCP. " +
            "Send overmind_notify(kind=approval, featureSlug=feat) and wait for the human's reply — validation is never agent-approvable.");
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
        current.Reason.ShouldBe(
            "No tasks yet — decompose the validated plan via planning_upsert_tasks: each task an execution contract (goal, scope, anti-scope, acceptance-criteria, refs).");
    }

    [Fact]
    public async Task Current_GateOff_SkipsSpecPlanStop()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("empty");
        current.Reason.ShouldBe(
            "No tasks yet — decompose the validated plan via planning_upsert_tasks: each task an execution contract (goal, scope, anti-scope, acceptance-criteria, refs).");
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
        current.Reason.ShouldBe("All tasks are done — write the result artifact via planning_upsert_artifact(kind=result).");
    }

    [Fact]
    public async Task Current_ResultDraft_TellsToValidateIt()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1" });
        await SetTaskStatusAsync(task.Id, "done");
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "result", Body = "# Result\n\n<goal>\nShipped.\n</goal>" });

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("empty");
        current.Artifact.ShouldBeNull();
        current.Reason.ShouldBe(
            "Result artifact drafted — validate it via the proof flow: " +
            "send overmind_notify(kind=approval, featureSlug=feat) and wait for the human's reply.");
    }

    [Fact]
    public async Task Current_ResultValidated_TellsToCloseTheFeature()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1" });
        await SetTaskStatusAsync(task.Id, "done");
        var result = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "result", Body = "# Result" });
        await FreezeViaHumanDoorAsync(result.Id);

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("empty");
        current.Artifact.ShouldBeNull();
        current.Reason.ShouldBe("Result validated — closing the feature needs the human's done tap on the board.");
    }

    [Fact]
    public async Task Current_OnlyReviewTasksLeft_ReturnsHeld()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var first = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1", Order = 1 });
        var second = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 2", Order = 2 });
        await SetTaskStatusAsync(first.Id, "review");
        await SetTaskStatusAsync(second.Id, "review");

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("held");
        current.Artifact.ShouldBeNull();
        current.Reason.ShouldBe("2 in review — finish the reviews, then close each task with planning_set_status.");
    }

    [Fact]
    public async Task Delete_HumanDoor_DeletedTaskVanishesFromCurrentAndRoster()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var doomed = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "doomed", Order = 1 });
        var kept = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "kept", Order = 2 });

        var delete = await _client.DeleteAsync($"/api/planning/artifacts/{doomed.Id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var current = await GetCurrentAsync(project.Id, "feat");
        current.State.ShouldBe("task");
        current.Artifact!.Id.ShouldBe(kept.Id);

        var roster = await _client.GetFromJsonAsync<RosterJson>(
            $"/api/agent/projects/{project.Id}/planning/features/feat/artifacts", JsonOptions);
        roster!.Artifacts.Select(a => a.ArtifactId).ShouldBe([kept.Id]);

        var gone = await _client.GetAsync($"/api/agent/planning/artifacts/{doomed.Id}");
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_AgentDoor_DraftTask_LandsTheAuditLineInTheFeatureLog()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var doomed = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "doomed", Order = 1 });

        var delete = await DeleteArtifactAsync(doomed.Id, new { LogNote = "duplicate from upsert" }, agentDoor: true);
        delete.StatusCode.ShouldBe(HttpStatusCode.OK);

        var echo = await delete.Content.ReadFromJsonAsync<DeleteJson>(JsonOptions);
        echo!.Id.ShouldBe(doomed.Id);
        echo.Kind.ShouldBe("task");
        echo.FeatureLogLine.ShouldBe("artifact task 01 deleted by agent, duplicate from upsert");

        var feature = await _client.GetFromJsonAsync<FeatureJson>(AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        feature!.Body.ShouldContain("artifact task 01 deleted by agent, duplicate from upsert");

        var roster = await _client.GetFromJsonAsync<RosterJson>(
            $"/api/agent/projects/{project.Id}/planning/features/feat/artifacts", JsonOptions);
        roster!.Artifacts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_AgentDoor_ValidatedSpec_Returns409NamingTheHumanDoor()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });
        await FreezeViaHumanDoorAsync(spec.Id);

        var delete = await DeleteArtifactAsync(spec.Id, agentDoor: true);

        delete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await delete.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("ArtifactDeletionRefused");
        problem.Detail!.ShouldContain("deletes draft artifacts only");
        problem.Detail!.ShouldContain("human door in the Overmind UI");

        var kept = await _client.GetAsync(AgentReadUrl(spec.Id));
        kept.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_AgentDoor_DoneTask_Returns409()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "settled", Order = 1 });
        await SetTaskStatusAsync(task.Id, "done");

        var delete = await DeleteArtifactAsync(task.Id, agentDoor: true);

        delete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await delete.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("ArtifactDeletionRefused");
    }

    [Fact]
    public async Task Delete_HumanDoor_ValidatedSpec_PullsTheFeatureOutOfReview()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });
        await FreezeViaHumanDoorAsync(spec.Id);
        await BringToStatusViaHumanDoorAsync(project.Id, "feat", "review");

        var delete = await DeleteArtifactAsync(spec.Id, new { LogNote = "spec superseded" });
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var feature = await _client.GetFromJsonAsync<FeatureJson>(AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        feature!.Status.ShouldBe("inProgress");
        feature.Body.ShouldContain("artifact spec deleted by human, spec superseded");
        feature.Body.ShouldContain("feature demoted: review → inProgress (spec deleted)");

        var gone = await _client.GetAsync(AgentReadUrl(spec.Id));
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_TaskWithDependents_Returns409NamingTheCleanup()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await _client.PostAsJsonAsync(AgentTasksUrl(project.Id, "feat"), new
        {
            Tasks = new object[]
            {
                new { Body = "blocker", Order = 1 },
                new { Body = "dependent", Order = 2, BlockedBy = new[] { 1 } }
            }
        });
        var feature = await _client.GetFromJsonAsync<FeatureJson>(AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        var blocker = feature!.Artifacts.Single(a => a.Order == 1);

        var delete = await DeleteArtifactAsync(blocker.Id, agentDoor: true);

        delete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await delete.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("DeletionBlockedByDependents");
        problem.Detail!.ShouldContain("Clean up dependencies first");
        problem.Detail!.ShouldContain("task 02");

        var kept = await _client.GetAsync(AgentReadUrl(blocker.Id));
        kept.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_UnknownArtifact_404CarriesTheAliasHint()
    {
        var response = await _client.DeleteAsync($"/api/planning/artifacts/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Detail!.ShouldContain("artifact aliases");
    }

    [Fact]
    public async Task Current_ReviewAndBlockedLeft_ReturnsHeldNamingReviewCount()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var first = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1", Order = 1 });
        var second = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 2", Order = 2 });
        await SetTaskStatusAsync(first.Id, "review");
        await SetTaskStatusAsync(second.Id, "blocked");

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("held");
        current.Artifact.ShouldBeNull();
        current.Reason.ShouldBe("1 in review — finish the reviews, then close each task with planning_set_status.");
    }

    [Fact]
    public async Task Current_WorkableTodoBeatsHeld_ReturnsTheTodoTask()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var first = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1", Order = 1 });
        var second = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 2", Order = 2 });
        await SetTaskStatusAsync(first.Id, "review");

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("task");
        current.Reason.ShouldBeNull();
        current.Artifact.ShouldNotBeNull();
        current.Artifact!.Order.ShouldBe(2);
        current.Artifact.Status.ShouldBe("todo");
    }

    [Fact]
    public async Task Current_TaskPayload_Warnings_AllFourSpecPlanCombinations()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "t1", Order = 0 });

        (await GetCurrentAsync(project.Id, "feat")).Warnings!
            .ShouldBe(["spec missing", "plan not validated"]);

        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "spec" });

        (await GetCurrentAsync(project.Id, "feat")).Warnings!
            .ShouldBe(["spec not validated", "plan not validated"]);

        await FreezeViaHumanDoorAsync(spec.Id);

        (await GetCurrentAsync(project.Id, "feat")).Warnings!
            .ShouldBe(["plan not validated"]);

        var plan = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "plan", Body = "plan" });
        await FreezeViaHumanDoorAsync(plan.Id);

        (await GetCurrentAsync(project.Id, "feat")).Warnings.ShouldBeNull();
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
        current.Warnings!.ShouldBe(["spec missing", "plan not validated"]);
    }

    [Fact]
    public async Task Current_CleanSpecPlan_RawWirePayload_OmitsTheWarningsKey()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "spec" });
        var plan = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "plan", Body = "plan" });
        await FreezeViaHumanDoorAsync(spec.Id);
        await FreezeViaHumanDoorAsync(plan.Id);
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1" });

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("task");
        current.Warnings.ShouldBeNull();

        var raw = await _client.GetStringAsync(
            $"/api/agent/projects/{project.Id}/planning/features/feat/current");
        JsonNode.Parse(raw)!.AsObject().ContainsKey("warnings").ShouldBeFalse();
    }

    [Fact]
    public async Task Current_BlockedFeature_ReturnsStopNamingOnlyUnclosedBlockers()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat-a", "Feature A");
        await CreateAgentFeatureAsync(project.Id, "feat-b", "Feature B");
        await CreateAgentFeatureAsync(project.Id, "feat-c", "Feature C");
        await AddDependencyAsync(project.Id, "feat-a", "feat-b");
        await AddDependencyAsync(project.Id, "feat-a", "feat-c");
        await BringToStatusViaHumanDoorAsync(project.Id, "feat-c", "review");
        var task = await CreateAgentArtifactAsync(project.Id, "feat-a", new { Kind = "task", Body = "task 1", Order = 1 });

        var current = await GetCurrentAsync(project.Id, "feat-a");

        current.State.ShouldBe("stop");
        current.Artifact.ShouldBeNull();
        current.Reason.ShouldNotBeNull();
        current.Reason!.ShouldStartWith("STOP:");
        current.Reason.ShouldContain("is blocked by unclosed features: feat-b (draft)");
        current.Reason.ShouldNotContain("feat-c");
        current.Reason.ShouldContain("Reads stay open");
        current.Feature.Slug.ShouldBe("feat-a");

        var readable = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/agent/projects/{project.Id}/planning/features/feat-a", JsonOptions);
        readable!.Artifacts.Single().Id.ShouldBe(task.Id);

        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/agent/planning/artifacts/{task.Id}", JsonOptions);
        artifact!.Body.ShouldContain("task 1");
    }

    [Fact]
    public async Task Current_BlockersAllClosed_BehavesExactlyAsUnblocked()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat-a", "Feature A");
        await CreateAgentFeatureAsync(project.Id, "feat-b", "Feature B");
        await AddDependencyAsync(project.Id, "feat-a", "feat-b");
        var first = await CreateAgentArtifactAsync(project.Id, "feat-a", new { Kind = "task", Body = "task 1", Order = 1 });
        await CreateAgentArtifactAsync(project.Id, "feat-a", new { Kind = "task", Body = "task 2", Order = 2 });
        await SetTaskStatusAsync(first.Id, "done");
        await CreateAgentArtifactAsync(project.Id, "feat-b", new { Kind = "result", Body = "close-out" });
        await BringToStatusViaHumanDoorAsync(project.Id, "feat-b", "done");

        var current = await GetCurrentAsync(project.Id, "feat-a");

        current.State.ShouldBe("task");
        current.Reason.ShouldBeNull();
        current.Artifact.ShouldNotBeNull();
        current.Artifact!.Order.ShouldBe(2);
    }

    private async Task AddDependencyAsync(Guid projectId, string slug, string blockedBy)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features/{slug}/dependencies",
            new { BlockedBy = blockedBy });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetArtifactStatus_TaskDoneWithUntickedAc_SucceedsWithWarning()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Task\n\n<acceptance-criteria>\n- [x] endpoint wired\n- [ ] docs written\n- [ ] manual pass\n</acceptance-criteria>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentStatusUrl(task.Id),
            new { Status = "done", LogNote = "closing with open items" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(raw).RootElement.GetProperty("warnings").EnumerateArray()
            .Select(w => w.GetString()).ToArray()
            .ShouldBe(["2 of 3 acceptance items unticked"]);

        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{task.Id}", JsonOptions);
        artifact!.Status.ShouldBe("done");
    }

    [Fact]
    public async Task SetArtifactStatus_TaskDoneWithAllAcTicked_ReturnsNoWarnings()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Task\n\n<acceptance-criteria>\n- [x] endpoint wired\n- [x] docs written\n</acceptance-criteria>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentStatusUrl(task.Id),
            new { Status = "done", LogNote = "everything ticked" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        PropertyNames(raw).ShouldNotContain("warnings");
    }

    [Fact]
    public async Task SetArtifactStatus_TaskDoneWithoutAcSection_ReturnsNoWarnings()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Task\n\n<goal>\nShip it.\n</goal>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentStatusUrl(task.Id),
            new { Status = "done", LogNote = "no acceptance section" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        PropertyNames(raw).ShouldNotContain("warnings");
    }

    [Fact]
    public async Task SetArtifactStatus_NonDoneTransition_NeverWarns()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Task\n\n<acceptance-criteria>\n- [ ] first thing\n- [ ] second thing\n</acceptance-criteria>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentStatusUrl(task.Id),
            new { Status = "review", LogNote = "handing over with open items" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        PropertyNames(raw).ShouldNotContain("warnings");
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("ready")]
    [InlineData("inProgress")]
    [InlineData("review")]
    public async Task SetFeatureStatus_DoneWithResultAndFreshTap_AppliesAndLogsProvenance(string start)
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "result", Body = "close-out" });
        await NotifyAndReplyAsync(project.Id, "feat");
        await BringToStatusViaHumanDoorAsync(project.Id, "feat", start);

        var response = await _client.PostAsJsonAsync(
            AgentFeatureStatusUrl(project.Id, "feat"),
            new { Status = "done", LogNote = "by user request in-session" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(raw).RootElement.EnumerateObject().Select(p => p.Name).ToArray()
            .ShouldBe(["slug", "status", "version", "lastLogLine"]);
        var echo = await response.Content.ReadFromJsonAsync<FeatureMutationJson>(JsonOptions);
        echo!.Slug.ShouldBe("feat");
        echo.Status.ShouldBe("done");
        echo.LastLogLine.ShouldStartWith("done via agent review, approved by human tap ");

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        feature!.Body.ShouldContain("by user request in-session");
        feature.Body.ShouldContain("done via agent review, approved by human tap ");
    }

    [Fact]
    public async Task SetFeatureStatus_DoneWithClaim_ProvenanceCarriesClaimIdentity()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var session = Guid.NewGuid().ToString();
        _client.DefaultRequestHeaders.Add("X-Overmind-Session", session);
        var claim = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/feat/claim",
            new { Alias = "Dan", Vendor = "qwen" });
        claim.StatusCode.ShouldBe(HttpStatusCode.OK);

        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "result", Body = "close-out" });
        await NotifyAndReplyAsync(project.Id, "feat");

        var response = await _client.PostAsJsonAsync(
            AgentFeatureStatusUrl(project.Id, "feat"),
            new { Status = "done", LogNote = "review cycle closed it" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echo = await response.Content.ReadFromJsonAsync<FeatureMutationJson>(JsonOptions);
        echo!.LastLogLine.ShouldBe("claim released on done-close");

        var closed = await _client.GetFromJsonAsync<FeatureJson>(
            AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        closed!.Body.ShouldContain(
            $"done via agent review by Dan (qwen), approved by human tap {DateTime.UtcNow:yyyy-MM-dd}");
        closed.Body.ShouldContain("claim released on done-close");
    }

    [Fact]
    public async Task SetFeatureStatus_DoneWithoutResult_Returns409NamingResult()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await NotifyAndReplyAsync(project.Id, "feat");

        var response = await _client.PostAsJsonAsync(
            AgentFeatureStatusUrl(project.Id, "feat"),
            new { Status = "done", LogNote = "by user request in-session" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("FeatureResultRequired");
        problem.Detail!.ShouldContain("result artifact is missing");
        problem.Detail!.ShouldContain("kind=result");
    }

    [Fact]
    public async Task SetFeatureStatus_DoneWithResultButNoTap_Returns409TeachingNotifyReply()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "result", Body = "close-out" });

        var response = await _client.PostAsJsonAsync(
            AgentFeatureStatusUrl(project.Id, "feat"),
            new { Status = "done", LogNote = "by user request in-session" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("FeatureDoneTapRequired");
        problem.Detail!.ShouldContain("overmind_notify(kind=approval, featureSlug=feat)");
        problem.Detail!.ShouldContain("newer than the result's last update");

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        feature!.Status.ShouldNotBe("done");
    }

    [Fact]
    public async Task HumanDoor_DoneWithoutResult_Returns409WithExplanation()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await BringToStatusViaHumanDoorAsync(project.Id, "feat", "review");

        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat",
            new { Status = "done" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("FeatureResultRequired");
        problem.Detail!.ShouldContain("result artifact is missing");
    }

    [Fact]
    public async Task HumanDoor_DoneWithResult_NeedsNoTap()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "result", Body = "close-out" });
        await BringToStatusViaHumanDoorAsync(project.Id, "feat", "review");

        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat",
            new { Status = "done" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        feature!.Status.ShouldBe("done");
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
        var echo = await response.Content.ReadFromJsonAsync<FeatureMutationJson>(JsonOptions);
        echo!.Status.ShouldBe("inProgress");
        echo.LastLogLine.ShouldBe("review findings need fixes");
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
        var echo = await response.Content.ReadFromJsonAsync<FeatureMutationJson>(JsonOptions);
        echo!.Status.ShouldBe("inProgress");
        echo.LastLogLine.ShouldNotBe("by user request in-session");

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        feature!.Body.ShouldNotContain("by user request in-session");
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
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "result", Body = "close-out" });
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
    public async Task UpdateFeature_TitleOnly_RenamesAndEchoesSlimProjection()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentFeatureUpdateUrl(project.Id, "feat"),
            new { Title = "Renamed feature", LogNote = "title polish" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(raw).RootElement.EnumerateObject().Select(p => p.Name).ToArray()
            .ShouldBe(["slug", "status", "version", "updatedAt", "bodyChars"]);

        var echo = await response.Content.ReadFromJsonAsync<FeatureUpdateJson>(JsonOptions);
        echo!.Slug.ShouldBe("feat");
        echo.Status.ShouldBe("draft");

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        feature!.Title.ShouldBe("Renamed feature");
        echo.BodyChars.ShouldBe(feature.Body.Length);
    }

    [Fact]
    public async Task UpdateFeature_BodyOnly_ReplacesBodyAndKeepsTitle()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentFeatureUpdateUrl(project.Id, "feat"),
            new { Body = "<goal>\nFresh goal.\n</goal>\n", LogNote = "rewrite body" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echo = await response.Content.ReadFromJsonAsync<FeatureUpdateJson>(JsonOptions);

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        feature!.Title.ShouldBe("Feature");
        feature.Body.ShouldStartWith("<goal>");
        echo!.BodyChars.ShouldBe(feature.Body.Length);
    }

    [Fact]
    public async Task UpdateFeature_PriorityOnly_MovesToTailOfTargetBand()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "moved", "Moved");
        await CreateAgentFeatureAsync(project.Id, "p2-stays", "Stays");
        await CreateAgentFeatureAsync(project.Id, "p1-first", "First", "p1");

        var response = await _client.PostAsJsonAsync(
            AgentFeatureUpdateUrl(project.Id, "moved"),
            new { Priority = "p1", LogNote = "bumping priority by user request" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var queueResponse = await _client.GetAsync(
            $"/api/agent/projects/{project.Id}/planning/task-queue");
        queueResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var queue = await queueResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var positions = queue.GetProperty("items").EnumerateArray().ToDictionary(
            i => i.GetProperty("slug").GetString()!,
            i => (Priority: i.GetProperty("priority").GetString()!,
                Position: i.GetProperty("queuePosition").GetInt32()));

        positions["moved"].Priority.ShouldBe("p1");
        positions["moved"].Position.ShouldBe(2);
        positions["p1-first"].Position.ShouldBe(1);
        positions["p2-stays"].Position.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateFeature_MissingLogNote_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentFeatureUpdateUrl(project.Id, "feat"),
            new { Title = "Renamed feature", LogNote = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateFeature_AllEditableFieldsEmpty_Returns400NamingFields()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentFeatureUpdateUrl(project.Id, "feat"),
            new { LogNote = "empty edit" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var messages = body.GetProperty("errors").EnumerateObject().SelectMany(e => e.Value.EnumerateArray());
        messages.ShouldContain(m => m.GetString()!.Contains("title", StringComparison.Ordinal));
        messages.ShouldContain(m => m.GetString()!.Contains("body", StringComparison.Ordinal));
        messages.ShouldContain(m => m.GetString()!.Contains("priority", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public async Task UpdateFeature_WhitespaceOnlyTitleOrBody_Returns400(string whitespace)
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var titleResponse = await _client.PostAsJsonAsync(
            AgentFeatureUpdateUrl(project.Id, "feat"),
            new { Title = whitespace, LogNote = "n" });
        var bodyResponse = await _client.PostAsJsonAsync(
            AgentFeatureUpdateUrl(project.Id, "feat"),
            new { Body = whitespace, LogNote = "n" });

        titleResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        bodyResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateFeature_UnknownPriority_Returns400ListingBands()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentFeatureUpdateUrl(project.Id, "feat"),
            new { Priority = "banana", LogNote = "n" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var messages = body.GetProperty("errors").EnumerateObject().SelectMany(e => e.Value.EnumerateArray());
        messages.ShouldContain(m => m.GetString()!.Contains("p0, p1, p2, p3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpdateFeature_UnknownFeature_Returns404()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PostAsJsonAsync(
            AgentFeatureUpdateUrl(project.Id, "missing"),
            new { Title = "X", LogNote = "n" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateFeature_RewrittenLogHistory_FailsAsNotAppendable()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await BringToStatusViaHumanDoorAsync(project.Id, "feat", "inProgress");

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        var tampered = string.Join('\n', feature!.Body.Split('\n')
            .Where(line => !line.Contains("status moved", StringComparison.Ordinal)));

        var response = await _client.PostAsJsonAsync(
            AgentFeatureUpdateUrl(project.Id, "feat"),
            new { Body = tampered, LogNote = "sneaky rewrite" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("LogHistoryNotAppendable");
    }

    [Fact]
    public async Task UpdateFeature_WritesAgentMarkerThenHumanPutStillWorksWithoutLogNote()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var agentResponse = await _client.PostAsJsonAsync(
            AgentFeatureUpdateUrl(project.Id, "feat"),
            new { Title = "Agent renamed", LogNote = "agent edit" });
        agentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        feature!.Body.ShouldContain("[agent] — agent edit");

        var humanResponse = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat",
            new { Title = "Human renamed" });
        humanResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await _client.GetFromJsonAsync<FeatureJson>(
            AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        updated!.Title.ShouldBe("Human renamed");
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
            .ShouldBe(["artifactId", "kind", "section", "version", "bodyChars", "status", "lastLogLine"]);

        var patch = await response.Content.ReadFromJsonAsync<PatchResultJson>(JsonOptions);
        patch!.ArtifactId.ShouldBe(spec.Id);
        patch.Kind.ShouldBe("spec");
        patch.Section.ShouldBe("goal");
        patch.Version.ShouldBe(2);
        patch.Status.ShouldBe("draft");
        patch.LastLogLine.ShouldBe(string.Empty);

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
        var raw = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(raw).RootElement.EnumerateObject().Select(p => p.Name).ToArray()
            .ShouldBe(["artifactId", "kind", "section", "version", "bodyChars", "status", "lastLogLine",
                       "tickedIndex", "tickedText", "noChange"]);
        var patch = await response.Content.ReadFromJsonAsync<PatchResultJson>(JsonOptions);
        patch!.ArtifactId.ShouldBe(task.Id);
        patch.Kind.ShouldBe("task");
        patch.Section.ShouldBe("acceptance-criteria");
        patch.Version.ShouldBe(2);
        patch.TickedIndex.ShouldBe(1);
        patch.TickedText.ShouldBe("first thing");
        patch.NoChange.ShouldBe(false);

        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{task.Id}", JsonOptions);
        artifact!.Body.ShouldContain("- [x] first thing");
        artifact.Body.ShouldContain("- [ ] second thing");
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
    public async Task AppendLog_ReturnsCompactEchoCarryingTheNewLastLogLine()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "v1" });

        var response = await _client.PostAsJsonAsync(
            AgentLogUrl(spec.Id), new { Note = "progress note", Source = "agent" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(raw).RootElement.EnumerateObject().Select(p => p.Name).ToArray()
            .ShouldBe(["id", "status", "version", "lastLogLine"]);
        var echo = await response.Content.ReadFromJsonAsync<ArtifactMutationJson>(JsonOptions);
        echo!.Id.ShouldBe(spec.Id);
        echo.Version.ShouldBe(2);
        echo.Status.ShouldBe("draft");
        echo.LastLogLine.ShouldBe("progress note");
    }

    [Fact]
    public async Task Validate_ReturnsCompactEchoCarryingTheValidationMarker()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "v1" });

        var response = await _client.PostAsJsonAsync(
            AgentValidateUrl(spec.Id),
            new { AsMigrated = true, LogNote = "frozen v1 on disk, user-approved" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(raw).RootElement.EnumerateObject().Select(p => p.Name).ToArray()
            .ShouldBe(["id", "status", "version", "lastLogLine"]);
        var echo = await response.Content.ReadFromJsonAsync<ArtifactMutationJson>(JsonOptions);
        echo!.Id.ShouldBe(spec.Id);
        echo.Status.ShouldBe("validated");
        echo.LastLogLine.ShouldBe("validated as migrated — pre-approved on disk: frozen v1 on disk, user-approved");
    }

    [Fact]
    public async Task SetArtifactStatus_ReturnsCompactEchoKeySet()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task" });

        var response = await _client.PostAsJsonAsync(
            AgentStatusUrl(task.Id), new { Status = "inProgress", LogNote = "started" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(raw).RootElement.EnumerateObject().Select(p => p.Name).ToArray()
            .ShouldBe(["id", "status", "version", "lastLogLine"]);
        var echo = await response.Content.ReadFromJsonAsync<ArtifactMutationJson>(JsonOptions);
        echo!.Id.ShouldBe(task.Id);
        echo.Status.ShouldBe("inProgress");
        echo.Version.ShouldBe(1);
        echo.LastLogLine.ShouldBe("started");
    }

    [Fact]
    public async Task SetArtifactStatus_TaskTaken_PromotesReadyFeatureToInProgress()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "spec" });
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task" });

        var ceremony = await _client.GetFromJsonAsync<FeatureJson>(
            AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        ceremony!.Status.ShouldBe("ready");

        var response = await _client.PostAsJsonAsync(
            AgentStatusUrl(task.Id), new { Status = "inProgress", LogNote = "started" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        feature!.Status.ShouldBe("inProgress");
        feature.Body.ShouldContain("[auto] — task 00 taken → inProgress");
    }

    [Fact]
    public async Task Patch_RetickSurfacesNoChange_AndLeavesVersionUntouched()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Task\n\n<acceptance-criteria>\n- [ ] first thing\n</acceptance-criteria>\n"
        });

        var first = await _client.PostAsJsonAsync(
            AgentPatchUrl(task.Id), new { Op = "tick_ac", AcIndex = 1 });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstEcho = await first.Content.ReadFromJsonAsync<PatchResultJson>(JsonOptions);
        firstEcho!.NoChange.ShouldBe(false);
        firstEcho.TickedIndex.ShouldBe(1);
        firstEcho.TickedText.ShouldBe("first thing");
        firstEcho.Version.ShouldBe(2);

        var second = await _client.PostAsJsonAsync(
            AgentPatchUrl(task.Id), new { Op = "tick_ac", AcIndex = 1 });
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondEcho = await second.Content.ReadFromJsonAsync<PatchResultJson>(JsonOptions);
        secondEcho!.NoChange.ShouldBe(true);
        secondEcho.TickedIndex.ShouldBe(1);
        secondEcho.TickedText.ShouldBe("first thing");
        secondEcho.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Patch_UntickAcByText_ClearsTheMarkerAndEchoesTheResolvedItem()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Task\n\n<acceptance-criteria>\n- [x] mistaken tick\n- [ ] keep me\n</acceptance-criteria>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(task.Id),
            new { Op = "untick_ac", AcText = "mistaken tick" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var patch = await response.Content.ReadFromJsonAsync<PatchResultJson>(JsonOptions);
        patch!.Section.ShouldBe("acceptance-criteria");
        patch.TickedIndex.ShouldBe(1);
        patch.TickedText.ShouldBe("mistaken tick");
        patch.NoChange.ShouldBe(false);
        patch.Version.ShouldBe(2);

        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{task.Id}", JsonOptions);
        artifact!.Body.ShouldContain("- [ ] mistaken tick");
        artifact.Body.ShouldContain("- [ ] keep me");
    }

    [Fact]
    public async Task Patch_UntickAlreadyUnticked_SurfacesNoChange_AndLeavesVersionUntouched()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Task\n\n<acceptance-criteria>\n- [ ] untouched item\n</acceptance-criteria>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(task.Id), new { Op = "untick_ac", AcIndex = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var patch = await response.Content.ReadFromJsonAsync<PatchResultJson>(JsonOptions);
        patch!.NoChange.ShouldBe(true);
        patch.TickedIndex.ShouldBe(1);
        patch.TickedText.ShouldBe("untouched item");
        patch.Version.ShouldBe(1);
    }

    [Fact]
    public async Task Patch_SectionReplace_OmitsTickEcho_AndMovesFeatureUpdatedAt()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "spec",
            Body = "# Spec\n\n<goal>\nOld goal.\n</goal>\n"
        });

        var featureBefore = await _client.GetFromJsonAsync<FeatureJson>(
            AgentFeatureUrl(project.Id, "feat"), JsonOptions);

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(spec.Id),
            new { Op = "replace_section", Section = "goal", Content = "New goal." });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(raw).RootElement.EnumerateObject().Select(p => p.Name).ToArray()
            .ShouldBe(["artifactId", "kind", "section", "version", "bodyChars", "status", "lastLogLine"]);

        var featureAfter = await _client.GetFromJsonAsync<FeatureJson>(
            AgentFeatureUrl(project.Id, "feat"), JsonOptions);
        featureAfter!.UpdatedAt.ShouldBeGreaterThan(featureBefore!.UpdatedAt);
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
    public async Task Patch_InlineSectionAfterUpsert_SucceedsWithoutResend()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "spec",
            Body = "# Spec\n\n<goal>\nOld goal.\n</goal>\n\n<anti-scope>No parser changes.</anti-scope>\n"
        });

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(spec.Id),
            new { Op = "replace_section", Section = "anti-scope", Content = "Parser stays anchored." });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{spec.Id}", JsonOptions);
        artifact!.Body.ShouldContain("<anti-scope>\nParser stays anchored.\n</anti-scope>");
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
        problem.Detail!.ShouldContain("1: [ ] first thing");
        problem.Detail!.ShouldContain("2: [ ] second thing");
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
        problem.Detail!.ShouldContain("address by index instead");
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
            new { Op = "tick_ac", AcIndex = 1 });

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
    [InlineData("tick_ac", null, null, 1, "first thing")]
    [InlineData("untick_ac", null, null, null, null)]
    [InlineData("untick_ac", null, null, 1, "first thing")]
    [InlineData("untick_ac", "goal", "content", null, null)]
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
    public async Task Patch_SetBrief_UpdatesTheTaskWithoutVersionBumpOrBodyChange()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var body = "# Task\n\n<goal>\nWire it.\n</goal>\n";
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = body,
            Brief = "first brief"
        });

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(task.Id),
            new { Op = "set_brief", Brief = "second brief" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(raw).RootElement.EnumerateObject().Select(p => p.Name).ToArray()
            .ShouldBe(["artifactId", "kind", "section", "version", "bodyChars", "status", "lastLogLine"]);

        var patch = await response.Content.ReadFromJsonAsync<PatchResultJson>(JsonOptions);
        patch!.ArtifactId.ShouldBe(task.Id);
        patch.Kind.ShouldBe("task");
        patch.Section.ShouldBe("brief");
        patch.Version.ShouldBe(1);
        patch.BodyChars.ShouldBe(body.Length);
        patch.LastLogLine.ShouldBe(string.Empty);

        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{task.Id}", JsonOptions);
        artifact!.Brief.ShouldBe("second brief");
        artifact.Body.ShouldBe(body);
        artifact.Version.ShouldBe(1);
    }

    [Theory]
    [InlineData("spec")]
    [InlineData("plan")]
    [InlineData("result")]
    public async Task Patch_SetBrief_OnNonTaskKind_Returns400BriefNotAllowedForKind(string kind)
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var artifact = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = kind, Body = "# Doc" });

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(artifact.Id),
            new { Op = "set_brief", Brief = "not allowed" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("BriefNotAllowedForKind");
    }

    [Theory]
    [InlineData(5000, 3)]
    [InlineData(501, 1)]
    [InlineData(20, 2)]
    public async Task Patch_SetBrief_OverCapOrMultiline_Returns400InvalidBrief(int length, int lineCount)
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat", new
        {
            Kind = "task",
            Body = "# Task\n\n<goal>\nWire it.\n</goal>\n"
        });
        var brief = string.Join('\n', Enumerable.Repeat(new string('x', length), lineCount));

        var response = await _client.PostAsJsonAsync(
            AgentPatchUrl(task.Id),
            new { Op = "set_brief", Brief = brief });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("InvalidBrief");
    }

    [Theory]
    [InlineData("replace_section", "goal", "New goal.", null)]
    [InlineData("tick_ac", null, null, 1)]
    [InlineData("untick_ac", null, null, 1)]
    public async Task Patch_BriefCombinedWithAnotherAction_Returns400InvalidPatchRequest(
        string op, string? section, string? content, int? acIndex)
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
            new { Op = op, Section = section, Content = content, AcIndex = acIndex, Brief = "stray brief" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("InvalidPatchRequest");
        problem.Detail!.ShouldContain("or a brief");
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
    public async Task Diff_AgentDoor_ServesTheSameContractAsTheHumanDoor()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var task = await CreateAgentArtifactAsync(project.Id, "feat",
            new { Kind = "task", Body = "old line", Order = 0 });
        await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"), new { Kind = "task", Body = "new line", Order = 0 });

        var response = await _client.GetAsync($"{AgentReadUrl(task.Id)}/diff");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var diff = await response.Content.ReadFromJsonAsync<DiffJson>(JsonOptions);
        diff!.ArtifactId.ShouldBe(task.Id);
        diff.FromVersion.ShouldBe(1);
        diff.ToVersion.ShouldBe(2);
        diff.Diff.ShouldContain("-old line");
        diff.Diff.ShouldContain("+new line");

        var human = await _client.GetAsync($"/api/planning/artifacts/{task.Id}/diff");
        human.StatusCode.ShouldBe(HttpStatusCode.OK);
        var humanDiff = await human.Content.ReadFromJsonAsync<DiffJson>(JsonOptions);
        humanDiff!.Diff.ShouldBe(diff.Diff);
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
        problem.Detail.ShouldStartWith($"Artifact '{missing}' was not found.");
        problem.Detail.ShouldContain("<slug>/T<order>");
        problem.Detail.ShouldContain("planning_list_artifacts");
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

    [Fact]
    public async Task Import_FeatureFolder_CreatesFeatureWithStatusesVerbatim()
    {
        var (project, root) = await CreateProjectWithBacklogFolderAsync();

        var response = await _client.PostAsJsonAsync(
            AgentImportUrl(project.Id), new { Path = Path.Combine(root, "BACKLOG", "058-demo") });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<FeatureImportJson>(JsonOptions);
        report!.Slug.ShouldBe("058-demo");
        report.Title.ShouldBe("Demo import");
        report.Status.ShouldBe("inProgress");
        report.Created.ShouldBeTrue();
        report.ArtifactsCreated.ShouldBe(2);
        report.ArtifactsUpdated.ShouldBe(0);
        report.TrackedFiles.ShouldBe(3);
        report.Materialization.ShouldBe("on");
        report.Notes.ShouldNotBeNull();

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/agent/projects/{project.Id}/planning/features/058-demo", JsonOptions);
        feature!.Status.ShouldBe("inProgress");
        feature.Version.ShouldBe(2);
        var spec = feature.Artifacts.Single(a => a.Kind == "spec");
        spec.Status.ShouldBe("validated");
        spec.FrozenAt.ShouldNotBeNull();
        var task = feature.Artifacts.Single(a => a.Kind == "task");
        task.Status.ShouldBe("review");
        task.Review.ShouldBe("hard");
        task.Brief.ShouldBe("Wire the import endpoint");
    }

    [Fact]
    public async Task Import_ReimportAfterDiskEdit_RestoresStatusWithImportMarker()
    {
        var (project, root) = await CreateProjectWithBacklogFolderAsync();
        var featureFolder = Path.Combine(root, "BACKLOG", "058-demo");
        var taskPath = Path.Combine(featureFolder, "tasks", "01-wire.md");

        var first = await _client.PostAsJsonAsync(AgentImportUrl(project.Id), new { Path = featureFolder });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        await File.WriteAllTextAsync(taskPath,
            (await File.ReadAllTextAsync(taskPath)).Replace("status: review", "status: done"));

        var second = await _client.PostAsJsonAsync(AgentImportUrl(project.Id), new { Path = featureFolder });

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await second.Content.ReadFromJsonAsync<FeatureImportJson>(JsonOptions);
        report!.Created.ShouldBeFalse();
        report.ArtifactsCreated.ShouldBe(0);
        report.ArtifactsUpdated.ShouldBe(1);

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/agent/projects/{project.Id}/planning/features/058-demo", JsonOptions);
        var task = feature!.Artifacts.Single(a => a.Kind == "task");
        task.Status.ShouldBe("done");
        task.Body.ShouldContain("[import] — status restored from imported file");
    }

    [Fact]
    public async Task Import_PathOutsideProjectRoot_Returns400()
    {
        var (project, _) = await CreateProjectWithBacklogFolderAsync();
        var outside = Path.Combine(Path.GetTempPath(), $"overmind-elsewhere-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);
        try
        {
            var response = await _client.PostAsJsonAsync(AgentImportUrl(project.Id), new { Path = outside });

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
            problem!.Title.ShouldBe("ImportFailed");
            problem.Detail!.ShouldContain("resolves outside the project directory");
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Import_BlankPath_Returns400WithoutTouchingDisk(string? path)
    {
        var (project, _) = await CreateProjectWithBacklogFolderAsync();

        var response = await _client.PostAsJsonAsync(AgentImportUrl(project.Id), new { Path = path });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Import_RelativePath_ReturnsValidationProblem()
    {
        var (project, _) = await CreateProjectWithBacklogFolderAsync();

        var response = await _client.PostAsJsonAsync(AgentImportUrl(project.Id), new { Path = "BACKLOG/058-demo" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var messages = body.GetProperty("errors").EnumerateObject().SelectMany(e => e.Value.EnumerateArray());
        messages.ShouldContain(m => m.GetString()!.Contains("absolute", StringComparison.Ordinal));
    }

    private static string AgentArtifactsUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts";

    private static string AgentTasksUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts/tasks";

    private static string AgentValidateUrl(Guid artifactId) =>
        $"/api/agent/planning/artifacts/{artifactId}/validate";

    private static string AgentStatusUrl(Guid artifactId) =>
        $"/api/agent/planning/artifacts/{artifactId}/status";

    private static string AgentFeatureStatusUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/status";

    private static string AgentFeatureUpdateUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/update";

    private static string AgentFeatureUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}";

    private static string AgentPatchUrl(Guid artifactId) =>
        $"/api/agent/planning/artifacts/{artifactId}/patch";

    private static string AgentLogUrl(Guid artifactId) =>
        $"/api/agent/planning/artifacts/{artifactId}/log";

    private static string AgentReadUrl(Guid artifactId) =>
        $"/api/agent/planning/artifacts/{artifactId}";

    private static string AgentDeleteUrl(Guid artifactId) =>
        $"/api/agent/planning/artifacts/{artifactId}";

    private async Task<HttpResponseMessage> DeleteArtifactAsync(Guid artifactId, object? body = null, bool agentDoor = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, agentDoor
            ? AgentDeleteUrl(artifactId)
            : $"/api/planning/artifacts/{artifactId}");

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }

    private static string AgentImportUrl(Guid projectId) =>
        $"/api/agent/projects/{projectId}/planning/import";

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

    private async Task<(ProjectJson Project, string Root)> CreateProjectWithBacklogFolderAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"overmind-import-api-{Guid.NewGuid():N}");
        var backlog = Path.Combine(root, "BACKLOG", "058-demo");
        Directory.CreateDirectory(Path.Combine(backlog, "tasks"));
        await File.WriteAllTextAsync(Path.Combine(backlog, "feature.md"),
            """
            ---
            feature: 058
            slug: demo
            status: in_progress
            priority: p1
            version: 2
            created: 2026-09-20
            ---

            # Feature 058 — Demo import

            <goal>
            Import it whole.
            </goal>
            """);
        await File.WriteAllTextAsync(Path.Combine(backlog, "spec.md"),
            """
            ---
            feature: 058
            slug: demo
            status: validated
            created: 2026-09-20
            validated: 2026-09-21
            ---

            # Spec 058 — Demo import

            Spec body.
            """);
        await File.WriteAllTextAsync(Path.Combine(backlog, "tasks", "01-wire.md"),
            """
            ---
            feature: 058
            task: 01
            slug: demo
            status: review
            review: hard
            created: 2026-09-20
            brief: Wire the import endpoint
            ---

            # Wire the import endpoint

            <goal>
            One call, whole folder.
            </goal>
            """);

        var name = $"agent-import-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = root,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return (project!, root);
    }

    private async Task<FeatureJson> CreateAgentFeatureAsync(Guid projectId, string slug, string title, string? priority = null)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = "", Priority = priority });

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

    public sealed record TaskBatchJson(int Created, int Updated, ArtifactJson[] Artifacts);

    public sealed record FeatureImportJson(
        string Slug, string Title, string Status, bool Created,
        int ArtifactsCreated, int ArtifactsUpdated, string[] Notes,
        int TrackedFiles, string Materialization, string? NextStep);

    public sealed record LogLineJson(string? Date, string Text);

    public sealed record VersionJson(int Version, string Body, DateTime CreatedAt);

    public sealed record DiffJson(Guid ArtifactId, int FromVersion, int ToVersion, string Diff);

    public sealed record RosterJson(RosterItemJson[] Artifacts);

    public sealed record RosterItemJson(Guid ArtifactId, string Kind, int Order);

    public sealed record CurrentJson(
        string State, CurrentFeatureJson Feature, ArtifactJson? Artifact, string? Reason,
        string[]? Warnings = null);

    public sealed record CurrentFeatureJson(string Slug, string Title, string Status);

    public sealed record PatchResultJson(
        Guid ArtifactId, string Kind, string Section, int Version, int BodyChars,
        string Status, string? LastLogLine,
        string? StatusChanged = null, string? FeatureStatusChanged = null,
        int? TickedIndex = null, string? TickedText = null, bool? NoChange = null);

    public sealed record FeatureMutationJson(string Slug, string Status, int Version, string LastLogLine);

    public sealed record FeatureUpdateJson(string Slug, string Status, int Version, DateTime UpdatedAt, int BodyChars);

    public sealed record ArtifactMutationJson(Guid Id, string Status, int Version, string LastLogLine);

    public sealed record ArtifactReadJson(
        Guid Id, string Kind, string Status, int Version, int Order,
        DateTime UpdatedAt, string? Brief, string Body, string[]? MissingSections = null);

    public sealed record DeleteJson(Guid Id, string Kind, string FeatureLogLine);

    public sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
