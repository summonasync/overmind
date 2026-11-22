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
public sealed class RoomSlashNameBirthRefusalTests(KestrelApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Guid _sessionId = Guid.NewGuid();

    [Fact]
    public async Task CreateDoor_SlashRoomName_IsRefused400NamingInvalidLaneName_AndTheRoomFamilyStaysUnborn()
    {
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);

        var post = await client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/lanes",
            new { Name = "zz.Birth/Edge" }, JsonOptions);

        post.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        post.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await post.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        problem.GetProperty("title").GetString().ShouldBe("InvalidLaneName");
        problem.GetProperty("detail").GetString()!.ShouldContain("zz.Birth/Edge");

        var roster = await client.GetAsync($"/api/agent/projects/{projectId}/planning/lanes");
        roster.StatusCode.ShouldBe(HttpStatusCode.OK);
        var lanes = await roster.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        NamesOf(lanes).ShouldBeEmpty();

        var rooms = await client.GetAsync($"/api/agent/projects/{projectId}/wave/rooms");
        rooms.StatusCode.ShouldBe(HttpStatusCode.OK);
        var family = await rooms.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        RoomNamesOf(family).ShouldBe(["all"]);
    }

    [Fact]
    public async Task RosterWriterDoor_SlashNameBirth_IsRefused400NamingInvalidLaneName_AndKeepsTheStoredRoster()
    {
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);

        var born = await client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/lanes",
            new { Name = "frontend" }, JsonOptions);
        born.StatusCode.ShouldBe(HttpStatusCode.OK);

        var put = await client.PutAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/lanes-roster",
            new { Lanes = new[] { new { Name = "frontend" }, new { Name = "zz.Slash/Edge" } } }, JsonOptions);

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await put.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        problem.GetProperty("title").GetString().ShouldBe("InvalidLaneName");

        var roster = await client.GetAsync($"/api/agent/projects/{projectId}/planning/lanes");
        var lanes = await roster.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        NamesOf(lanes).ShouldBe(["frontend"]);
    }

    [Fact]
    public async Task WavePostIntoANeverBornSlashRoom_AnswersNamed404CarryingTheDecodedName()
    {
        var segment = Uri.EscapeDataString("zz.NoSuch/Edge");
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);
        await SeedAgentSessionAsync();

        var posted = await SendAgentAsync(
            client, HttpMethod.Post, $"/api/agent/projects/{projectId}/wave/rooms/{segment}/messages",
            new { Text = "never born" });

        posted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        posted.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await posted.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        problem.GetProperty("title").GetString().ShouldBe("MissingWaveRoom");
        var detail = problem.GetProperty("detail").GetString();
        detail.ShouldNotBeNull();
        detail.ShouldContain("zz.NoSuch/Edge");
        detail.ShouldNotContain("%2F");
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

    private static IReadOnlyList<string> NamesOf(JsonElement roster) =>
    [
        .. roster.GetProperty("lanes").EnumerateArray()
            .Select(lane => lane.GetProperty("name").GetString()!)
    ];

    private static IReadOnlyList<string> RoomNamesOf(JsonElement rooms) =>
    [
        .. rooms.GetProperty("items").EnumerateArray()
            .Select(room => room.GetProperty("room").GetString()!)
    ];

    private async Task<Guid> CreateProjectAsync(HttpClient client)
    {
        var name = $"kestrel-birth-{Guid.NewGuid():N}";
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
}
