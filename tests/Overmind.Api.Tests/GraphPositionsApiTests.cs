using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// T8's door: the owner pins a dot in one browser, the same dot sits pinned in every other browser
// against the same Host, and it survives a restart — because the row lives in the database and
// nowhere else (R5 forbids a client-side copy). The read payload carries only pinned rows, so
// "pinned" is not a flag the canvas interprets, it is the shape of the answer.
public sealed class GraphPositionsApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private Guid _projectId;
    private Guid _noteId;

    public GraphPositionsApiTests() => _client = _factory.CreateClient();

    public async Task InitializeAsync()
    {
        var created = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = "positions",
            DiskPath = TestPaths.DiskPath("positions"),
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await created.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        _projectId = project!.Id;

        var note = await _client.PostAsJsonAsync("/api/entries", new
        {
            projectId = _projectId,
            type = "journal",
            title = "Pinned note",
            content = "the dot that stays",
        });
        note.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await note.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        _noteId = entry!.Id;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Put_ThroughTheHumanDoor_PinsReachTheGraphRead()
    {
        var saved = await PutPositionsAsync(_client, PinBody(_noteId, 12.5, 34.25, pinned: true));

        saved.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var payload = await GraphAsync(_client);
        var pin = payload.Positions.ShouldHaveSingleItem();
        pin.Kind.ShouldBe("entry");
        pin.Id.ShouldBe(_noteId);
        pin.X.ShouldBe(12.5);
        pin.Y.ShouldBe(34.25);
    }

    [Fact]
    public async Task RePin_MovesTheDot_AndUnpinReturnsTheNodeToTheSimulation()
    {
        await PutPositionsAsync(_client, PinBody(_noteId, 1, 2, pinned: true));

        await PutPositionsAsync(_client, PinBody(_noteId, 9, 8, pinned: true));
        var moved = await GraphAsync(_client);
        var pin = moved.Positions.ShouldHaveSingleItem();
        pin.X.ShouldBe(9);
        pin.Y.ShouldBe(8);

        await PutPositionsAsync(_client, PinBody(_noteId, 9, 8, pinned: false));
        var field = await GraphAsync(_client);
        field.Positions.ShouldBeEmpty("an unpinned node is simulated again, so no row reaches the read");

        using var scope = _factory.Services.CreateScope();
        var row = scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .GraphNodePositions.Single();
        row.Pinned.ShouldBeFalse("the row keeps the last spot; the read keeps it out while Pinned is false");
        row.X.ShouldBe(9);
    }

    [Fact]
    public async Task UnknownOrTaskKind_IsANamedRefusal_NeverABlank400()
    {
        var task = await PutPositionsAsync(_client,
            $$"""{"positions":[{"kind":"task","id":"{{_noteId}}","x":1,"y":2,"pinned":true}]}""");
        task.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadProblemAsync(task)).Title.ShouldBe("UnknownGraphNodeKind");

        var nonsense = await PutPositionsAsync(_client,
            $$"""{"positions":[{"kind":"sticker","id":"{{_noteId}}","x":1,"y":2,"pinned":true}]}""");
        nonsense.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadProblemAsync(nonsense)).Title.ShouldBe("UnknownGraphNodeKind");

        var empty = await PutPositionsAsync(_client, "{}");
        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadProblemAsync(empty)).Title.ShouldBe("InvalidGraphPositionsShape");

        (await GraphAsync(_client)).Positions.ShouldBeEmpty("a refused batch writes nothing");
    }

    [Fact]
    public async Task Positions_MatchAcrossTwoBrowserProfiles_AgainstOneHost()
    {
        var first = _factory.CreateClient();
        var second = _factory.CreateClient();

        await PutPositionsAsync(first, PinBody(_noteId, 110, 90, pinned: true));

        var seen = await GraphAsync(second);
        var pin = seen.Positions.ShouldHaveSingleItem();
        pin.Id.ShouldBe(_noteId);
        pin.X.ShouldBe(110);
        pin.Y.ShouldBe(90);

        first.Dispose();
        second.Dispose();
    }

    [Fact]
    public async Task Positions_SurviveAHostRestart_AgainstTheSameDatabase()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-positions-restart-{Guid.NewGuid():N}.db");
        try
        {
            Guid noteId;
            using (var first = new ApiFactory(databasePath, preserveDatabase: true))
            {
                using var client = first.CreateClient();
                var project = await client.PostAsJsonAsync("/api/projects", new
                {
                    Name = "restart",
                    DiskPath = TestPaths.DiskPath("restart-positions"),
                }, JsonOptions);
                project.StatusCode.ShouldBe(HttpStatusCode.Created);
                var projectId = (await project.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions))!.Id;

                var note = await client.PostAsJsonAsync("/api/entries", new
                {
                    projectId,
                    type = "journal",
                    title = "Restart survivor",
                    content = "pinned before the restart",
                });
                note.StatusCode.ShouldBe(HttpStatusCode.Created);
                noteId = (await note.Content.ReadFromJsonAsync<EntryJson>(JsonOptions))!.Id;

                var saved = await PutPositionsAsync(client, PinBody(noteId, 55, 66, pinned: true));
                saved.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            }

            using (var second = new ApiFactory(databasePath, preserveDatabase: true))
            {
                using var client = second.CreateClient();
                var payload = await GraphAsync(client);
                var pin = payload.Positions.ShouldHaveSingleItem();
                pin.Id.ShouldBe(noteId);
                pin.X.ShouldBe(55);
                pin.Y.ShouldBe(66);
            }
        }
        finally
        {
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                var file = databasePath + suffix;
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }

    private static string PinBody(Guid noteId, double x, double y, bool pinned) => $$"""
        {"positions":[{"kind":"entry","id":"{{noteId}}","x":{{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"y":{{y.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"pinned":{{(pinned ? "true" : "false")}}}]}
        """;

    private static async Task<HttpResponseMessage> PutPositionsAsync(HttpClient client, string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/graph/positions")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        return await client.SendAsync(request);
    }

    private async Task<GraphEnvelope> GraphAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/graph");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        return (await response.Content.ReadFromJsonAsync<GraphEnvelope>(JsonOptions))!;
    }

    private static async Task<ProblemJson> ReadProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        return problem!;
    }

    private sealed record ProjectJson(Guid Id);

    private sealed record EntryJson(Guid Id);

    private sealed record GraphPositionDto(string Kind, Guid Id, double X, double Y);

    private sealed record GraphEnvelope(
        List<GraphNodeJson> Nodes,
        List<object> Edges,
        List<GraphPositionDto> Positions,
        object Counts);

    private sealed record GraphNodeJson(string Kind, Guid Id, string Label, Guid ProjectId, bool Orphan);

    private sealed record ProblemJson(string? Title, string? Detail, int Status);
}
