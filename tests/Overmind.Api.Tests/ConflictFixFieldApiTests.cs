using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Overmind.Core;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// 266 T4. The conflict family stays hard — same status, same failure code, same detail — and gains a
// machine-readable one-line remedy as the ProblemDetails extension "fix". The table pins the family
// seam by seam; the HTTP probes prove the field survives the wire. This is deliberately NOT the
// soft-cap warnings[] shape: warnings ride accepted payloads, fix rides rejections.
public sealed class ConflictFixFieldApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly string _sandboxDir;
    private readonly string _databasePath;

    public ConflictFixFieldApiTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"overmind-fixfield-{Guid.NewGuid():N}.db");
        _factory = new ApiFactory(_databasePath);
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-fixfield-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sandboxDir);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        if (Directory.Exists(_sandboxDir))
        {
            Directory.Delete(_sandboxDir, recursive: true);
        }
    }

    public sealed record FixRow(string Name, PlanningException Exception, int Status, string Verb);

    public static TheoryData<FixRow> Family => new(
    [
        new("claim-alive", PlanningException.FeatureClaimHeld("001-x", "Dan", "qwen", AgentSessionLiveness.Alive),
            409, "coordinate"),
        new("claim-stale", PlanningException.FeatureClaimHeld("001-x", "Dan", "qwen", AgentSessionLiveness.Stale),
            409, "planning_claim_feature"),
        new("claim-done", PlanningException.FeatureClaimRefused("001-x"), 409, "reopen"),
        new("lane-claim", PlanningException.LaneClaimHeld("lane-a", "001-x", "Dan", "qwen", AgentSessionLiveness.Alive),
            409, "overrideLaneCheck"),
        new("dup-slug", PlanningException.DuplicateSlug("001-x"), 409, "planning_create_feature"),
        new("dup-artifact", PlanningException.DuplicateArtifact(PlanningArtifactKind.Spec, 0), 409, "planning_upsert_artifact"),
        new("collision", PlanningException.NumberCollision("010-a", "010-b", PlanningFeatureStatus.Draft, 11),
            409, "resend"),
        new("batch-concurrent", PlanningException.TaskBatchConcurrentWrite(), 409, "retry"),
        new("batch-outside", PlanningException.BatchArtifactOutsideFeature(Guid.NewGuid(), "001-x"), 409, "planning_list_artifacts"),
        new("proof", PlanningException.ValidationProofRequired("001-x"), 409, "overmind_notify"),
        new("human-validation", PlanningException.HumanValidationRequired(PlanningArtifactKind.Plan, "001-x"),
            409, "overmind_notify"),
        new("result-required", PlanningException.FeatureResultRequired("001-x"), 409, "planning_upsert_artifact"),
        new("done-tap", PlanningException.FeatureDoneTapRequired("001-x"), 409, "overmind_notify"),
        new("change-note", PlanningException.ChangeNoteRequired(), 409, "lognote"),
        new("migrated-note", PlanningException.MigratedNoteRequired(), 400, "lognote"),
        new("amendment-note", PlanningException.AmendmentNoteRequired(), 400, "lognote"),
        new("validate-task", PlanningException.CannotValidateTask(), 409, "planning_set_status"),
        new("cycle", PlanningException.DependencyCycle("001-a", "002-b", ["002-b -> 003-c -> 001-a"]),
            409, "planning_remove_feature_dependency"),
        new("blocked", PlanningException.FeatureBlocked("001-a", ["002-b", "003-c"]), 409, "planning_current_task"),
        new("task-cycle", PlanningException.TaskDependencyCycle(3, [3, 4, 3]), 409, "planning_upsert_artifact"),
        new("delete-dependents", PlanningException.DeletionBlockedByDependents(["004-t", "005-u"]), 409, "planning_upsert_artifact"),
        new("delete-status", PlanningException.AgentDeletionRefused(PlanningArtifactKind.Spec, PlanningArtifactStatus.Validated),
            409, "overmind ui"),
        new("operation-id", PlanningException.OperationIdConflict(Guid.NewGuid()), 409, "payload"),
        new("ownership-artifact", PlanningException.ArtifactOutsideCallerProject(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            409, "planning_list_artifacts"),
        new("ownership-issue", PlanningException.IssueOutsideCallerProject(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            409, "issues_create"),
        new("ownership-entry", PlanningException.EntryOutsideCallerProject(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            409, "list_entries"),
        new("scope-unverifiable", PlanningException.AgentCallerScopeUnverifiable("no pair"), 400, "overmind_onboard"),
        new("status-door", PlanningException.FeatureStatusNotAllowedForAgent(PlanningFeatureStatus.Ready), 400, "overmind ui"),
        new("transition", PlanningException.InvalidFeatureTransition("001-x", PlanningFeatureStatus.Done, PlanningFeatureStatus.Review),
            400, "planning_set_feature_status"),
        new("feature-log-note", PlanningException.FeatureLogNoteRequired(), 400, "lognote"),
        new("issue-transition", PlanningException.IssueInvalidTransition(IssueStatus.Closed, IssueStatus.Open), 400, "issues_promote"),
        new("queue-done", PlanningException.FeatureNotInQueue("001-x"), 400, "planning_task_queue"),
        new("queue-position", PlanningException.InvalidQueuePosition("001-x", 9, 4), 400, "planning_move_feature"),
        new("queue-reorder", PlanningException.InvalidQueueReorder(FeaturePriority.P2, "gap at 3"), 400, "planning_task_queue"),
        new("lane-dup", PlanningException.LaneNameDuplicate("lane-a"), 409, "payload"),
        new("lane-not-roster", PlanningException.LaneNotInRoster("lane-z", ["lane-a"]), 409, "planning_create_lane"),
        new("lane-birth", PlanningException.LaneBirthRestricted("lane-z"), 400, "planning_create_lane"),
        new("lane-occupied", PlanningException.LaneStillOccupied("lane-a", ["001-x"]), 409, "planning_update_feature"),
        new("lane-roster-retired", PlanningException.LaneRosterWriteRetired("lanes", "lane-roster"), 410, "PUT"),
        new("log-not-newest", PlanningException.LogEditNotNewest(PlanningLogSource.Agent, PlanningLogSource.Ui, 2, 3),
            409, "planning_append_log"),
        new("log-empty", PlanningException.LogEditEmptyLog(), 409, "planning_append_log"),
        new("log-actor", PlanningException.LogEditWrongActor(PlanningLogSource.Agent, PlanningLogSource.Ui),
            409, "planning_append_log"),
        new("export-notempty", PlanningException.ExportTargetNotEmpty("/tmp/x"), 409, "empty"),
        new("export-mirror", PlanningException.ExportTargetInsideMirror("BACKLOG/x"), 409, "BACKLOG"),
        new("directive-cap", PlanningException.DirectiveTextTooLong(3000, 2000, 2400, "tail"), 400, "planning_append_directive"),
    ]);

    [Theory]
    [MemberData(nameof(Family))]
    public void ConflictFamily_FixedShape_KeepsStatusCodeDetailAndGainsFix(FixRow row)
    {
        var problem = ((ProblemHttpResult)PlanningErrors.Map(row.Exception)).ProblemDetails;

        problem.Status.ShouldBe(row.Status);
        problem.Title.ShouldBe(row.Exception.Code.ToString());
        problem.Detail.ShouldBe(row.Exception.Message);
        problem.Detail.ShouldNotBe(problem.Title);

        problem.Extensions.TryGetValue("fix", out var fix).ShouldBeTrue($"'{row.Name}' must carry a fix");
        var text = fix.ShouldBeOfType<string>();
        text.ShouldNotBeNullOrWhiteSpace();
        text.ShouldContain(row.Verb, Case.Insensitive, $"'{row.Name}' fix must name the actionable move");
        text.ShouldNotContain("\n");
        text.Length.ShouldBeLessThanOrEqualTo(400);
    }

    [Theory]
    [MemberData(nameof(MechanicalRejections))]
    public void MechanicalRejections_CarryNoFix(PlanningException exception)
    {
        var problem = ((ProblemHttpResult)PlanningErrors.Map(exception)).ProblemDetails;

        problem.Extensions.ContainsKey("fix").ShouldBeFalse($"{exception.Code} is not a state conflict");
    }

    public static TheoryData<PlanningException> MechanicalRejections => new(
    [
        PlanningException.InvalidSlug("BAD SLUG"),
        PlanningException.TickItemNotFoundByIndex(9, []),
        PlanningException.PatchSectionNotFound("goal", []),
        PlanningException.InvalidBrief(),
        PlanningException.WaveTextTooLong(900, 500),
        PlanningException.MissingFeature("nope"),
    ]);

    [Fact]
    public void BatchWrapping_PreservesTheInnerFix()
    {
        var inner = PlanningException.ValidationProofRequired("001-x");

        var wrapped = PlanningException.TaskBatchItem(2, inner);

        var problem = ((ProblemHttpResult)PlanningErrors.Map(wrapped)).ProblemDetails;
        problem.Detail.ShouldStartWith("tasks[2]: ");
        problem.Title.ShouldBe("ValidationProofRequired");
        problem.Extensions["fix"].ShouldBe(inner.Fix);
    }

    [Fact]
    public async Task DuplicateSlug_Http409_BodyCarriesFixAtTopLevel()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "dup-fix", "First");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features",
            new { Slug = "dup-fix", Title = "Second", Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("title").GetString().ShouldBe("DuplicateSlug");
        body.GetProperty("status").GetInt32().ShouldBe(409);
        body.GetProperty("fix").GetString().ShouldContain("planning_create_feature");
    }

    [Fact]
    public async Task ClaimLock_Http409_FixNamesTheMoveForTheForeignSession()
    {
        var project = await CreateProjectAsync();
        var slug = $"lock-{Guid.NewGuid():N}"[..26];
        await CreateFeatureAsync(project.Id, slug, "Locked");
        await SeedSessionAsync(HolderSession, "Holder", "qwen");
        await SeedSessionAsync(ForeignSession, "Foreign", "claude");

        var claim = await SendWithSessionAsync(
            HttpMethod.Post, $"/api/agent/projects/{project.Id}/planning/features/{slug}/claim",
            new { }, HolderSession);
        claim.StatusCode.ShouldBe(HttpStatusCode.OK);

        var update = await SendWithSessionAsync(
            HttpMethod.Post, $"/api/agent/projects/{project.Id}/planning/features/{slug}/update",
            new { LogNote = "intruder" }, ForeignSession);

        update.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = JsonDocument.Parse(await update.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("title").GetString().ShouldBe("FeatureClaimHeld");
        body.GetProperty("detail").GetString()!.ShouldContain("claim held by Holder by qwen");
        var fix = body.GetProperty("fix").GetString();
        fix.ShouldContain("Coordinate with Holder");
        fix.ShouldContain("planning_unclaim_feature");
    }

    private static readonly Guid HolderSession = Guid.NewGuid();
    private static readonly Guid ForeignSession = Guid.NewGuid();

    private async Task<HttpResponseMessage> SendWithSessionAsync(
        HttpMethod method, string url, object body, Guid sessionId)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        request.Headers.Add("X-Overmind-Session", sessionId.ToString());
        return await _client.SendAsync(request);
    }

    private async Task SeedSessionAsync(Guid sessionId, string alias, string vendor)
    {
        using var db = new SqliteConnection($"Data Source={_databasePath};Pooling=false");
        await db.OpenAsync();
        using var command = db.CreateCommand();
        command.CommandText = $"""
            INSERT OR REPLACE INTO AgentSessions (Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{sessionId.ToString().ToUpperInvariant()}', '{alias}', '{vendor}', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', NULL)
            """;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var name = $"fixfield-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = Path.Combine(_sandboxDir, name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions))!;
    }

    private async Task CreateFeatureAsync(Guid projectId, string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = "" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);
}
