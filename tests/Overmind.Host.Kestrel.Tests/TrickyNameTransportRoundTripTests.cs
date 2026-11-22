using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Host.Api;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Host.Kestrel.Tests;

[Trait("Host", "Kestrel")]
[Collection("overmind kestrel host")]
public sealed class TrickyNameTransportRoundTripTests(KestrelApiFactory factory)
{
    private const string LegacySlashName = "C. Edge/Pi";
    private const string BareSlashName = "rooms/echo";
    private const string LiteralEscapeName = "a%2Fb";
    private const string UnicodeName = "Полоса-Ревью";
    private const string TrailingPercentName = "release 50%";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Guid _sessionId = Guid.NewGuid();

    [Fact]
    public async Task SlashLegacyName_RoundTripsFromSeedThroughBothDoorFamiliesToDeletion()
    {
        var legacyWire = Uri.EscapeDataString(LegacySlashName);
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);

        var birth = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/lanes",
            new { Name = LegacySlashName }, JsonOptions);
        birth.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var birthProblem = await birth.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        birthProblem.GetProperty("title").GetString().ShouldBe("InvalidLaneName");
        birthProblem.GetProperty("detail").GetString()!.ShouldContain(LegacySlashName);

        await SeedLaneAsync(projectId, LegacySlashName);
        await SeedAgentSessionAsync();

        var posted = await SendAgentAsync(
            client, HttpMethod.Post, $"/api/agent/projects/{projectId}/wave/rooms/{legacyWire}/messages",
            new { Text = "legacy slash room delivery" });
        posted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var message = await posted.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        message.GetProperty("room").GetString().ShouldBe(LegacySlashName);
        message.GetProperty("text").GetString().ShouldBe("legacy slash room delivery");

        var history = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{legacyWire}/messages", null);
        history.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = (await history.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("items");
        items.GetArrayLength().ShouldBe(1);
        items[0].GetProperty("room").GetString().ShouldBe(LegacySlashName);
        items[0].GetProperty("text").GetString().ShouldBe("legacy slash room delivery");

        var updated = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/lanes/{legacyWire}",
            new { Contour = "src/edge/**" }, JsonOptions);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        LaneRowOf(client, projectId, LegacySlashName).GetProperty("contour").GetString().ShouldBe("src/edge/**");

        var deleted = await client.DeleteAsync($"/api/projects/{projectId}/planning/lanes/{legacyWire}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        RosterNames(client, projectId).ShouldNotContain(LegacySlashName);
        RoomNames(client, projectId).ShouldBe(["all"]);
    }

    [Fact]
    public async Task BareSlashSegmentWire_AddressesTheSlashTwinOnBothDoorFamilies()
    {
        const string bareWire = "rooms%2Fecho";
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);
        await SeedLaneAsync(projectId, BareSlashName);
        await SeedAgentSessionAsync();

        var posted = await SendAgentAsync(
            client, HttpMethod.Post, $"/api/agent/projects/{projectId}/wave/rooms/{bareWire}/messages",
            new { Text = "bare escape reaches the twin" });
        posted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var message = await posted.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        message.GetProperty("room").GetString().ShouldBe(BareSlashName);
        message.GetProperty("text").GetString().ShouldBe("bare escape reaches the twin");

        var history = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{bareWire}/messages", null);
        history.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = (await history.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("items");
        items.GetArrayLength().ShouldBe(1);
        items[0].GetProperty("room").GetString().ShouldBe(BareSlashName);
        items[0].GetProperty("text").GetString().ShouldBe("bare escape reaches the twin");

        var updated = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/lanes/{bareWire}",
            new { Contour = "src/rooms/**" }, JsonOptions);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        LaneRowOf(client, projectId, BareSlashName).GetProperty("contour").GetString().ShouldBe("src/rooms/**");

        var deleted = await client.DeleteAsync($"/api/projects/{projectId}/planning/lanes/{bareWire}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        RosterNames(client, projectId).ShouldNotContain(BareSlashName);
        RoomNames(client, projectId).ShouldBe(["all"]);
    }

    [Fact]
    public async Task LiteralEscapeName_BirthsThroughTheBodyDoor_ButEveryWireDoorShadowsOntoItsSlashTwin()
    {
        var shadowWire = Uri.EscapeDataString(LiteralEscapeName);
        const string twinName = "a/b";
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);
        await SeedAgentSessionAsync();

        var birth = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/lanes",
            new { Name = LiteralEscapeName, Contour = "src/literal/**" }, JsonOptions);
        birth.StatusCode.ShouldBe(HttpStatusCode.OK);
        LaneRowOf(client, projectId, LiteralEscapeName)
            .GetProperty("contour").GetString().ShouldBe("src/literal/**");

        var roomOrphan = await SendAgentAsync(
            client, HttpMethod.Post, $"/api/agent/projects/{projectId}/wave/rooms/{shadowWire}/messages",
            new { Text = "shadow probe" });
        roomOrphan.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        roomOrphan.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var roomProblem = await roomOrphan.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        roomProblem.GetProperty("title").GetString().ShouldBe("MissingWaveRoom");
        var roomDetail = roomProblem.GetProperty("detail").GetString();
        roomDetail!.ShouldContain("Wave room 'a/b' does not exist");
        roomDetail.ShouldContain($"'{LiteralEscapeName}'");

        var laneOrphan = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/lanes/{shadowWire}",
            new { Contour = "src/shadow/**" }, JsonOptions);
        laneOrphan.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var laneProblem = await laneOrphan.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        laneProblem.GetProperty("title").GetString().ShouldBe("MissingLane");
        laneProblem.GetProperty("detail").GetString()!.ShouldContain("Lane 'a/b' was not found");

        await SeedLaneAsync(projectId, twinName);

        var twinPosted = await SendAgentAsync(
            client, HttpMethod.Post, $"/api/agent/projects/{projectId}/wave/rooms/{shadowWire}/messages",
            new { Text = "the twin answers" });
        twinPosted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var twinMessage = await twinPosted.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        twinMessage.GetProperty("room").GetString().ShouldBe(twinName);
        twinMessage.GetProperty("text").GetString().ShouldBe("the twin answers");

        var twinHistory = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{shadowWire}/messages", null);
        twinHistory.StatusCode.ShouldBe(HttpStatusCode.OK);
        var twinItems = (await twinHistory.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("items");
        twinItems.GetArrayLength().ShouldBe(1);
        twinItems[0].GetProperty("room").GetString().ShouldBe(twinName);

        var updated = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/lanes/{shadowWire}",
            new { Contour = "src/twin/**" }, JsonOptions);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        LaneRowOf(client, projectId, twinName).GetProperty("contour").GetString().ShouldBe("src/twin/**");
        LaneRowOf(client, projectId, LiteralEscapeName)
            .GetProperty("contour").GetString().ShouldBe("src/literal/**");

        var deleted = await client.DeleteAsync($"/api/projects/{projectId}/planning/lanes/{shadowWire}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        RosterNames(client, projectId).ShouldBe([LiteralEscapeName]);
        RoomNames(client, projectId).ShouldBe(["all", LiteralEscapeName]);

        var postDeleteOrphan = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{shadowWire}/peek", null);
        postDeleteOrphan.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var postDeleteProblem = await postDeleteOrphan.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        postDeleteProblem.GetProperty("title").GetString().ShouldBe("MissingWaveRoom");
        postDeleteProblem.GetProperty("detail").GetString()!.ShouldContain("'a/b'");
    }

    [Fact]
    public async Task UnicodeName_BirthsThroughTheDoor_AndRoundTripsBothDoorFamilies()
    {
        var unicodeWire = Uri.EscapeDataString(UnicodeName);
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);
        await SeedAgentSessionAsync();

        var birth = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/lanes",
            new { Name = UnicodeName, Contour = "src/review/**" }, JsonOptions);
        birth.StatusCode.ShouldBe(HttpStatusCode.OK);
        LaneRowOf(client, projectId, UnicodeName)
            .GetProperty("contour").GetString().ShouldBe("src/review/**");

        var posted = await SendAgentAsync(
            client, HttpMethod.Post, $"/api/agent/projects/{projectId}/wave/rooms/{unicodeWire}/messages",
            new { Text = "cyrillic text rides the same wire" });
        posted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var message = await posted.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        message.GetProperty("room").GetString().ShouldBe(UnicodeName);
        message.GetProperty("text").GetString().ShouldBe("cyrillic text rides the same wire");

        var history = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{unicodeWire}/messages", null);
        history.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = (await history.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("items");
        items.GetArrayLength().ShouldBe(1);
        items[0].GetProperty("room").GetString().ShouldBe(UnicodeName);
        items[0].GetProperty("text").GetString().ShouldBe("cyrillic text rides the same wire");

        var updated = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/lanes/{unicodeWire}",
            new { Contour = "src/review-next/**" }, JsonOptions);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        LaneRowOf(client, projectId, UnicodeName)
            .GetProperty("contour").GetString().ShouldBe("src/review-next/**");

        var deleted = await client.DeleteAsync($"/api/projects/{projectId}/planning/lanes/{unicodeWire}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        RosterNames(client, projectId).ShouldNotContain(UnicodeName);
        RoomNames(client, projectId).ShouldBe(["all"]);
    }

    [Fact]
    public async Task TrailingPercentName_BirthsAndRoundTripsLiterally_GhostsAnswerNamed404()
    {
        var percentWire = Uri.EscapeDataString(TrailingPercentName);
        var ghostWire = Uri.EscapeDataString("zz.Ghost%");
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);
        await SeedAgentSessionAsync();

        var birth = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/lanes",
            new { Name = TrailingPercentName, Contour = "src/release/**" }, JsonOptions);
        birth.StatusCode.ShouldBe(HttpStatusCode.OK);
        LaneRowOf(client, projectId, TrailingPercentName)
            .GetProperty("contour").GetString().ShouldBe("src/release/**");

        var posted = await SendAgentAsync(
            client, HttpMethod.Post, $"/api/agent/projects/{projectId}/wave/rooms/{percentWire}/messages",
            new { Text = "percent tail survives the door" });
        posted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var message = await posted.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        message.GetProperty("room").GetString().ShouldBe(TrailingPercentName);
        message.GetProperty("text").GetString().ShouldBe("percent tail survives the door");

        var history = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{percentWire}/messages", null);
        history.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = (await history.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("items");
        items.GetArrayLength().ShouldBe(1);
        items[0].GetProperty("room").GetString().ShouldBe(TrailingPercentName);
        items[0].GetProperty("text").GetString().ShouldBe("percent tail survives the door");

        var updated = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/lanes/{percentWire}",
            new { Contour = "src/release-50/**" }, JsonOptions);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        LaneRowOf(client, projectId, TrailingPercentName)
            .GetProperty("contour").GetString().ShouldBe("src/release-50/**");

        var deleted = await client.DeleteAsync($"/api/projects/{projectId}/planning/lanes/{percentWire}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        RosterNames(client, projectId).ShouldNotContain(TrailingPercentName);
        RoomNames(client, projectId).ShouldBe(["all"]);

        var laneGhost = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/lanes/{ghostWire}",
            new { Contour = "src/ghost/**" }, JsonOptions);
        laneGhost.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var ghostProblem = await laneGhost.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        ghostProblem.GetProperty("title").GetString().ShouldBe("MissingLane");
        ghostProblem.GetProperty("detail").GetString()!.ShouldContain("zz.Ghost%");

        var roomGhost = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{ghostWire}/peek", null);
        roomGhost.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var roomGhostProblem = await roomGhost.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        roomGhostProblem.GetProperty("title").GetString().ShouldBe("MissingWaveRoom");
        roomGhostProblem.GetProperty("detail").GetString()!.ShouldContain("'zz.Ghost%'");
    }

    private async Task<HttpResponseMessage> SendAgentAsync(
        HttpClient client, HttpMethod method, string url, object? body)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add(SessionHeader.Name, _sessionId.ToString());
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return await client.SendAsync(request);
    }

    private static JsonElement LaneRowOf(HttpClient client, Guid projectId, string name)
    {
        var roster = client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{projectId}/planning/lanes", JsonOptions).GetAwaiter().GetResult();
        var lane = roster.GetProperty("lanes").EnumerateArray()
            .FirstOrDefault(row => row.GetProperty("name").GetString() == name);
        lane.ValueKind.ShouldBe(JsonValueKind.Object, $"lane '{name}' is missing from the roster");
        return lane;
    }

    private static string[] RosterNames(HttpClient client, Guid projectId)
    {
        var roster = client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{projectId}/planning/lanes", JsonOptions).GetAwaiter().GetResult();
        return [.. roster.GetProperty("lanes").EnumerateArray()
            .Select(row => row.GetProperty("name").GetString()!)];
    }

    private static string[] RoomNames(HttpClient client, Guid projectId)
    {
        var rooms = client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{projectId}/wave/rooms", JsonOptions).GetAwaiter().GetResult();
        return [.. rooms.GetProperty("items").EnumerateArray()
            .Select(row => row.GetProperty("room").GetString()!)];
    }

    private async Task<Guid> CreateProjectAsync(HttpClient client)
    {
        var name = $"kestrel-roundtrip-{Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = Path.Combine(Path.GetTempPath(), "overmind-kestrel-test", name),
            Description = (string?)null,
        }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return project.GetProperty("id").GetGuid();
    }

    private async Task SeedLaneAsync(Guid projectId, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        db.PlanningLanes.Add(PlanningLane.Create(projectId, name, contour: null, serialOrder: null));
        await db.SaveChangesAsync();
    }

    private async Task SeedAgentSessionAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        db.AgentSessions.Add(AgentSession.Restore(_sessionId, "kestrel-probe", "claude",
            DateTime.UtcNow, DateTime.UtcNow));
        await db.SaveChangesAsync();
    }
}
