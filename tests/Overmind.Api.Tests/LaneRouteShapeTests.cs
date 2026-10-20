using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// Route shape for the lane roster (143/T4): the whole-roster writer answers on its own sibling literal and
// nowhere else, so a URL whose shape says "one lane" can never be dispatched as "replace the roster". The
// retired collection PUT still answers, but as a tombstone that binds no body and calls no service.
public sealed class LaneRouteShapeTests : IDisposable
{
    private const string RosterShapedBody = """{"lanes":[{"name":"swapped"}]}""";
    private const string RetiredCode = "LaneRosterWriteRetired";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;
    private readonly string _projectDir;

    // The dot never reaches the {name} position from a client, and this is measured, not assumed: over a raw
    // socket against Kestrel the wire target `PUT /probe/lanes/.` arrives with RawTarget still holding the dot
    // while Path is already `/probe/lanes/` (spike ~/dev/tmp/overmind-143-spike/LaneRouteShapeProbeTests.cs:403-408,
    // the same for `%2E`), and routing matches on Path. `curl --path-as-is` does not change that — the dot is
    // dropped by the server's own path normalisation before routing ever sees it. So `{name}='.'` is reachable
    // by nobody outside this process: the injection below is the only door, it is synthetic by construction,
    // and what it buys is the chance to say what the granular handler answers when handed the dot anyway.
    private string? _injectedPath;

    public LaneRouteShapeTests()
    {
        _client = new HttpClient(_factory.Server.CreateHandler(context =>
        {
            if (_injectedPath is { } path)
            {
                context.Request.Path = path;
            }
        }))
        {
            BaseAddress = new Uri("http://localhost")
        };

        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-lane-route-{Guid.NewGuid():N}");
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

    [Theory]
    [InlineData("/api", ".")]
    [InlineData("/api", "%2E")]
    [InlineData("/api/agent", ".")]
    [InlineData("/api/agent", "%2E")]
    public async Task RosterShapedPut_DotSegmentInTheNamePosition_CannotReachTheRosterWriter(
        string door, string nameSegment)
    {
        var project = await CreateProjectAsync();
        await CreateLaneAsync(project.Id, "frontend");

        var put = await PutAsync(
            $"{door}/projects/{project.Id}/planning/lanes/{nameSegment}", RosterShapedBody);

        // This is the spike's "200 and the roster was replaced" case, inverted. The dot is gone before the
        // wire, so what the server answers is PUT on the collection — and the collection no longer writes:
        // it refuses typed and leaves the roster alone. This asserts nothing about the dot as it left the
        // client (under TestServer RawTarget is an empty string); the claim is about the collapsed URL.
        await AssertRetiredAsync(put, door, project.Id);
        (await RosterNamesAsync(project.Id)).ShouldBe(["frontend"]);
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/agent")]
    public async Task PutOnTheCollection_AnswersTheTypedRetirementRefusal_AndWritesNothing(string door)
    {
        var project = await CreateProjectAsync();
        await CreateLaneAsync(project.Id, "frontend");

        var put = await PutAsync($"{door}/projects/{project.Id}/planning/lanes", RosterShapedBody);

        await AssertRetiredAsync(put, door, project.Id);
        (await RosterNamesAsync(project.Id)).ShouldBe(["frontend"]);
    }

    [Theory]
    [InlineData("/api", "")]
    [InlineData("/api", "not json at all")]
    [InlineData("/api", """{"lanes":"swapped"}""")]
    [InlineData("/api/agent", "{}")]
    public async Task Tombstone_BindsNoBody_SoNoShapeOfPayloadChangesTheRefusal(string door, string body)
    {
        var project = await CreateProjectAsync();
        await CreateLaneAsync(project.Id, "frontend");

        var put = await PutAsync($"{door}/projects/{project.Id}/planning/lanes", body);

        // A ValidationProblem here would mean the retired door still binds SetLanesRequest, which is the hole
        // wearing a different hat. The refusal is a routing answer, so a malformed body cannot route anywhere.
        await AssertRetiredAsync(put, door, project.Id);
        (await RosterNamesAsync(project.Id)).ShouldBe(["frontend"]);
    }

    [Fact]
    public async Task Tombstone_OnAProjectThatDoesNotExist_StillRefusesRetirement_BecauseNothingIsLookedUp()
    {
        var ghostProject = Guid.NewGuid();

        var put = await PutAsync(
            $"/api/projects/{ghostProject}/planning/lanes", RosterShapedBody);

        put.StatusCode.ShouldBe(HttpStatusCode.Gone);
        var problem = await ReadProblemAsync(put);
        problem!.Title.ShouldBe(RetiredCode);
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/agent")]
    public async Task DoubleDotPut_CollapsesAboveTheCollection_LandsOnNoWriter_AndWritesNothing(string door)
    {
        var project = await CreateProjectAsync();
        await CreateLaneAsync(project.Id, "frontend");

        var put = await PutAsync($"{door}/projects/{project.Id}/planning/lanes/..", RosterShapedBody);

        // The sibling literal is a sibling, not a parent: `..` walks out of the lanes prefix altogether and
        // matches no API route, so there is no writer to reach. The answer is 405 rather than 404 because the
        // SPA fallback claims the leftover shape for GET/HEAD — the spike's bare WebApplication had no
        // fallback and answered 404 there; this is the Host, and the routing refusal is the same nothing-writes.
        put.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        (await RosterNamesAsync(project.Id)).ShouldBe(["frontend"]);
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/agent")]
    public async Task VerbsTheCollectionNeverAnswered_StillRefuse405_AllowNowCarriesTheRetiredPut(string door)
    {
        var project = await CreateProjectAsync();
        await CreateLaneAsync(project.Id, "frontend");

        var delete = await _client.DeleteAsync($"{door}/projects/{project.Id}/planning/lanes");

        delete.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        var allow = delete.Content.Headers.GetValues("Allow").ToArray();
        allow.ShouldContain("GET");
        allow.ShouldContain("POST");
        allow.ShouldContain("PUT");
        (await RosterNamesAsync(project.Id)).ShouldBe(["frontend"]);
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/agent")]
    public async Task RosterWriter_AnswersOnItsOwnLiteral_AndReplacesTheRoster(string door)
    {
        var project = await CreateProjectAsync();
        await CreateLaneAsync(project.Id, "frontend");

        var put = await PutAsync(
            $"{door}/projects/{project.Id}/planning/lanes-roster",
            """{"lanes":[{"name":"swapped","contour":"src/**"},{"name":"frontend"}]}""");

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RosterNamesAsync(project.Id)).ShouldBe(["frontend", "swapped"]);
    }

    [Fact]
    public async Task TombstonePointer_RoundTripsToTheLiveWriter_OnTheDoorThatRefused()
    {
        var project = await CreateProjectAsync();
        await CreateLaneAsync(project.Id, "frontend");

        // One read of this body, by necessity: the handler's response stream is closed after the first consume,
        // so a second ReadProblemAsync throws ObjectDisposedException. The refusal shape is pinned by
        // AssertRetiredAsync elsewhere; here only the pointer matters, and it matters as a live route.
        var refusal = await PutAsync(
            $"/api/agent/projects/{project.Id}/planning/lanes", RosterShapedBody);

        refusal.StatusCode.ShouldBe(HttpStatusCode.Gone);
        var problem = await ReadProblemAsync(refusal);
        problem!.Title.ShouldBe(RetiredCode);

        // The pointer is followed rather than read: every literal in this file could stay in lockstep with a
        // renamed route and still hide a detail that sends the caller to a 404. Only the path the refusal itself
        // printed gets PUT back at the same door, so route and pointer diverging is red by construction.
        var rosterPath = PointerFrom(problem);

        var write = await PutAsync(
            rosterPath, """{"lanes":[{"name":"swapped","contour":"src/**"},{"name":"frontend"}]}""");

        write.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RosterNamesAsync(project.Id)).ShouldBe(["frontend", "swapped"]);
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/agent")]
    public async Task DotSegmentThatSurvivesToRouting_IsRefusedAsATypedPlanningFailure(string door)
    {
        var project = await CreateProjectAsync();
        await CreateLaneAsync(project.Id, "frontend");
        var granularTarget = $"{door}/projects/{project.Id}/planning/lanes/.";

        _injectedPath = granularTarget;
        HttpResponseMessage refused;
        try
        {
            refused = await PutAsync(granularTarget, RosterShapedBody);
        }
        finally
        {
            _injectedPath = null;
        }

        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadProblemAsync(refused);
        problem!.Title.ShouldBe("InvalidLaneName");
        problem.Detail!.ShouldContain("'.'");
        (await RosterNamesAsync(project.Id)).ShouldBe(["frontend"]);
    }

    private async Task AssertRetiredAsync(HttpResponseMessage refusal, string door, Guid projectId)
    {
        refusal.StatusCode.ShouldBe(HttpStatusCode.Gone);

        var problem = await ReadProblemAsync(refusal);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe(RetiredCode);
        var detail = problem.Detail;
        detail.ShouldNotBeNull();
        detail!.ShouldContain($"PUT {door}/projects/{projectId}/planning/lanes-roster");
        detail.ShouldContain("is retired and wrote nothing");
        detail.ShouldContain("RFC 3986");
        refusal.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    private static string PointerFrom(ProblemJson? problem)
    {
        const string marker = "answers at 'PUT ";
        var detail = problem!.Detail!;
        var markerAt = detail.IndexOf(marker, StringComparison.Ordinal);
        markerAt.ShouldBeGreaterThan(-1, $"the refusal stopped printing its own pointer: {detail}");
        var from = markerAt + marker.Length;
        return detail[from..detail.IndexOf('\'', from)];
    }

    private static string LanesPath(Guid projectId) => $"/api/projects/{projectId}/planning/lanes";

    private async Task<HttpResponseMessage> PutAsync(string target, string body) =>
        await _client.PutAsync(target, new StringContent(body, Encoding.UTF8, "application/json"));

    private static async Task<ProblemJson?> ReadProblemAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"lane-route-proj-{Guid.NewGuid():N}",
            DiskPath = _projectDir,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task CreateLaneAsync(Guid projectId, string name)
    {
        var created = await _client.PostAsJsonAsync(LanesPath(projectId), new { Name = name });
        created.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<string[]> RosterNamesAsync(Guid projectId)
    {
        var roster = await _client.GetFromJsonAsync<LanesJson>(LanesPath(projectId), JsonOptions);
        return [.. roster!.Lanes.Select(lane => lane.Name)];
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record LanesJson(LaneJson[] Lanes);

    public sealed record LaneJson(string Name);

    public sealed record ProblemJson(string Title, string? Detail);
}
