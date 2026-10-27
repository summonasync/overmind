using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class LanesApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;
    private readonly string _projectDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public LanesApiTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-lanes-{Guid.NewGuid():N}");
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
    public async Task PutLanes_OnAgentDoor_UpdatesAKnownLaneInPlace()
    {
        var project = await CreateProjectAsync();
        await SeedLaneAsync(project.Id, "frontend");

        var put = await _client.PutAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/lanes-roster",
            new { Lanes = new[] { new { Name = "frontend", Contour = "web/src", SerialOrder = 1, Priority = 2 } } });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var roster = await GetRosterAsync(project.Id, agentDoor: true);
        roster!.Lanes.ShouldHaveSingleItem();
        roster.Lanes[0].Name.ShouldBe("frontend");
        roster.Lanes[0].Contour.ShouldBe("web/src");
        roster.Lanes[0].SerialOrder.ShouldBe(1);
        roster.Lanes[0].Priority.ShouldBe(2);
        roster.Lanes[0].OpenFeatureCount.ShouldBe(0);
        roster.Lanes[0].Holder.ShouldBeNull();
        roster.Lanes[0].Features.ShouldBeEmpty();
    }

    [Fact]
    public async Task PutLanes_PriorityOutOfRange_Returns400()
    {
        var project = await CreateProjectAsync();

        var put = await _client.PutAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/lanes-roster",
            new { Lanes = new[] { new { Name = "frontend", Priority = 4 } } });

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await put.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem!.Errors.ShouldContainKey("Lanes[0].Priority");
    }

    [Fact]
    public async Task PutLanes_NameOverTheCap_Returns400()
    {
        var project = await CreateProjectAsync();

        var put = await _client.PutAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/lanes-roster",
            new { Lanes = new[] { new { Name = new string('x', PlanningLane.MaxNameLength + 1) } } });

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await put.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem!.Errors.ShouldContainKey("Lanes[0].Name");
    }

    [Fact]
    public async Task PutLanes_ContourOverTheCap_Returns400NamingTheField()
    {
        var project = await CreateProjectAsync();

        var put = await _client.PutAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/lanes-roster",
            new { Lanes = new[] { new { Name = "frontend", Contour = new string('x', PlanningLane.MaxContourLength + 1) } } });

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await put.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem!.Errors.ShouldContainKey("Lanes[0].Contour");
    }

    [Fact]
    public async Task Lanes_AreVisibleOnBothDoors()
    {
        var project = await CreateProjectAsync();
        await SeedLaneAsync(project.Id, "frontend");

        var human = await GetRosterAsync(project.Id, agentDoor: false);
        var agent = await GetRosterAsync(project.Id, agentDoor: true);

        human!.Lanes.Single().Name.ShouldBe("frontend");
        agent!.Lanes.Single().Name.ShouldBe("frontend");
    }

    [Fact]
    public async Task PutLanes_MissingLane_LeavesItIntact()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "011-a", "A");
        await CreateFeature(project.Id, "012-b", "B");
        await SetLane(project.Id, "011-a", "frontend");
        await SetLane(project.Id, "012-b", "backend");

        var put = await _client.PutAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/lanes-roster",
            new { Lanes = new[] { new { Name = "backend", Contour = "api/src" } } });

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roster = await GetRosterAsync(project.Id, agentDoor: true);
        roster!.Lanes.Select(l => l.Name).ShouldBe(["backend", "frontend"]);
        roster.Lanes.Single(l => l.Name == "backend").Contour.ShouldBe("api/src");
        roster.Lanes.Single(l => l.Name == "frontend").Features.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task PutLanes_DuplicateNames_Returns409()
    {
        var project = await CreateProjectAsync();

        var put = await _client.PutAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/lanes-roster",
            new { Lanes = new[] { new { Name = "frontend" }, new { Name = "Frontend" } } });

        put.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PutLanes_MissingName_Returns400()
    {
        var project = await CreateProjectAsync();

        var put = await _client.PutAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/lanes-roster",
            new { Lanes = new[] { new { Contour = "web" } } });

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutLanes_UnknownProject_Returns404()
    {
        var ghostProject = Guid.NewGuid();

        var put = await _client.PutAsJsonAsync(
            $"/api/agent/projects/{ghostProject}/planning/lanes-roster",
            new { Lanes = Array.Empty<object>() });

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await put.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Detail!.ShouldContain($"Project '{ghostProject}' was not found.");
    }

    [Fact]
    public async Task UpdateFeature_SetsLane_AndClearsWithEmptyString()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "011-laned", "Laned");

        var set = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/011-laned",
            new { Lane = "frontend" });
        set.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadFeatureAsync(set);
        updated!.Lane.ShouldBe("frontend");

        var cleared = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/011-laned",
            new { Lane = string.Empty });
        cleared.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadFeatureAsync(cleared))!.Lane.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateFeature_UnknownLane_BirthsTheLane()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "011-laned", "Laned");

        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/011-laned",
            new { Lane = "ghost-lane" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadFeatureAsync(response))!.Lane.ShouldBe("ghost-lane");
        var roster = await GetRosterAsync(project.Id, agentDoor: true);
        var lane = roster!.Lanes.ShouldHaveSingleItem();
        lane.Name.ShouldBe("ghost-lane");
        lane.Contour.ShouldBeNull();
        lane.Priority.ShouldBeNull();
        lane.OpenFeatureCount.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateFeature_OmitsLane_KeepsItUnchanged()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "011-laned", "Laned");
        await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/011-laned",
            new { Lane = "frontend" });

        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/011-laned",
            new { Title = "Retitled" });

        (await ReadFeatureAsync(response))!.Lane.ShouldBe("frontend");
    }

    [Fact]
    public async Task AgentUpdateFeature_CarriesLaneThroughTheAgentDoor()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "011-laned", "Laned");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/011-laned/update",
            new { Lane = "frontend", LogNote = "joining the lane" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Claim_IntoBusyLane_Returns409NamingTheLane()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "011-a", "A");
        await CreateFeature(project.Id, "012-b", "B");
        await SetLane(project.Id, "011-a", "frontend");
        await SetLane(project.Id, "012-b", "frontend");
        await Claim(project.Id, "011-a", "Van", "claude", overrideLaneCheck: true);

        var response = await ClaimResponse(project.Id, "012-b", "Bobby", "claude");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("LaneClaimHeld");
        problem.Detail!.ShouldContain("frontend");
        problem.Detail!.ShouldContain("Van by claude");
    }

    [Fact]
    public async Task Claim_WithOverrideLaneCheck_PassesAndEchoes()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "011-a", "A");
        await CreateFeature(project.Id, "012-b", "B");
        await SetLane(project.Id, "011-a", "frontend");
        await SetLane(project.Id, "012-b", "frontend");
        await Claim(project.Id, "011-a", "Van", "claude", overrideLaneCheck: true);

        var claim = await ReadClaimAsync(await ClaimResponse(
            project.Id, "012-b", "Bobby", "claude", overrideLaneCheck: true));

        claim!.OverrideLaneCheck.ShouldBeTrue();
        claim.Lane.ShouldBe("frontend");
        claim.ClaimedByAlias.ShouldBe("Bobby");
    }

    [Fact]
    public async Task QueueRows_CarryLaneAndLaneHolder()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "011-a", "A");
        await SetLane(project.Id, "011-a", "frontend");
        await Claim(project.Id, "011-a", "Van", "claude");

        var queue = await _client.GetFromJsonAsync<QueueJson>(
            $"/api/agent/projects/{project.Id}/planning/task-queue", JsonOptions);

        var row = queue!.Items.ShouldHaveSingleItem();
        row.Lane.ShouldBe("frontend");
        row.LaneHolder.ShouldNotBeNull();
        row.LaneHolder!.Alias.ShouldBe("Van");
        row.LaneHolder.Vendor.ShouldBe("claude");
        row.LaneHolder.ClaimState.ShouldBeOneOf("alive", "stale", "ended");
    }

    [Fact]
    public async Task QueueRows_UnclaimedRowInBusyLane_CarriesTheLaneHolder()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "011-a", "A");
        await CreateFeature(project.Id, "012-b", "B");
        await SetLane(project.Id, "011-a", "frontend");
        await SetLane(project.Id, "012-b", "frontend");
        await Claim(project.Id, "011-a", "Van", "claude", overrideLaneCheck: true);

        var queue = await _client.GetFromJsonAsync<QueueJson>(
            $"/api/agent/projects/{project.Id}/planning/task-queue", JsonOptions);

        var unclaimed = queue!.Items.Single(i => i.Slug == "012-b");
        unclaimed.Lane.ShouldBe("frontend");
        unclaimed.ClaimedByAlias.ShouldBeNull();
        unclaimed.LaneHolder.ShouldNotBeNull();
        unclaimed.LaneHolder!.Alias.ShouldBe("Van");
        unclaimed.LaneHolder.ClaimState.ShouldBeOneOf("alive", "stale", "ended");

        var board = await _client.GetFromJsonAsync<BoardItemJson[]>(
            $"/api/agent/projects/{project.Id}/planning/features", JsonOptions);
        var boardRow = board!.Single(i => i.Slug == "012-b");
        boardRow.LaneHolder!.Alias.ShouldBe("Van");
    }

    [Fact]
    public async Task QueueRows_RowInQuietLane_HasNullHolder()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "011-a", "A");
        await CreateFeature(project.Id, "012-b", "B");
        await SetLane(project.Id, "011-a", "frontend");
        await SetLane(project.Id, "012-b", "backend");
        await Claim(project.Id, "011-a", "Van", "claude", overrideLaneCheck: true);

        var queue = await _client.GetFromJsonAsync<QueueJson>(
            $"/api/agent/projects/{project.Id}/planning/task-queue", JsonOptions);

        var quiet = queue!.Items.Single(i => i.Slug == "012-b");
        quiet.Lane.ShouldBe("backend");
        quiet.LaneHolder.ShouldBeNull();
    }

    [Fact]
    public async Task LaneListing_ProjectionsCarryHolderAndCounts()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "011-a", "A");
        await CreateFeature(project.Id, "012-b", "B");
        await SetLane(project.Id, "011-a", "frontend");
        await SetLane(project.Id, "012-b", "frontend");
        await Claim(project.Id, "011-a", "Van", "claude", overrideLaneCheck: true);

        var roster = await GetRosterAsync(project.Id, agentDoor: true);

        var lane = roster!.Lanes.ShouldHaveSingleItem();
        lane.OpenFeatureCount.ShouldBe(2);
        lane.Holder!.Alias.ShouldBe("Van");
        lane.Holder.ClaimState.ShouldBeOneOf("alive", "stale", "ended");
        lane.Features.Select(f => f.Slug).ShouldBe(["011-a", "012-b"]);
        lane.Features[0].ClaimedByAlias.ShouldBe("Van");
        lane.Features[1].ClaimedByAlias.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostLane_IsRefused_OnBothDoors(bool agentDoor)
    {
        var project = await CreateProjectAsync();
        await SeedLaneAsync(project.Id, "frontend");

        var post = await _client.PostAsJsonAsync(
            $"{Door(agentDoor)}/projects/{project.Id}/planning/lanes",
            new { Name = "backend", Contour = "api/src", SerialOrder = 1, Priority = 2 });

        post.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await post.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("LaneBirthRestricted");
        problem.Detail!.ShouldContain("planning_update_feature");

        var roster = await GetRosterAsync(project.Id, agentDoor: !agentDoor);
        roster!.Lanes.ShouldHaveSingleItem().Name.ShouldBe("frontend");
    }

    [Fact]
    public async Task PutLanes_NameTheRouteCannotAddress_Returns400InvalidLaneName_AndKeepsTheStoredRoster()
    {
        var project = await CreateProjectAsync();
        await SeedLaneAsync(project.Id, "frontend");

        var put = await _client.PutAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/lanes-roster",
            new { Lanes = new[] { new { Name = "frontend" }, new { Name = @"infra\win" } } });

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await put.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("InvalidLaneName");

        var roster = await GetRosterAsync(project.Id, agentDoor: true);
        roster!.Lanes.Select(l => l.Name).ShouldBe(["frontend"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PutLane_EditsMutableFields_KeepsIdentity_OnBothDoors(bool agentDoor)
    {
        var project = await CreateProjectAsync();
        await SeedLaneAsync(project.Id, "frontend");

        var put = await _client.PutAsJsonAsync(
            $"{Door(agentDoor)}/projects/{project.Id}/planning/lanes/frontend",
            new { Contour = "web/src/**", SerialOrder = 5, Priority = 1 });

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roster = await GetRosterAsync(project.Id, agentDoor: !agentDoor);
        var lane = roster!.Lanes.ShouldHaveSingleItem();
        lane.Name.ShouldBe("frontend");
        lane.Contour.ShouldBe("web/src/**");
        lane.SerialOrder.ShouldBe(5);
        lane.Priority.ShouldBe(1);
    }

    [Fact]
    public async Task PutLane_UnknownLane_Returns404NamingTheLane()
    {
        var project = await CreateProjectAsync();
        await SeedLaneAsync(project.Id, "frontend");

        var put = await _client.PutAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/lanes/ghost",
            new { Contour = "web/src" });

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await put.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Detail!.ShouldContain("'ghost'");
        problem.Detail!.ShouldContain("roster");
    }

    [Fact]
    public async Task PutLane_DoesNotCreateTheLane()
    {
        var project = await CreateProjectAsync();

        var put = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planning/lanes/frontend",
            new { Contour = "web/src" });

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await put.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("MissingLane");
        problem.Detail!.ShouldContain("Lane 'frontend' was not found");
        (await GetRosterAsync(project.Id, agentDoor: true))!.Lanes.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteLane_LaneWithOpenFeatures_IsRefused_OnBothDoors(bool agentDoor)
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "011-a", "A");
        await CreateFeature(project.Id, "012-b", "B");
        await SetLane(project.Id, "011-a", "frontend");
        await SetLane(project.Id, "012-b", "backend");

        var delete = await _client.DeleteAsync(
            $"{Door(agentDoor)}/projects/{project.Id}/planning/lanes/Frontend");

        delete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await delete.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("LaneStillOccupied");
        problem.Detail!.ShouldContain("011-a");

        var roster = await GetRosterAsync(project.Id, agentDoor: !agentDoor);
        roster!.Lanes.Select(l => l.Name).ShouldBe(["backend", "frontend"]);
        roster.Lanes.Single(l => l.Name == "frontend").Features.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DeleteLane_EmptyLane_IsRemoved()
    {
        var project = await CreateProjectAsync();
        await SeedLaneAsync(project.Id, "frontend");

        var delete = await _client.DeleteAsync($"/api/agent/projects/{project.Id}/planning/lanes/frontend");

        delete.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echoed = await delete.Content.ReadFromJsonAsync<LanesJson>(JsonOptions);
        echoed!.Lanes.ShouldBeEmpty();
        (await GetRosterAsync(project.Id, agentDoor: true))!.Lanes.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteLane_UnknownLane_IsAnIdempotentNoOp()
    {
        var project = await CreateProjectAsync();
        await SeedLaneAsync(project.Id, "frontend");

        var delete = await _client.DeleteAsync($"/api/projects/{project.Id}/planning/lanes/ghost");

        delete.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echoed = await delete.Content.ReadFromJsonAsync<LanesJson>(JsonOptions);
        echoed!.Lanes.Single().Name.ShouldBe("frontend");
    }

    [Fact]
    public async Task DeleteLane_UnknownProject_Returns404()
    {
        var ghostProject = Guid.NewGuid();

        var delete = await _client.DeleteAsync($"/api/agent/projects/{ghostProject}/planning/lanes/frontend");

        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await delete.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Detail!.ShouldContain($"Project '{ghostProject}' was not found.");
    }

    [Fact]
    public async Task LaneLifecycle_GranularCreatesAndEditsAccumulate()
    {
        var project = await CreateProjectAsync();
        await SeedLaneAsync(project.Id, "frontend");
        await SeedLaneAsync(project.Id, "backend");

        var edit = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planning/lanes/frontend",
            new { Contour = "web/src" });
        edit.StatusCode.ShouldBe(HttpStatusCode.OK);

        var roster = await GetRosterAsync(project.Id, agentDoor: true);
        roster!.Lanes.Select(l => l.Name).ShouldBe(["backend", "frontend"]);
        roster.Lanes.Single(l => l.Name == "frontend").Contour.ShouldBe("web/src");
    }

    private static string Door(bool agentDoor) => agentDoor ? "/api/agent" : "/api";

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"lanes-proj-{Guid.NewGuid():N}",
            DiskPath = _projectDir,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task SeedLaneAsync(Guid projectId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        db.PlanningLanes.Add(PlanningLane.Create(projectId, name, contour: null, serialOrder: null));
        await db.SaveChangesAsync();
    }

    private async Task<LanesJson?> GetRosterAsync(Guid projectId, bool agentDoor)
    {
        var door = agentDoor ? "/api/agent" : "/api";
        var response = await _client.GetAsync($"{door}/projects/{projectId}/planning/lanes");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<LanesJson>(JsonOptions);
    }

    private async Task CreateFeature(Guid projectId, string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = "" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task SetLane(Guid projectId, string slug, string lane)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}",
            new { Lane = lane });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task Claim(Guid projectId, string slug, string alias, string vendor, bool overrideLaneCheck = false)
    {
        var response = await ClaimResponse(projectId, slug, alias, vendor, overrideLaneCheck);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> ClaimResponse(
        Guid projectId, string slug, string alias, string vendor, bool overrideLaneCheck = false)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/agent/projects/{projectId}/planning/features/{slug}/claim")
        {
            Content = JsonContent.Create(
                new { Alias = alias, Vendor = vendor, OverrideLaneCheck = overrideLaneCheck })
        };
        request.Headers.Add("X-Overmind-Session", Guid.NewGuid().ToString());
        return await _client.SendAsync(request);
    }

    private static async Task<FeatureJson?> ReadFeatureAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<FeatureJson>(JsonOptions);

    private static async Task<ClaimJson?> ReadClaimAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<ClaimJson>(JsonOptions);

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record LanesJson(LaneJson[] Lanes);

    public sealed record LaneJson(
        string Name,
        string? Contour,
        int? SerialOrder,
        int? Priority,
        int OpenFeatureCount,
        LaneHolderJson? Holder,
        LaneFeatureJson[] Features);

    public sealed record LaneHolderJson(string Alias, string Vendor, string ClaimState);

    public sealed record LaneFeatureJson(
        string Slug, string Status, string? ClaimedByAlias, string? ClaimedByVendor, string? ClaimState);

    public sealed record FeatureJson(string Slug, string Title, string? Lane);

    public sealed record ClaimJson(
        string Slug, string? ClaimedByAlias, string? ClaimedByVendor, DateTime? ClaimedAt,
        string? ClaimState, bool ClaimedByHuman, string? Lane, bool OverrideLaneCheck);

    public sealed record QueueJson(QueueItemJson[] Items);

    public sealed record BoardItemJson(
        string Slug, string Title, string Status, string Priority, int Version,
        string? Lane, LaneHolderJson? LaneHolder);

    public sealed record QueueItemJson(
        string Slug, string? Number, string Title, string Priority, string Status, int QueuePosition,
        string? ClaimedByAlias, string? ClaimedByVendor,
        string? Lane, LaneHolderJson? LaneHolder);

    public sealed record ProblemJson(string Title, string? Detail);

    public sealed record ValidationProblemJson([property: JsonPropertyName("errors")] Dictionary<string, string[]> Errors);
}
