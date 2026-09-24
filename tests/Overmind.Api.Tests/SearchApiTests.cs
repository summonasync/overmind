using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class SearchApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Guid _projectId;
    private readonly Guid _otherProjectId;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SearchApiTests()
    {
        _client = _factory.CreateClient();
        _projectId = CreateProject("search-main");
        _otherProjectId = CreateProject("search-other");

        CreateEntryAsync(_projectId, "giraffe notes", "the giraffe has a long neck").GetAwaiter().GetResult();
        CreateEntryAsync(_otherProjectId, "giraffe elsewhere", "another giraffe record").GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Search_ReturnsHitsAcrossProjects()
    {
        var result = await _client.GetFromJsonAsync<SearchJson>("/api/search?q=giraffe", JsonOptions);
        result.ShouldNotBeNull();
        result!.Hits.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Search_ProjectFilter_ScopesHits()
    {
        var result = await _client.GetFromJsonAsync<SearchJson>(
            $"/api/search?q=giraffe&projectId={_projectId}", JsonOptions);

        result.ShouldNotBeNull();
        result!.Hits.Count.ShouldBe(1);
        result.Hits[0].Title.ShouldBe("giraffe notes");
    }

    [Fact]
    public async Task Search_Metacharacters_ReturnEmptyInsteadOfError()
    {
        var response = await _client.GetAsync("/api/search?q=%22%28%28broken");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SearchJson>(JsonOptions);
        result.ShouldNotBeNull();
        result!.Hits.ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_MissingQuery_Returns400()
    {
        var response = await _client.GetAsync("/api/search");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_Union_FindsTaskBodyTerm_WithSnippetFeatureSlugAndArtifactId()
    {
        var projectId = CreateProject("search-planning");
        await CreateFeatureAsync(projectId, "039-field-acceptance", "Field acceptance");
        var artifactId = await CreateArtifactAsync(
            projectId, "039-field-acceptance", "task", "<goal>Deliver the quokkacounter widget.</goal>", order: 2);

        using var doc = await SearchJsonAsync("quokkacounter");

        var hit = doc.RootElement.GetProperty("hits").EnumerateArray().ShouldHaveSingleItem();
        hit.GetProperty("source").GetString().ShouldBe("artifact");
        hit.GetProperty("artifactId").GetString().ShouldBe(artifactId.ToString());
        hit.GetProperty("featureSlug").GetString().ShouldBe("039-field-acceptance");
        hit.GetProperty("kind").GetString().ShouldBe("task");
        hit.GetProperty("order").GetInt32().ShouldBe(2);
        hit.GetProperty("label").GetString().ShouldBe("039 task");
        hit.GetProperty("snippet").GetString().ShouldNotBeNull().ShouldContain("quokkacounter");
    }

    [Fact]
    public async Task Search_Union_HitsAreSourceTagged_EntryKeysStayCompatible()
    {
        var projectId = CreateProject("search-union-shapes");
        await CreateEntryAsync(projectId, "axolotl entry", "the axolotl regenerates limbs");
        await CreateFeatureAsync(projectId, "040-axolotl-feature", "Axolotl feature", "feature body studies the axolotl");
        await CreateArtifactAsync(projectId, "040-axolotl-feature", "spec", "spec body covers axolotl care", order: 0);

        using var doc = await SearchJsonAsync("axolotl");
        var hits = doc.RootElement.GetProperty("hits").EnumerateArray().ToArray();

        hits.Select(h => h.GetProperty("source").GetString())
            .ShouldBe(["entry", "feature", "artifact"]);

        var entry = hits[0];
        PropertyNames(entry).ShouldBe(["entryId", "rank", "snippet", "source", "title", "type"]);
        entry.GetProperty("entryId").GetString().ShouldNotBeNullOrEmpty();
        entry.GetProperty("title").GetString().ShouldBe("axolotl entry");
        entry.GetProperty("type").GetString().ShouldBe("note");

        var feature = hits[1];
        PropertyNames(feature).ShouldBe(["featureSlug", "rank", "snippet", "source", "title"]);
        feature.GetProperty("featureSlug").GetString().ShouldBe("040-axolotl-feature");
        feature.GetProperty("title").GetString().ShouldBe("Axolotl feature");
        feature.GetProperty("snippet").GetString().ShouldNotBeNull().ShouldContain("axolotl");

        var artifact = hits[2];
        PropertyNames(artifact).ShouldBe(
        ["artifactId", "featureSlug", "kind", "label", "order", "rank", "snippet", "source"]);
        artifact.GetProperty("featureSlug").GetString().ShouldBe("040-axolotl-feature");
        artifact.GetProperty("kind").GetString().ShouldBe("spec");
        artifact.GetProperty("order").GetInt32().ShouldBe(0);
        artifact.GetProperty("label").GetString().ShouldBe("040 spec");
        artifact.GetProperty("snippet").GetString().ShouldNotBeNull().ShouldContain("axolotl");
    }

    private Guid CreateProject(string name)
    {
        var response = _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
        }).GetAwaiter().GetResult();

        response.EnsureSuccessStatusCode();
        return response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions).GetAwaiter().GetResult()!.Id;
    }

    private async Task CreateEntryAsync(Guid projectId, string title, string content)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = "Note",
            Title = title,
            Content = content,
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

        response.EnsureSuccessStatusCode();
    }

    private async Task CreateFeatureAsync(Guid projectId, string slug, string title, string body = "")
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = body });

        response.EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateArtifactAsync(
        Guid projectId, string slug, string kind, string body, int order)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}/artifacts",
            new { Kind = kind, Body = body, Order = order });

        response.EnsureSuccessStatusCode();
        var artifact = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        artifact.ShouldNotBeNull();
        return artifact!.Id;
    }

    private async Task<JsonDocument> SearchJsonAsync(string query)
    {
        var response = await _client.GetAsync($"/api/search?q={Uri.EscapeDataString(query)}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string[] PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order().ToArray();

    public sealed record ProjectJson(Guid Id);

    public sealed record HitJson(Guid EntryId, string Title, string Snippet, double Rank);

    public sealed record SearchJson(string Query, List<HitJson> Hits);

    public sealed record ArtifactJson(Guid Id);
}
