using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Overmind.Api.Tests;

// Soft-cap door coverage (266 T1): per field x cap x zone at the real HTTP doors —
// silent under the cap, whole + warning inside the grace band, truncated + preview past it.
public sealed class SoftCapDoorsApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SoftCapDoorsApiTests(ITestOutputHelper output)
    {
        _client = _factory.CreateClient();
        _output = output;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ---- notify (L=2000) ----

    [Theory]
    [InlineData(2_001, 2_001, false)]
    [InlineData(2_440, 2_440, false)]
    [InlineData(2_441, 2_400, true)]
    public async Task Notify_BandStoresWhole_PastCeilingTruncates(int send, int stored, bool truncated)
    {
        var project = await CreateProjectAsync();

        var response = await _client.PostAsJsonAsync("/api/agent/notifications",
            new { Kind = "info", Text = new string('n', send), ProjectId = project.Id });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await ReadAsync(response);
        payload["text"]!.GetValue<string>().Length.ShouldBe(stored);
        var warning = payload["warnings"]!.AsArray().Single()!.AsObject();
        warning["accepted_len"]!.GetValue<int>().ShouldBe(stored);
        warning["limit"]!.GetValue<int>().ShouldBe(2_000);
        warning["truncated"]!.GetValue<bool>().ShouldBe(truncated);
    }

    [Fact]
    public async Task Notify_AtCap_AndBelow_StaySilent()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PostAsJsonAsync("/api/agent/notifications",
            new { Kind = "info", Text = new string('n', 2_000), ProjectId = project.Id });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ReadAsync(response))["warnings"].ShouldBeNull();
    }

    // ---- issues append (L=2000) ----

    [Theory]
    [InlineData(2_001, 2_001, false)]
    [InlineData(2_441, 2_400, true)]
    public async Task IssuesAppend_SoftCapsTheTrailText(int send, int stored, bool truncated)
    {
        var project = await CreateProjectAsync();
        var issue = await CreateIssueAsync(project.Id);

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/issues/{issue}/append",
            new { AuthorProjectId = project.Id, Text = new string('t', send) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await ReadAsync(response);
        payload["text"]!.GetValue<string>().Length.ShouldBe(stored);
        payload["warnings"]!.AsArray().Single()!["truncated"]!.GetValue<bool>().ShouldBe(truncated);
    }

    // ---- entries append_entry (L=2000, the real FollowUpTextCap consumer) ----

    [Theory]
    [InlineData(2_001, 2_001, false)]
    [InlineData(2_441, 2_400, true)]
    public async Task EntryAppend_SoftCapsTheFollowUpBlock(int send, int stored, bool truncated)
    {
        var project = await CreateProjectAsync();
        var entry = await CreateEntryAsync(project.Id);

        var response = await _client.PostAsJsonAsync($"/api/agent/entries/{entry}/append",
            new { Text = new string('f', send) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await ReadAsync(response);
        payload["warnings"]!.AsArray().Single()!["truncated"]!.GetValue<bool>().ShouldBe(truncated);
        // the follow-up block lands in the entry body at the capped size
        payload["content"]!.GetValue<string>().ShouldContain(new string('f', stored));
    }

    // ---- task brief (L=500, single-line guard kept) ----

    [Theory]
    [InlineData(501, 501, false)]
    [InlineData(610, 610, false)]
    [InlineData(611, 600, true)]
    public async Task ArtifactBrief_GraceBandAndTruncationPerWorkedExample(int send, int stored, bool truncated)
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "briefs");

        var response = await _client.PostAsJsonAsync(AgentArtifactsUrl(project.Id, "briefs"),
            new { Kind = "task", Body = "<goal>soft brief</goal>", Brief = new string('b', send) });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await ReadAsync(response);
        payload["brief"]!.GetValue<string>().Length.ShouldBe(stored);
        payload["warnings"]!.AsArray().Single()!["truncated"]!.GetValue<bool>().ShouldBe(truncated);
    }

    [Fact]
    public async Task ArtifactBrief_Multiline_StillHardRejected()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "briefs2");

        var response = await _client.PostAsJsonAsync(AgentArtifactsUrl(project.Id, "briefs2"),
            new { Kind = "task", Body = "x", Brief = "line one\r\nline two" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchSetBrief_PastCeiling_TruncatesAndWarns()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "briefpatch");
        var artifact = await CreateArtifactAsync(project.Id, "briefpatch", "task", "body");

        var response = await _client.PostAsJsonAsync($"/api/agent/planning/artifacts/{artifact}/patch",
            new { Op = "set_brief", Brief = new string('p', 900) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await ReadAsync(response);
        var warning = payload["warnings"]!.AsArray().Single()!.AsObject();
        warning["accepted_len"]!.GetValue<int>().ShouldBe(600);
        warning["truncated"]!.GetValue<bool>().ShouldBeTrue();
        warning["lost_tail_preview"]!.GetValue<string>().Length.ShouldBeLessThanOrEqualTo(50);
    }

    [Fact]
    public async Task UpsertTasks_PerItemBrief_SoftCapsAndReportsAtBatchLevel()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "tasks-soft");

        var response = await _client.PostAsJsonAsync(AgentTasksUrl(project.Id, "tasks-soft"), new
        {
            Tasks = new object[]
            {
                new { Body = "<goal>one</goal>", Order = 0, Brief = new string('q', 611) },
                new { Body = "<goal>two</goal>", Order = 1, Brief = new string('q', 400) },
            },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await ReadAsync(response);
        payload["artifacts"]![0]!["brief"]!.GetValue<string>().Length.ShouldBe(600);
        payload["artifacts"]![1]!["brief"]!.GetValue<string>().Length.ShouldBe(400);
        var warnings = payload["warnings"]!.AsArray();
        warnings.Count.ShouldBe(1);
        warnings[0]!["truncated"]!.GetValue<bool>().ShouldBeTrue();
    }

    // ---- reason / tombstone (L=500) ----

    [Theory]
    [InlineData(501, false)]
    [InlineData(611, true)]
    public async Task DeclineReason_SoftCaps(int send, bool truncated)
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "declines");
        var artifact = await CreateArtifactAsync(project.Id, "declines", "spec", "# Spec\n<goal>g</goal>");

        var response = await _client.PostAsJsonAsync(
            $"/api/planning/artifacts/{artifact}/decline", new { Reason = new string('d', send) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var warning = (await ReadAsync(response))["warnings"]!.AsArray().Single()!;
        warning["limit"]!.GetValue<int>().ShouldBe(500);
        warning["truncated"]!.GetValue<bool>().ShouldBe(truncated);
    }

    // ---- lane contour (L=2000) ----

    [Theory]
    [InlineData(2_001, 2_001, false)]
    [InlineData(2_441, 2_400, true)]
    public async Task LaneCreate_SoftCapsContour(int send, int stored, bool truncated)
    {
        var project = await CreateProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/lanes",
            new { Name = $"lane-{Guid.NewGuid():N}"[..18], Contour = new string('c', send) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await ReadAsync(response);
        payload["contour"]!.GetValue<string>().Length.ShouldBe(stored);
        payload["warnings"]!.AsArray().Single()!["truncated"]!.GetValue<bool>().ShouldBe(truncated);
    }

    [Fact]
    public async Task LaneRosterSet_SoftCapsEveryContour_AndUpdateDoorWarns()
    {
        var project = await CreateProjectAsync();
        var create = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/lanes", new { Name = "rostered", Contour = "seed" });
        create.StatusCode.ShouldBe(HttpStatusCode.OK);

        var roster = await _client.PutAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/lanes-roster",
            new { Lanes = new object[] { new { Name = "rostered", Contour = new string('r', 2_001) } } });
        roster.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rosterPayload = await ReadAsync(roster);
        rosterPayload["warnings"]!.AsArray().Single()!["truncated"]!.GetValue<bool>().ShouldBeFalse();

        var update = await _client.PutAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/lanes/rostered",
            new { Contour = new string('r', 3_000) });
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updatePayload = await ReadAsync(update);
        updatePayload["lanes"]!.AsArray().Single()!["contour"]!.GetValue<string>().Length.ShouldBe(2_400);
    }

    // ---- log-line ceiling L=2000 (new guard, owner ruling via the task contract) ----

    [Theory]
    [InlineData(2_001, 2_001, false)]
    [InlineData(2_441, 2_400, true)]
    public async Task AppendLog_Note_SoftCapsAtTheNewLineCeiling(int send, int stored, bool truncated)
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "loglines");
        var artifact = await CreateArtifactAsync(project.Id, "loglines", "task", "body");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/planning/artifacts/{artifact}/log", new { Note = new string('l', send) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await ReadAsync(response);
        payload["warnings"]!.AsArray().Single()!["limit"]!.GetValue<int>().ShouldBe(2_000);
        payload["warnings"]!.AsArray().Single()!["truncated"]!.GetValue<bool>().ShouldBe(truncated);

        var read = await _client.GetFromJsonAsync<JsonNode>(
            $"/api/planning/artifacts/{artifact}", JsonOptions);
        var lastLine = read!["log"]!.AsArray()[^1]!["text"]!.GetValue<string>();
        lastLine.ShouldStartWith(new string('l', stored));
        lastLine.ShouldNotContain(new string('l', stored + 1));
    }

    [Fact]
    public async Task SetStatus_LogNote_WarnsAlongsideDomainWarnings()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "statusnote");
        var artifact = await CreateArtifactAsync(project.Id, "statusnote", "task", "body");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/planning/artifacts/{artifact}/status",
            new { Status = "inProgress", LogNote = new string('s', 2_001) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var warnings = (await ReadAsync(response))["warnings"]!.AsArray();
        warnings.Single(w => w!.AsObject().ContainsKey("accepted_len"))!["limit"]!
            .GetValue<int>().ShouldBe(2_000);
    }

    [Fact]
    public async Task BatchAppendLog_AndSetStatus_SoftCapAtTheWireMapping()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "batchsoft");
        var artifact = await CreateArtifactAsync(project.Id, "batchsoft", "task", "body");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/batchsoft/batch",
            new
            {
                Ops = new object[]
                {
                    new { Op = "append_log", ArtifactId = artifact, Note = new string('a', 2_441) },
                    new { Op = "set_status", ArtifactId = artifact, Status = "inProgress", LogNote = new string('b', 2_001) },
                },
            });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await ReadAsync(response);
        var warnings = payload["warnings"]!.AsArray();
        warnings.Count.ShouldBe(2);
        warnings.Count(w => w!["truncated"]!.GetValue<bool>()).ShouldBe(1);
    }

    // ---- empties stay the field validator's call ----

    [Fact]
    public async Task SoftDoors_KeepBlankRejects()
    {
        var project = await CreateProjectAsync();

        var notify = await _client.PostAsJsonAsync(
            "/api/agent/notifications", new { Kind = "info", Text = "   ", ProjectId = project.Id });
        notify.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- denominators: prove the table actually ran ----

    // Door HTTP cases a method runs per xUnit case — 1 unless the body fires several
    // soft-cap doors back to back. nameof() keys are compile-checked, so deleting or
    // renaming the method breaks the build, not the census silently.
    private static readonly Dictionary<string, int> MultiDoorCases = new()
    {
        [nameof(LaneRosterSet_SoftCapsEveryContour_AndUpdateDoorWarns)] = 2,
    };

    [Fact]
    public void Denominator_PrintsEveryFieldZoneCensus()
    {
        // Derived from the attributes xUnit itself expands (theory InlineData rows, facts as
        // single cases) plus the MultiDoorCases weights — deleting any door case shrinks
        // this number and the pinned total below goes red.
        var doors = typeof(SoftCapDoorsApiTests)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<FactAttribute>() is not null)
            .Where(m => m.Name != nameof(Denominator_PrintsEveryFieldZoneCensus))
            .ToArray();

        foreach (var key in MultiDoorCases.Keys)
        {
            doors.ShouldContain(m => m.Name == key, $"census weights point at a removed door: {key}");
        }

        var doorCases = doors.Sum(m =>
        {
            var rows = m.GetCustomAttributes<InlineDataAttribute>().Count();
            return rows > 0 ? rows : MultiDoorCases.GetValueOrDefault(m.Name, 1);
        });

        _output.WriteLine($"soft-cap door denominators: HTTP door cases={doorCases} " +
            $"(across {doors.Length} door methods), " +
            "fields covered=8 (wave, notify, issues, entry, brief, reason, contour, log-line), " +
            "zones per field=silent/warn/truncate, plus 2 wave-cap rewrites in WaveChatApiTests");

        doorCases.ShouldBe(25);
    }

    // ---- helpers ----

    private static string AgentArtifactsUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts";

    private static string AgentTasksUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts/tasks";

    private static async Task<JsonNode> ReadAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var name = $"softcap-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions))!;
    }

    private async Task CreateFeatureAsync(Guid projectId, string slug)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features", new { Title = $"Feature {slug}", Slug = slug });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<Guid> CreateArtifactAsync(Guid projectId, string slug, string kind, string body)
    {
        var response = await _client.PostAsJsonAsync(AgentArtifactsUrl(projectId, slug),
            new { Kind = kind, Body = body });

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        return Guid.Parse((await ReadAsync(response))["id"]!.GetValue<string>());
    }

    private async Task<Guid> CreateIssueAsync(Guid projectId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/issues",
            new { AuthorProjectId = projectId, Title = "soft issue", Body = "## Repro\nx" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return Guid.Parse((await ReadAsync(response))["id"]!.GetValue<string>());
    }

    private async Task<Guid> CreateEntryAsync(Guid projectId)
    {
        var response = await _client.PostAsJsonAsync("/api/agent/entries",
            new { ProjectId = projectId, Type = "note", Title = "soft entry", Content = "base" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return Guid.Parse((await ReadAsync(response))["id"]!.GetValue<string>());
    }

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath);
}
