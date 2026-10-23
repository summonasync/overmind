using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PlanningQueueApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;
    private readonly string _projectDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PlanningQueueApiTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-queue-{Guid.NewGuid():N}");
        _projectDir = Path.Combine(_sandboxDir, "project");
        Directory.CreateDirectory(_projectDir);
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

    [Fact]
    public async Task Board_CarriesQueuePositionInCreationOrder()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "board-a", "Board A");
        await CreateFeature(project.Id, "board-b", "Board B");
        await CreateFeature(project.Id, "board-c", "Board C");

        var board = (await GetBoardAsync(project.Id))!;

        board.Single(f => f.Slug == "board-a").QueuePosition.ShouldBe(1);
        board.Single(f => f.Slug == "board-b").QueuePosition.ShouldBe(2);
        board.Single(f => f.Slug == "board-c").QueuePosition.ShouldBe(3);
    }

    [Fact]
    public async Task QueueRead_RowsCarryFeatureTimestamps()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "stamp-a", "Stamp A");

        var queue = await GetQueueAsync(project.Id);
        var row = queue!.Items.Single(i => i.Slug == "stamp-a");
        row.CreatedAt.ShouldNotBe(default);
        row.UpdatedAt.ShouldNotBe(default);
        row.UpdatedAt.ShouldBeGreaterThanOrEqualTo(row.CreatedAt);

        var board = await GetBoardAsync(project.Id);
        board!.Single(f => f.Slug == "stamp-a").CreatedAt.ShouldNotBe(default);
    }

    [Fact]
    public async Task QueueRead_SortsPriorityThenPosition_ExcludesDone()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "p2-first", "P2 first", "p2");
        await CreateFeature(project.Id, "p2-second", "P2 second", "p2");
        await CreateFeature(project.Id, "p1-solo", "P1 solo", "p1");
        await CreateFeature(project.Id, "p0-solo", "P0 solo", "p0");
        await BringToStatusAsync(project.Id, "p2-first", "done");

        var queue = await GetQueueAsync(project.Id);

        queue!.Items.Select(i => i.Slug).ShouldBe(["p0-solo", "p1-solo", "p2-second"]);
        queue.Items.Select(i => i.Priority).ShouldBe(["p0", "p1", "p2"]);
        queue.Items.Single(i => i.Slug == "p2-second").QueuePosition.ShouldBe(2);
        queue.Items.ShouldNotContain(i => i.Slug == "p2-first");
    }

    [Fact]
    public async Task QueueRead_DecoratesReadinessBlockersAndCurrent()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "q-decorated", "Decorated");
        await CreateFeature(project.Id, "q-blocker", "Blocker");
        await CreateFeature(project.Id, "99-numbered", "Numbered");

        var specId = await CreateArtifactAsync(project.Id, "q-decorated", "spec");
        await ValidateArtifactAsync(specId);
        var queueAfterSpec = await GetQueueAsync(project.Id);
        queueAfterSpec!.Items.Single(i => i.Slug == "q-decorated").Readiness.ShouldBe(1);

        var planId = await CreateArtifactAsync(project.Id, "q-decorated", "plan");
        await ValidateArtifactAsync(planId);
        await CreateTaskAsync(project.Id, "q-decorated");
        await CreateTaskAsync(project.Id, "q-decorated");

        await AddDependencyAsync(project.Id, "q-decorated", "q-blocker");

        var queue = await GetQueueAsync(project.Id);
        var decorated = queue!.Items.Single(i => i.Slug == "q-decorated");
        decorated.Readiness.ShouldBe(3);
        decorated.TaskTotal.ShouldBe(2);
        decorated.TaskDone.ShouldBe(0);
        decorated.TaskCounts.Todo.ShouldBe(2);
        decorated.TaskCounts.Done.ShouldBe(0);
        decorated.BlockedBy.ShouldBe(["q-blocker"]);
        decorated.IsBlocked.ShouldBeTrue();

        var blocker = queue.Items.Single(i => i.Slug == "q-blocker");
        blocker.BlockedBy.ShouldNotBeNull();
        blocker.BlockedBy.ShouldBeEmpty();
        blocker.IsBlocked.ShouldBeFalse();
        blocker.Readiness.ShouldBe(0);
        blocker.Number.ShouldBeNull();

        queue.Items.Single(i => i.Slug == "99-numbered").Number.ShouldBe("99");
    }

    [Fact]
    public async Task QueueRead_SpecAndPlanValidatedWithoutTasks_ReadinessIsTwo()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "rdy-two", "Ready two");

        var specId = await CreateArtifactAsync(project.Id, "rdy-two", "spec");
        await ValidateArtifactAsync(specId);
        var planId = await CreateArtifactAsync(project.Id, "rdy-two", "plan");
        await ValidateArtifactAsync(planId);

        var queue = await GetQueueAsync(project.Id);

        queue!.Items.Single(i => i.Slug == "rdy-two").Readiness.ShouldBe(2);
    }

    [Fact]
    public async Task QueueRead_LimitTruncatesFromTheHead()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "lim-a", "Lim A");
        await CreateFeature(project.Id, "lim-b", "Lim B");
        await CreateFeature(project.Id, "lim-c", "Lim C");

        var full = await GetQueueAsync(project.Id);
        full!.Items.Length.ShouldBe(3);

        var truncated = await GetQueueAsync(project.Id, limit: 2);
        truncated!.Items.Select(i => i.Slug).ShouldBe(["lim-a", "lim-b"]);
    }

    [Fact]
    public async Task QueueRead_RowsCarryMirrorLagAfterALandedPass()
    {
        var project = await CreateProjectAsync();
        await _client.PutAsJsonAsync("/api/settings", new { PlanningMaterializationSync = "on" });
        await CreateFeature(project.Id, "lag-a", "Lag A");

        using var raw = await _client.GetFromJsonAsync<JsonDocument>(
            $"/api/agent/projects/{project.Id}/planning/task-queue", JsonOptions);
        var row = raw!.RootElement.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("slug").GetString() == "lag-a");

        row.GetProperty("pendingMutations").GetInt32().ShouldBe(0);
        row.GetProperty("stale").GetBoolean().ShouldBeFalse();
        row.GetProperty("lastAttemptAt").GetDateTime().ShouldNotBe(default);
        row.GetProperty("lastSuccessAt").GetDateTime().ShouldNotBe(default);
    }

    [Fact]
    public async Task QueueRead_FailedAttemptReportsStaleUntilTheNextPassLands()
    {
        var project = await CreateProjectAsync();
        await _client.PutAsJsonAsync("/api/settings", new { PlanningMaterializationSync = "on" });
        await CreateFeature(project.Id, "lag-stale", "Lag stale");

        var clean = await GetQueueAsync(project.Id);
        var cleanRow = clean!.Items.Single(i => i.Slug == "lag-stale");
        cleanRow.Stale.ShouldBeFalse();

        var featureFile = Path.Combine(
            Directory.GetDirectories(Path.Combine(_projectDir, "BACKLOG"))
                .Single(d => Path.GetFileName(d).EndsWith("lag-stale")),
            "feature.md");
        await File.AppendAllTextAsync(featureFile, "\nhand edit\n");

        await CreateFeature(project.Id, "lag-second", "Lag second");

        var failed = await GetQueueAsync(project.Id);
        var failedRow = failed!.Items.Single(i => i.Slug == "lag-stale");
        failedRow.Stale.ShouldBeTrue();
        failedRow.PendingMutations.ShouldBe(0);
        failedRow.LastAttemptAt.ShouldNotBeNull();
        failedRow.LastSuccessAt.ShouldNotBeNull();
        failedRow.LastAttemptAt!.Value.ShouldBeGreaterThan(failedRow.LastSuccessAt!.Value);

        var mirrored = await File.ReadAllTextAsync(featureFile);
        await File.WriteAllTextAsync(featureFile, mirrored.Replace("\nhand edit\n", string.Empty));

        await CreateFeature(project.Id, "lag-third", "Lag third");

        var recovered = await GetQueueAsync(project.Id);
        recovered!.Items.Single(i => i.Slug == "lag-stale").Stale.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task QueueRead_NonPositiveLimit_Returns400(int limit)
    {
        var project = await CreateProjectAsync();

        var response = await _client.GetAsync(
            $"/api/agent/projects/{project.Id}/planning/task-queue?limit={limit}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem!.Errors.ShouldContainKey("Limit");
    }

    [Fact]
    public async Task QueueRead_UnknownProject_Returns404()
    {
        var response = await _client.GetAsync($"/api/agent/projects/{Guid.NewGuid()}/planning/task-queue");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task QueueRead_IsAgentDoorOnly()
    {
        var project = await CreateProjectAsync();

        var humanDoor = await _client.GetAsync($"/api/projects/{project.Id}/planning/task-queue");
        humanDoor.StatusCode.ShouldBe(HttpStatusCode.OK);
        humanDoor.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
    }

    [Fact]
    public async Task Reorder_FullBand_RenumbersAndReturnsBand()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "band-a", "Band A");
        await CreateFeature(project.Id, "band-b", "Band B");
        await CreateFeature(project.Id, "band-c", "Band C");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/queue/reorder",
            new { Priority = "p2", Slugs = new[] { "band-c", "band-a", "band-b" } });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var band = await response.Content.ReadFromJsonAsync<QueueResponseJson>(JsonOptions);
        band!.Items.Select(i => i.Slug).ShouldBe(["band-c", "band-a", "band-b"]);
        band.Items.Select(i => i.QueuePosition).ShouldBe([1, 2, 3]);

        var queue = await GetQueueAsync(project.Id);
        queue!.Items.Select(i => i.Slug).ShouldBe(["band-c", "band-a", "band-b"]);
    }

    [Fact]
    public async Task Reorder_PartialList_Returns400NamingTheGap()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "band-a", "Band A");
        await CreateFeature(project.Id, "band-b", "Band B");

        var response = await ReorderAsync(project.Id, "p2", ["band-a"]);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("InvalidQueueReorder");
        problem.Detail!.ShouldContain("missing");
        problem.Detail!.ShouldContain("band-b");
    }

    [Fact]
    public async Task Reorder_ForeignSlug_Returns400NamingIt()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "band-a", "Band A");

        var response = await ReorderAsync(project.Id, "p2", ["band-a", "ghost-slug"]);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("InvalidQueueReorder");
        problem.Detail!.ShouldContain("ghost-slug");
    }

    [Fact]
    public async Task Reorder_CrossBandSlug_Returns400NamingThePriority()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "band-a", "Band A");
        await CreateFeature(project.Id, "other-band", "Other band", "p1");

        var response = await ReorderAsync(project.Id, "p2", ["band-a", "other-band"]);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("InvalidQueueReorder");
        problem.Detail!.ShouldContain("other-band");
        problem.Detail!.ShouldContain("p1");
    }

    [Fact]
    public async Task Reorder_DoneFeatureCannotReenter_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "band-a", "Band A");
        await CreateFeature(project.Id, "band-b", "Band B");
        await BringToStatusAsync(project.Id, "band-b", "done");

        var response = await ReorderAsync(project.Id, "p2", ["band-a", "band-b"]);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("InvalidQueueReorder");
        problem.Detail!.ShouldContain("band-b");
    }

    [Fact]
    public async Task Reorder_UnknownProject_Returns404()
    {
        var response = await ReorderAsync(Guid.NewGuid(), "p2", ["band-a"]);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reorder_UnknownPriority_Returns400()
    {
        var project = await CreateProjectAsync();

        var response = await ReorderAsync(project.Id, "p9", ["band-a"]);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem!.Errors.ShouldContainKey("Priority");
    }

    [Fact]
    public async Task Reorder_IsHumanDoorOnly()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "band-a", "Band A");

        var agentDoor = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/queue/reorder",
            new { Priority = "p2", Slugs = new[] { "band-a" } });

        agentDoor.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task QueueMove_FiveMoves_LandCorrectly()
    {
        var project = await CreateProjectAsync();
        foreach (var slug in new[] { "mv-a", "mv-b", "mv-c", "mv-d" })
        {
            await CreateFeature(project.Id, slug, $"Move {slug}");
        }

        var up = await MoveAsync(project.Id, "mv-d", "up");
        up!.Slug.ShouldBe("mv-d");
        up.QueuePosition.ShouldBe(3);
        (await QueueOrderAsync(project.Id)).ShouldBe(["mv-a", "mv-b", "mv-d", "mv-c"]);

        await MoveAsync(project.Id, "mv-a", "down");
        (await QueueOrderAsync(project.Id)).ShouldBe(["mv-b", "mv-a", "mv-d", "mv-c"]);

        var first = await MoveAsync(project.Id, "mv-c", "first");
        first!.QueuePosition.ShouldBe(1);
        (await QueueOrderAsync(project.Id)).ShouldBe(["mv-c", "mv-b", "mv-a", "mv-d"]);

        var last = await MoveAsync(project.Id, "mv-b", "last");
        last!.QueuePosition.ShouldBe(4);
        (await QueueOrderAsync(project.Id)).ShouldBe(["mv-c", "mv-a", "mv-d", "mv-b"]);

        var toPosition = await MoveAsync(project.Id, "mv-d", "toPosition", 1);
        toPosition!.QueuePosition.ShouldBe(1);
        (await QueueOrderAsync(project.Id)).ShouldBe(["mv-d", "mv-c", "mv-a", "mv-b"]);
    }

    [Fact]
    public async Task QueueMove_AtBandEdges_IsSuccessfulNoOp()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "mv-a", "Move A");
        await CreateFeature(project.Id, "mv-b", "Move B");

        var head = await MoveAsync(project.Id, "mv-a", "up");
        head!.QueuePosition.ShouldBe(1);
        (await QueueOrderAsync(project.Id)).ShouldBe(["mv-a", "mv-b"]);

        var tail = await MoveAsync(project.Id, "mv-b", "down");
        tail!.QueuePosition.ShouldBe(2);
        (await QueueOrderAsync(project.Id)).ShouldBe(["mv-a", "mv-b"]);
    }

    [Fact]
    public async Task QueueMove_PositionIsRequiredOnlyForToPosition()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "mv-a", "Move A");
        await CreateFeature(project.Id, "mv-b", "Move B");

        var missing = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/mv-a/queue-move",
            new { Move = "toPosition" });
        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var missingProblem = await missing.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        missingProblem!.Errors.ShouldContainKey("Position");
        missingProblem.Errors["Position"].ShouldContain(m => m.Contains("toPosition"));

        var extra = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/mv-a/queue-move",
            new { Move = "up", Position = 2 });
        extra.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var extraProblem = await extra.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        extraProblem!.Errors.ShouldContainKey("Position");

        var unknown = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/mv-a/queue-move",
            new { Move = "sideways" });
        unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var unknownProblem = await unknown.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        unknownProblem!.Errors.ShouldContainKey("Move");
        unknownProblem.Errors["Move"].ShouldContain(m => m.Contains("up, down, first, last, toPosition"));

        var nonPositive = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/mv-a/queue-move",
            new { Move = "toPosition", Position = 0 });
        nonPositive.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var nonPositiveProblem = await nonPositive.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        nonPositiveProblem!.Errors.ShouldContainKey("Position");
        nonPositiveProblem.Errors["Position"].ShouldContain(m => m.Contains("positive"));

        (await QueueOrderAsync(project.Id)).ShouldBe(["mv-a", "mv-b"]);
    }

    [Fact]
    public async Task QueueMove_PositionOutsideBand_Returns400NamingTheRange()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "mv-a", "Move A");
        await CreateFeature(project.Id, "mv-b", "Move B");

        var response = await MoveRawAsync(project.Id, "mv-a", "toPosition", 5);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("InvalidQueuePosition");
        problem.Detail!.ShouldContain("1..2");
    }

    [Fact]
    public async Task QueueMove_DoneFeature_Returns400FeatureNotInQueue()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "mv-a", "Move A");
        await BringToStatusAsync(project.Id, "mv-a", "done");

        var response = await MoveRawAsync(project.Id, "mv-a", "up", null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("FeatureNotInQueue");
    }

    [Fact]
    public async Task QueueMove_UnknownFeature_Returns404()
    {
        var project = await CreateProjectAsync();

        var response = await MoveRawAsync(project.Id, "ghost", "up", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task QueueMove_UnknownProject_Returns404()
    {
        var response = await MoveRawAsync(Guid.NewGuid(), "mv-a", "up", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HumanDoorMove_UpAndDown_SwapWithinBand_AndReorderIsReadable()
    {
        var project = await CreateProjectAsync();
        foreach (var slug in new[] { "hd-a", "hd-b", "hd-c" })
        {
            await CreateFeature(project.Id, slug, $"Human door {slug}");
        }

        var up = await HumanMoveAsync(project.Id, "hd-c", "up");
        up.Slug.ShouldBe("hd-c");
        up.QueuePosition.ShouldBe(2);
        (await QueueOrderAsync(project.Id)).ShouldBe(["hd-a", "hd-c", "hd-b"]);

        var down = await HumanMoveAsync(project.Id, "hd-a", "down");
        down.QueuePosition.ShouldBe(2);
        (await QueueOrderAsync(project.Id)).ShouldBe(["hd-c", "hd-a", "hd-b"]);

        var board = (await GetBoardAsync(project.Id))!;
        board.Single(f => f.Slug == "hd-c").QueuePosition.ShouldBe(1);
        board.Single(f => f.Slug == "hd-a").QueuePosition.ShouldBe(2);
        board.Single(f => f.Slug == "hd-b").QueuePosition.ShouldBe(3);
    }

    [Fact]
    public async Task HumanDoorMove_FirstLastAndToPosition_AreAvailableOnTheHumanDoor()
    {
        var project = await CreateProjectAsync();
        foreach (var slug in new[] { "hd-a", "hd-b", "hd-c", "hd-d" })
        {
            await CreateFeature(project.Id, slug, $"Human door {slug}");
        }

        var first = await HumanMoveAsync(project.Id, "hd-d", "first");
        first.QueuePosition.ShouldBe(1);
        (await QueueOrderAsync(project.Id)).ShouldBe(["hd-d", "hd-a", "hd-b", "hd-c"]);

        var last = await HumanMoveAsync(project.Id, "hd-d", "last");
        last.QueuePosition.ShouldBe(4);
        (await QueueOrderAsync(project.Id)).ShouldBe(["hd-a", "hd-b", "hd-c", "hd-d"]);

        var toPosition = await HumanMoveAsync(project.Id, "hd-c", "toPosition", 2);
        toPosition.QueuePosition.ShouldBe(2);
        (await QueueOrderAsync(project.Id)).ShouldBe(["hd-a", "hd-c", "hd-b", "hd-d"]);
    }

    [Fact]
    public async Task HumanDoorMove_AtBandEdges_IsSuccessfulNoOp()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "hd-a", "Human door A");
        await CreateFeature(project.Id, "hd-b", "Human door B");

        var head = await HumanMoveAsync(project.Id, "hd-a", "up");
        head.QueuePosition.ShouldBe(1);

        var tail = await HumanMoveAsync(project.Id, "hd-b", "down");
        tail.QueuePosition.ShouldBe(2);

        (await QueueOrderAsync(project.Id)).ShouldBe(["hd-a", "hd-b"]);
    }

    [Fact]
    public async Task HumanDoorMove_KeepsEveryFeatureInItsOwnPriorityBand()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "hd-p1", "Human door P1", "p1");
        await CreateFeature(project.Id, "hd-a", "Human door A");
        await CreateFeature(project.Id, "hd-b", "Human door B");

        await HumanMoveAsync(project.Id, "hd-p1", "down");
        await HumanMoveAsync(project.Id, "hd-a", "up");
        await HumanMoveAsync(project.Id, "hd-b", "up");

        var queue = await GetQueueAsync(project.Id);
        queue!.Items.Select(i => i.Slug).ShouldBe(["hd-p1", "hd-b", "hd-a"]);
        queue.Items.Select(i => i.Priority).ShouldBe(["p1", "p2", "p2"]);
        queue.Items.Single(i => i.Slug == "hd-p1").QueuePosition.ShouldBe(1);
        queue.Items.Single(i => i.Slug == "hd-b").QueuePosition.ShouldBe(1);
        queue.Items.Single(i => i.Slug == "hd-a").QueuePosition.ShouldBe(2);

        var board = (await GetBoardAsync(project.Id))!;
        board.Single(f => f.Slug == "hd-p1").Priority.ShouldBe("p1");
        board.Single(f => f.Slug == "hd-b").Priority.ShouldBe("p2");
    }

    [Fact]
    public async Task HumanDoorMove_DoneFeature_Returns400FeatureNotInQueue()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "hd-a", "Human door A");
        await CreateFeature(project.Id, "hd-b", "Human door B");
        await BringToStatusAsync(project.Id, "hd-b", "done");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/hd-b/queue-move", new { Move = "up" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Status.ShouldBe(400);
        problem.Title.ShouldBe("FeatureNotInQueue");
        problem.Detail!.ShouldContain("hd-b");
        (await QueueOrderAsync(project.Id)).ShouldBe(["hd-a"]);
    }

    [Fact]
    public async Task HumanDoorMove_UnknownFeature_Returns404NamingSlugInDetail()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/hd-ghost/queue-move", new { Move = "up" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Detail!.ShouldContain("hd-ghost");
    }

    [Theory]
    [InlineData("toPosition", null, "Position")]
    [InlineData("up", 2, "Position")]
    [InlineData("sideways", null, "Move")]
    [InlineData("toPosition", 0, "Position")]
    public async Task HumanDoorMove_InvalidMoveShape_Returns400ValidationProblem(
        string move, int? position, string expectedField)
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "hd-a", "Human door A");
        await CreateFeature(project.Id, "hd-b", "Human door B");

        var body = new Dictionary<string, object?> { ["Move"] = move };
        if (position.HasValue)
        {
            body["Position"] = position.Value;
        }

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/hd-a/queue-move", body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem!.Errors.ShouldContainKey(expectedField);
        (await QueueOrderAsync(project.Id)).ShouldBe(["hd-a", "hd-b"]);
    }

    private async Task<QueueItemJson> HumanMoveAsync(Guid projectId, string slug, string move, int? position = null)
    {
        var body = new Dictionary<string, object?> { ["Move"] = move };
        if (position.HasValue)
        {
            body["Position"] = position.Value;
        }

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}/queue-move", body);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<QueueItemJson>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> ReorderAsync(Guid projectId, string priority, string[] slugs) =>
        await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/queue/reorder",
            new { Priority = priority, Slugs = slugs });

    private async Task<QueueItemJson?> MoveAsync(Guid projectId, string slug, string move, int? position = null)
    {
        var response = await MoveRawAsync(projectId, slug, move, position);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<QueueItemJson>(JsonOptions);
    }

    private async Task<HttpResponseMessage> MoveRawAsync(Guid projectId, string slug, string move, int? position)
    {
        var body = new Dictionary<string, object?> { ["Move"] = move };
        if (position.HasValue)
        {
            body["Position"] = position.Value;
        }

        return await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features/{slug}/queue-move", body);
    }

    private async Task<string[]> QueueOrderAsync(Guid projectId)
    {
        var queue = await GetQueueAsync(projectId);
        return queue!.Items.Select(i => i.Slug).ToArray();
    }

    private async Task<QueueResponseJson?> GetQueueAsync(Guid projectId, int? limit = null)
    {
        var route = $"/api/agent/projects/{projectId}/planning/task-queue";
        if (limit.HasValue)
        {
            route += $"?limit={limit.Value}";
        }

        var response = await _client.GetAsync(route);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<QueueResponseJson>(JsonOptions);
    }

    private async Task<BoardItemJson[]?> GetBoardAsync(Guid projectId) =>
        await _client.GetFromJsonAsync<BoardItemJson[]>(
            $"/api/projects/{projectId}/planning/features", JsonOptions);

    private async Task<Guid> CreateArtifactAsync(Guid projectId, string slug, string kind)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}/artifacts",
            new { Kind = kind, Body = $"# {kind}" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var artifact = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        return artifact!.Id;
    }

    private async Task ValidateArtifactAsync(Guid artifactId)
    {
        var response = await _client.PostAsync($"/api/planning/artifacts/{artifactId}/validate", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task CreateTaskAsync(Guid projectId, string slug)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts/tasks",
            new { Tasks = new[] { new { Body = "do the thing" } } });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task AddDependencyAsync(Guid projectId, string slug, string blockedBy)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}/dependencies",
            new { BlockedBy = blockedBy });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task BringToStatusAsync(Guid projectId, string slug, string target)
    {
        string[] path = target switch
        {
            "inProgress" => ["inProgress"],
            "review" => ["inProgress", "review"],
            "done" => ["inProgress", "review", "done"],
            _ => throw new ArgumentException($"unknown status '{target}'"),
        };

        if (target == "done")
        {
            var seed = await _client.PostAsJsonAsync(
                $"/api/projects/{projectId}/planning/features/{slug}/artifacts",
                new { Kind = "result", Body = "close-out" });
            seed.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        }

        foreach (var step in path)
        {
            var response = await _client.PutAsJsonAsync(
                $"/api/projects/{projectId}/planning/features/{slug}",
                new { Status = step });
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"queue-proj-{Guid.NewGuid():N}",
            DiskPath = _projectDir,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task CreateFeature(Guid projectId, string slug, string title, string? priority = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["Slug"] = slug,
            ["Title"] = title,
            ["Body"] = "",
        };
        if (priority is not null)
        {
            body["Priority"] = priority;
        }

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features", body);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record BoardItemJson(
        string Slug, string Title, string Status, string Priority, int Version,
        string? SpecStatus, string? PlanStatus, int TaskTotal, int TaskDone,
        TaskCountsJson TaskCounts, DateTime UpdatedAt, bool IsBlocked, int QueuePosition,
        DateTime CreatedAt);

    public sealed record QueueResponseJson(QueueItemJson[] Items);

    public sealed record QueueItemJson(
        string Slug, string? Number, string Title, string Priority, string Status, int QueuePosition,
        string? SpecStatus, string? PlanStatus, int TaskDone, int TaskTotal,
        TaskCountsJson TaskCounts, int Readiness, string[] BlockedBy, bool IsBlocked,
        DateTime CreatedAt, DateTime UpdatedAt,
        int PendingMutations = 0, DateTime? LastAttemptAt = null, DateTime? LastSuccessAt = null,
        bool Stale = false);

    public sealed record TaskCountsJson(int Todo, int InProgress, int Review, int Done, int Blocked);

    public sealed record ArtifactJson(Guid Id);

    public sealed record ProblemJson(string? Title, int? Status, string? Detail);

    public sealed record ValidationProblemJson(Dictionary<string, string[]> Errors);
}
