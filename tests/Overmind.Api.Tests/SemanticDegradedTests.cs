using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Overmind.Api.Tests;

public sealed class SemanticDegradedTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SemanticDegradedTests(ITestOutputHelper output)
    {
        _output = output;
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Health_ReportsVectorSearchDown_WithoutQdrant()
    {
        if (await IsVectorSearchUp())
        {
            _output.WriteLine("Qdrant is reachable on this machine; skipping degraded health assertion.");
            return;
        }

        var health = await _client.GetFromJsonAsync<HealthJson>("/api/health", JsonOptions);
        health.ShouldNotBeNull();
        health!.VectorSearch.ShouldBe("down");
    }

    [Fact]
    public async Task FulltextSearch_Works_WithoutQdrant()
    {
        var projectId = await CreateProjectAsync("degraded-fts");
        await CreateEntryAsync(projectId, "pineapple express", "a strain of tropical fruit");

        var result = await _client.GetFromJsonAsync<SearchJson>("/api/search?q=pineapple", JsonOptions);

        result.ShouldNotBeNull();
        result!.Hits.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task SemanticMode_Returns503_WhenQdrantIsDown()
    {
        if (await IsVectorSearchUp())
        {
            _output.WriteLine("Qdrant is reachable on this machine; skipping degraded 503 assertion.");
            return;
        }

        var response = await _client.GetAsync("/api/search?q=anything&mode=semantic");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
        document.RootElement.GetProperty("detail").GetString().ShouldNotBeNullOrWhiteSpace();
        document.RootElement.GetProperty("status").GetInt32().ShouldBe(503);
    }

    [Fact]
    public async Task Search_RejectsUnknownMode()
    {
        var response = await _client.GetAsync("/api/search?q=test&mode=hybrid");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EntryCreate_WithQdrantDown_Returns201_AndFtsStillFindsIt()
    {
        var projectId = await CreateProjectAsync("degraded-crud");

        var created = await CreateEntryAsync(projectId, "quokka facts", "the quokka is a small marsupial");
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var found = await _client.GetFromJsonAsync<SearchJson>("/api/search?q=quokka", JsonOptions);
        found.ShouldNotBeNull();
        found!.Hits.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task EntryDelete_WithQdrantDown_StillSucceeds()
    {
        var projectId = await CreateProjectAsync("degraded-delete");
        var created = await CreateEntryAsync(projectId, "wombat", "stocky australian marsupial");
        var entryId = (await created.Content.ReadFromJsonAsync<EntryJson>(JsonOptions))!.Id;

        var deleted = await _client.DeleteAsync($"/api/entries/{entryId}");

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Reindex_ReturnsReport_WhenQdrantIsDown()
    {
        if (await IsVectorSearchUp())
        {
            _output.WriteLine("Qdrant is reachable on this machine; skipping degraded reindex assertion.");
            return;
        }

        var projectId = await CreateProjectAsync("degraded-reindex");
        await CreateEntryAsync(projectId, "numbat notes", "termite eating marsupial");

        var response = await _client.PostAsync("/api/search/reindex", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<ReindexJson>(JsonOptions);
        report.ShouldNotBeNull();
        report!.Total.ShouldBe(1);
        report.Indexed.ShouldBe(0);
        report.Failed.ShouldBe(1);
    }

    private async Task<bool> IsVectorSearchUp()
    {
        var health = await _client.GetFromJsonAsync<HealthJson>("/api/health", JsonOptions);
        return health?.VectorSearch == "up";
    }

    private async Task<Guid> CreateProjectAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name + Guid.NewGuid().ToString("N")[..6],
            DiskPath = "E:\\work\\" + name,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions))!.Id;
    }

    private async Task<HttpResponseMessage> CreateEntryAsync(Guid projectId, string title, string content)
    {
        return await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = "Note",
            Title = title,
            Content = content,
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });
    }

    public sealed record ProjectJson(Guid Id);

    public sealed record EntryJson(Guid Id);

    public sealed record HealthJson(string Status, string VectorSearch, string Version);

    public sealed record HitJson(Guid EntryId, string Title, string Snippet, double Rank);

    public sealed record SearchJson(string Query, List<HitJson> Hits);

    public sealed record ReindexJson(int Total, int Indexed, int Failed);
}
