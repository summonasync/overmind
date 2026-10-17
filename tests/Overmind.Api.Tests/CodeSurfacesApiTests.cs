using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class CodeSurfacesApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _projectDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CodeSurfacesApiTests()
    {
        _client = _factory.CreateClient();
        _projectDir = Path.Combine(Path.GetTempPath(), $"overmind-surfaces-api-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();

        if (Directory.Exists(_projectDir))
        {
            Directory.Delete(_projectDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("src/../secrets")]
    [InlineData("/absolute/path")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("src//double")]
    [InlineData("./current")]
    [InlineData("src/config?/app.cs")]
    [InlineData("src/[ab]/**")]
    [InlineData("src/{a,b}/**")]
    [InlineData(@"src\web\**")]
    public async Task Update_InvalidSurfacePattern_Returns400(string pattern)
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project, "alpha", "Alpha");

        var response = await PutCodeSurfacesAsync(project, "alpha", [pattern]);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem!.Errors.ShouldNotBeNull();
    }

    [Fact]
    public async Task AgentUpdate_UnsupportedWildcardMetacharacter_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project, "alpha", "Alpha");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project}/planning/features/alpha/update",
            new { LogNote = "declaring surfaces", CodeSurfaces = new[] { "src/config?/app.cs" } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldContain("* and ** wildcards");
    }

    [Fact]
    public async Task Update_OverThePatternCountCap_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project, "alpha", "Alpha");
        var patterns = Enumerable.Range(0, 17).Select(i => $"src/area-{i}/**").ToArray();

        var response = await PutCodeSurfacesAsync(project, "alpha", patterns);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_CodeSurfaces_RoundTripAndQueueOverlapProjection()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project, "alpha", "Alpha");
        await CreateFeatureAsync(project, "beta", "Beta");
        await ClaimAsync(project, "alpha");
        await ClaimAsync(project, "beta");
        await PutCodeSurfacesAsync(project, "alpha", ["src/web/**", "docs/**"]);
        await PutCodeSurfacesAsync(project, "beta", ["src/web/**/*.tsx"]);

        var feature = await GetFeatureAsync(project, "alpha");
        feature.GetProperty("codeSurfaces").EnumerateArray()
            .Select(v => v.GetString()).ShouldBe(["src/web/**", "docs/**"]);
        var featureOverlaps = feature.GetProperty("potentialOverlaps");
        featureOverlaps.GetArrayLength().ShouldBe(1);
        featureOverlaps[0].GetProperty("withFeature").GetString().ShouldBe("beta");
        featureOverlaps[0].GetProperty("sharedGlobs").EnumerateArray()
            .Select(v => v.GetString()).ShouldBe(["src/web/**"]);

        var queue = await GetQueueAsync(project);
        var alphaRow = queue.Single(i => i.GetProperty("slug").GetString() == "alpha");
        var betaRow = queue.Single(i => i.GetProperty("slug").GetString() == "beta");
        var alphaOverlaps = alphaRow.GetProperty("potentialOverlaps");
        var betaOverlaps = betaRow.GetProperty("potentialOverlaps");
        alphaOverlaps.GetArrayLength().ShouldBe(1);
        alphaOverlaps[0].GetProperty("withFeature").GetString().ShouldBe("beta");
        betaOverlaps.GetArrayLength().ShouldBe(1);
        betaOverlaps[0].GetProperty("withFeature").GetString().ShouldBe("alpha");
        alphaOverlaps[0].GetProperty("sharedGlobs").EnumerateArray()
            .Select(v => v.GetString()).ShouldBe(betaOverlaps[0].GetProperty("sharedGlobs")
                .EnumerateArray().Select(v => v.GetString()));
    }

    [Fact]
    public async Task Update_EmptyArray_ClearsSurfacesAndOverlapsVanish()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project, "alpha", "Alpha");
        await CreateFeatureAsync(project, "beta", "Beta");
        await ClaimAsync(project, "alpha");
        await ClaimAsync(project, "beta");
        await PutCodeSurfacesAsync(project, "alpha", ["src/web/**"]);
        await PutCodeSurfacesAsync(project, "beta", ["src/web/**"]);

        await PutCodeSurfacesAsync(project, "beta", []);

        var beta = await GetFeatureAsync(project, "beta");
        beta.GetProperty("codeSurfaces").EnumerateArray().ShouldBeEmpty();
        beta.GetProperty("potentialOverlaps").GetArrayLength().ShouldBe(0);

        var queue = await GetQueueAsync(project);
        queue.Single(i => i.GetProperty("slug").GetString() == "beta")
            .GetProperty("potentialOverlaps").GetArrayLength().ShouldBe(0);
        queue.Single(i => i.GetProperty("slug").GetString() == "alpha")
            .GetProperty("potentialOverlaps").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Update_OmittedSurfaces_KeepPreviousOnes()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project, "alpha", "Alpha");
        await PutCodeSurfacesAsync(project, "alpha", ["src/web/**"]);

        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{project}/planning/features/alpha",
            new { Title = "Renamed" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var feature = await GetFeatureAsync(project, "alpha");
        feature.GetProperty("title").GetString().ShouldBe("Renamed");
        feature.GetProperty("codeSurfaces").EnumerateArray()
            .Select(v => v.GetString()).ShouldBe(["src/web/**"]);
    }

    [Fact]
    public async Task WaveSnapshot_QueueRowsCarryPotentialOverlaps()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project, "alpha", "Alpha");
        await CreateFeatureAsync(project, "beta", "Beta");
        await ClaimAsync(project, "alpha");
        await ClaimAsync(project, "beta");
        await PutCodeSurfacesAsync(project, "alpha", ["src/web/**"]);
        await PutCodeSurfacesAsync(project, "beta", ["src/web/**"]);

        var snapshot = await GetJsonAsync(
            $"/api/agent/projects/{project}/planning/wave-snapshot");

        var queue = snapshot.GetProperty("queue").EnumerateArray().ToArray();
        var alphaRow = queue.Single(i => i.GetProperty("slug").GetString() == "alpha");
        var overlaps = alphaRow.GetProperty("potentialOverlaps");
        overlaps.GetArrayLength().ShouldBe(1);
        overlaps[0].GetProperty("withFeature").GetString().ShouldBe("beta");
    }

    private async Task<JsonElement> GetFeatureAsync(Guid projectId, string slug)
    {
        var feature = await GetJsonAsync($"/api/projects/{projectId}/planning/features/{slug}");
        feature.GetProperty("slug").GetString().ShouldBe(slug);
        return feature;
    }

    private async Task<JsonElement[]> GetQueueAsync(Guid projectId)
    {
        var queue = await GetJsonAsync($"/api/agent/projects/{projectId}/planning/task-queue");
        return [.. queue.GetProperty("items").EnumerateArray()];
    }

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return payload;
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"surfaces-proj-{Guid.NewGuid():N}",
            DiskPath = _projectDir,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!.Id;
    }

    private async Task CreateFeatureAsync(Guid projectId, string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task ClaimAsync(Guid projectId, string slug)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}/claim",
            new { Alias = $"coder-{slug}", Vendor = "test" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> PutCodeSurfacesAsync(
        Guid projectId, string slug, string[] codeSurfaces) =>
        await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}",
            new { CodeSurfaces = codeSurfaces });

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record ValidationProblemJson(Dictionary<string, string[]> Errors);
}
