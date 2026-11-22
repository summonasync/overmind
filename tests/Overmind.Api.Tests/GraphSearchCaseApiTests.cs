using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// The search ceiling on the graph door, pinned rather than left to surprise: the projection matches with SQLite's
// LIKE, which folds case for ASCII letters only. An owner whose corpus is Cyrillic gets case-insensitive hits on
// Latin titles and case-SENSITIVE hits on Cyrillic ones — so the door documents the limit instead of promising a
// normalisation it cannot deliver, and these tests hold the exact behaviour to it.
public sealed class GraphSearchCaseApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private Guid _cyrUpper, _cyrLower;
    private Guid _asciiUpper, _asciiLower;

    public GraphSearchCaseApiTests() => _client = _factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();

        var project = Project.Create("searchcase", "/tmp/graph/searchcase");
        db.Projects.Add(project);

        _cyrUpper = Add(db, project.Id, "Заметка Один", "cyrillic title, capital first letter");
        _cyrLower = Add(db, project.Id, "заметка два", "cyrillic title, lowercase first letter");
        _asciiUpper = Add(db, project.Id, "Relay Plan", "ascii title, capital");
        _asciiLower = Add(db, project.Id, "relay plan", "ascii title, lowercase");
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Search_Ascii_IsCaseInsensitive_BothCasesMatch()
    {
        var upper = await Search("RELAY");
        var lower = await Search("relay");

        upper.ShouldBe([_asciiUpper, _asciiLower], ignoreOrder: true,
            customMessage: "ASCII LIKE folds case, both rows answer");
        lower.ShouldBe(upper);
    }

    [Fact]
    public async Task Search_Cyrillic_IsCaseSensitive_OnlyTheExactCaseMatches()
    {
        // The documented ceiling: a Cyrillic term in the wrong case returns nothing, because SQLite LIKE does not
        // fold non-ASCII. Searching the lowercase form matches only the lowercase title, not the capitalised one.
        var lower = await Search("заметка");
        lower.ShouldBe([_cyrLower],
            customMessage: "Cyrillic search is case-sensitive; the capitalised title does not answer");

        var upper = await Search("Заметка");
        upper.ShouldBe([_cyrUpper]);

        lower.ShouldNotContain(_cyrUpper);
        upper.ShouldNotContain(_cyrLower);
    }

    private async Task<List<Guid>> Search(string term)
    {
        var response = await _client.GetAsync($"/api/graph?search={Uri.EscapeDataString(term)}");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<GraphEnvelope>(JsonOptions);
        return [.. payload!.Nodes.Where(node => node.Kind == "entry").Select(node => node.Id)];
    }

    private static Guid Add(OvermindDbContext db, Guid projectId, string title, string content)
    {
        var entry = ContextEntry.Create(projectId, EntryType.Journal, title, content);
        db.ContextEntries.Add(entry);
        return entry.Id;
    }

    private sealed record GraphNodeDto(string Kind, Guid Id);

    private sealed record GraphEnvelope(List<GraphNodeDto> Nodes);
}
