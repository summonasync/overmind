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

// The graph door folds search case for every corpus: the term is lowered in C# (invariant lower covers
// Cyrillic where SQLite's LIKE only folds ASCII) and matched against normalized title columns written on
// every save and re-folded by the startup backfill. These tests hold the promise — a Cyrillic term finds
// its title in either casing, wildcards stay literal, projects answer through their normalized name.
public sealed class GraphSearchCaseApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private Guid _cyrUpper, _cyrLower;
    private Guid _asciiUpper, _asciiLower;
    private Guid _percent;
    private Guid _projectId;

    public GraphSearchCaseApiTests() => _client = _factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();

        var project = Project.Create("searchcase", "/tmp/graph/searchcase");
        db.Projects.Add(project);
        _projectId = project.Id;

        _cyrUpper = Add(db, project.Id, "Заметка Один", "cyrillic title, capital first letter");
        _cyrLower = Add(db, project.Id, "заметка два", "cyrillic title, lowercase first letter");
        _asciiUpper = Add(db, project.Id, "Relay Plan", "ascii title, capital");
        _asciiLower = Add(db, project.Id, "relay plan", "ascii title, lowercase");
        _percent = Add(db, project.Id, "100% надёжно", "percent-sign title");
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
    public async Task Search_Cyrillic_IsCaseInsensitive_BothCasesMatch()
    {
        var lower = await Search("заметка");
        lower.ShouldBe([_cyrUpper, _cyrLower], ignoreOrder: true,
            customMessage: "the C# fold covers Cyrillic; both titles answer either casing");

        var upper = await Search("Заметка");
        upper.ShouldBe(lower);
    }

    [Fact]
    public async Task Search_Wildcards_MatchLiterally()
    {
        var hits = await Search("%");

        hits.ShouldBe([_percent],
            customMessage: "a bare % finds the percent-sign title, not everything");
    }

    [Fact]
    public async Task Search_Projects_AnswerThroughTheNormalizedName()
    {
        var response = await _client.GetAsync($"/api/graph?search={Uri.EscapeDataString("SEARCHCASE")}");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<GraphEnvelope>(JsonOptions);
        payload!.Nodes.ShouldContain(node => node.Kind == "project" && node.Id == _projectId,
            customMessage: "the project answers its name in any casing through NameNormalized");
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
