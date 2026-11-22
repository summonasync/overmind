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
public sealed class WaveSlashRoomRoutingTests(KestrelApiFactory factory)
{
    private const string SlashRoom = "C. Edge/Pi";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Guid _sessionId = Guid.NewGuid();

    [Fact]
    public async Task EncodedSlashRoomWire_RoutesAsOneSegment_AndServesTheWholeFamilyOnTheDecodedName()
    {
        var segment = Uri.EscapeDataString(SlashRoom);
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);
        await SeedLegacyLaneAsync(projectId, SlashRoom);
        await SeedAgentSessionAsync();

        var posted = await SendAgentAsync(
            client, HttpMethod.Post, $"/api/agent/projects/{projectId}/wave/rooms/{segment}/messages",
            new { Text = "kestrel wire delivery" });

        posted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var message = await posted.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        message.GetProperty("room").GetString().ShouldBe(SlashRoom);
        message.GetProperty("id").GetString().ShouldNotBeNullOrWhiteSpace();

        var history = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{segment}/messages", null);
        history.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await history.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        RoomsOf(page).ShouldBe([SlashRoom]);

        var peek = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{segment}/peek", null);
        peek.StatusCode.ShouldBe(HttpStatusCode.OK);
        var counts = await peek.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        counts.GetProperty("room").GetString().ShouldBe(SlashRoom);
        counts.GetProperty("count").GetInt32().ShouldBe(1);

        var unread = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{segment}/unread", null);
        unread.StatusCode.ShouldBe(HttpStatusCode.OK);
        var feed = await unread.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        RoomsOf(feed).ShouldBe([SlashRoom]);

        var hallFeed = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/all/unread", null);
        var hall = await hallFeed.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        RoomsOf(hall).ShouldBeEmpty();
    }

    [Fact]
    public async Task UnknownRoomOnTheEncodedWire_Answers404Problem_NamingTheRoomDecoded()
    {
        var segment = Uri.EscapeDataString("zzNoSuchRoom/X");
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);
        await SeedLegacyLaneAsync(projectId, SlashRoom);
        await SeedAgentSessionAsync();

        var response = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{segment}/peek", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        problem.GetProperty("title").GetString().ShouldBe("MissingWaveRoom");
        var detail = problem.GetProperty("detail").GetString();
        detail!.ShouldContain("zzNoSuchRoom/X");
        detail.ShouldNotContain("%2F");
    }

    [Fact]
    public async Task RawSlashWire_Post_IsRefused405_WithAnEmptyBodyAndNoContentType()
    {
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);
        await SeedLegacyLaneAsync(projectId, SlashRoom);
        await SeedAgentSessionAsync();

        var raw = await SendAgentAsync(
            client, HttpMethod.Post, $"/api/agent/projects/{projectId}/wave/rooms/C.%20Edge/Pi/messages",
            new { Text = "unencoded wire" });

        raw.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        (await raw.Content.ReadAsStringAsync()).ShouldBeEmpty();
        raw.Content.Headers.ContentType.ShouldBeNull();
    }

    [Fact]
    public async Task DoubleEncodedWireName_LandsInTheLiteralRoom_NeverInTheSpelledTwins()
    {
        const string literalRoom = "zz.Cross%20A";
        const string spacedRoom = "zz.Cross A";
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);
        await SeedLegacyLaneAsync(projectId, literalRoom);
        await SeedLegacyLaneAsync(projectId, spacedRoom);
        await SeedAgentSessionAsync();

        var wire = Uri.EscapeDataString(literalRoom);
        var posted = await SendAgentAsync(
            client, HttpMethod.Post, $"/api/agent/projects/{projectId}/wave/rooms/{wire}/messages",
            new { Text = "cross delivery probe" });

        posted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var message = await posted.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        message.GetProperty("room").GetString().ShouldBe(literalRoom);

        var literalHistory = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{wire}/messages", null);
        literalHistory.StatusCode.ShouldBe(HttpStatusCode.OK);
        var literalPage = await literalHistory.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        RoomsOf(literalPage).ShouldBe([literalRoom]);

        var spacedSegment = Uri.EscapeDataString(spacedRoom);
        var spacedHistory = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{spacedSegment}/messages", null);
        spacedHistory.StatusCode.ShouldBe(HttpStatusCode.OK);
        var spacedPage = await spacedHistory.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        RoomsOf(spacedPage).ShouldBeEmpty();
    }

    [Fact]
    public async Task LiteralEscapeName_IsShadowedByItsSlashTwin_AndAnswersNamed404WhileTheTwinIsAbsent()
    {
        const string shadowedName = "zz.Pair%2FTest";
        const string twinName = "zz.Pair/Test";
        var shadowedWire = Uri.EscapeDataString(shadowedName);
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);
        await SeedLegacyLaneAsync(projectId, shadowedName);
        await SeedAgentSessionAsync();

        var orphan = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{shadowedWire}/peek", null);
        orphan.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var orphanProblem = await orphan.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        orphanProblem.GetProperty("title").GetString().ShouldBe("MissingWaveRoom");
        orphanProblem.GetProperty("detail").GetString()!.ShouldContain(twinName);

        await SeedLegacyLaneAsync(projectId, twinName);
        var twin = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{shadowedWire}/peek", null);
        twin.StatusCode.ShouldBe(HttpStatusCode.OK);
        var twinCounts = await twin.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        twinCounts.GetProperty("room").GetString().ShouldBe(twinName);
    }

    [Fact]
    public async Task TrailingPercentName_RoundTripsLiterally_AndAnUnknownGhostAnswersNamed404NotAThrow()
    {
        const string percentRoom = "release 50%";
        var percentWire = Uri.EscapeDataString(percentRoom);
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);
        await SeedLegacyLaneAsync(projectId, percentRoom);
        await SeedAgentSessionAsync();

        var posted = await SendAgentAsync(
            client, HttpMethod.Post, $"/api/agent/projects/{projectId}/wave/rooms/{percentWire}/messages",
            new { Text = "percent tail survives" });

        posted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var message = await posted.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        message.GetProperty("room").GetString().ShouldBe(percentRoom);

        var peek = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{percentWire}/peek", null);
        peek.StatusCode.ShouldBe(HttpStatusCode.OK);
        var counts = await peek.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        counts.GetProperty("room").GetString().ShouldBe(percentRoom);
        counts.GetProperty("count").GetInt32().ShouldBe(1);

        var ghostWire = Uri.EscapeDataString("zz.Ghost%");
        var ghost = await SendAgentAsync(
            client, HttpMethod.Get, $"/api/agent/projects/{projectId}/wave/rooms/{ghostWire}/peek", null);
        ghost.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var ghostProblem = await ghost.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        ghostProblem.GetProperty("title").GetString().ShouldBe("MissingWaveRoom");
        ghostProblem.GetProperty("detail").GetString()!.ShouldContain("zz.Ghost%");
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

    private static IReadOnlyList<string> RoomsOf(JsonElement feed) =>
    [
        .. feed.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("room").GetString()!)
    ];

    private async Task<Guid> CreateProjectAsync(HttpClient client)
    {
        var name = $"kestrel-slash-{Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = Path.Combine(Path.GetTempPath(), "overmind-kestrel-test", name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return project.GetProperty("id").GetGuid();
    }

    private async Task SeedAgentSessionAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        db.AgentSessions.Add(AgentSession.Restore(_sessionId, "kestrel-probe", "claude",
            DateTime.UtcNow, DateTime.UtcNow));
        await db.SaveChangesAsync();
    }

    private async Task SeedLegacyLaneAsync(Guid projectId, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        db.PlanningLanes.Add(PlanningLane.Create(projectId, name, contour: null, serialOrder: null));
        await db.SaveChangesAsync();
    }
}
