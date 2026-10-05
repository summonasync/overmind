using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class ProjectManifestApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _diskPath = Path.Combine(Path.GetTempPath(), "overmind-manifest-api", Guid.NewGuid().ToString("N"));

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ProjectManifestApiTests()
    {
        Directory.CreateDirectory(_diskPath);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        Directory.Delete(_diskPath, recursive: true);
    }

    [Fact]
    public async Task Update_WithSetManifest_RoundTrips()
    {
        var project = await CreateProjectAsync("manifest-roundtrip");

        var update = new
        {
            Name = "manifest-roundtrip",
            DiskPath = _diskPath,
            Description = "still here",
            Manifest = "# Alpha\n\nBuild: dotnet build.",
            SetManifest = true,
        };
        var putResponse = await _client.PutAsJsonAsync($"/api/projects/{project.Id}", update, JsonOptions);
        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await putResponse.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        updated!.Manifest.ShouldBe("# Alpha\n\nBuild: dotnet build.");

        var fetched = await _client.GetFromJsonAsync<ProjectJson>($"/api/projects/{project.Id}", JsonOptions);
        fetched!.Manifest.ShouldBe("# Alpha\n\nBuild: dotnet build.");
    }

    [Fact]
    public async Task Update_WithoutSetManifest_PreservesExistingManifest()
    {
        var project = await CreateProjectAsync("manifest-keep");
        await PutManifestAsync(project.Id, "# keep me");

        var update = new { Name = "manifest-keep-renamed", DiskPath = _diskPath, Description = "touched" };
        var putResponse = await _client.PutAsJsonAsync($"/api/projects/{project.Id}", update, JsonOptions);
        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await putResponse.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        updated!.Manifest.ShouldBe("# keep me");
        updated.Name.ShouldBe("manifest-keep-renamed");
    }

    [Fact]
    public async Task Update_WithSetManifestAndNull_ClearsManifest()
    {
        var project = await CreateProjectAsync("manifest-clear");
        await PutManifestAsync(project.Id, "# gone soon");

        var update = new
        {
            Name = "manifest-clear",
            DiskPath = _diskPath,
            Description = "",
            Manifest = (string?)null,
            SetManifest = true,
        };
        var putResponse = await _client.PutAsJsonAsync($"/api/projects/{project.Id}", update, JsonOptions);
        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await putResponse.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        updated!.Manifest.ShouldBeNull();
    }

    [Fact]
    public async Task Import_PicksFirstFoundInPrecedenceOrder()
    {
        var project = await CreateProjectAsync("manifest-import");
        await File.WriteAllTextAsync(Path.Combine(_diskPath, "AGENTS.md"), "# agents body\n");
        await File.WriteAllTextAsync(Path.Combine(_diskPath, "CLAUDE.md"), "# claude body\n");
        await File.WriteAllTextAsync(Path.Combine(_diskPath, "QWEN.md"), "# qwen body\n");

        var first = await ImportAsync(project.Id);
        first.SourceFile.ShouldBe("AGENTS.md");
        first.Manifest.ShouldBe("<!-- imported from AGENTS.md -->\n\n# agents body");

        File.Delete(Path.Combine(_diskPath, "AGENTS.md"));
        var second = await ImportAsync(project.Id);
        second.SourceFile.ShouldBe("CLAUDE.md");
        second.Manifest.ShouldBe("<!-- imported from CLAUDE.md -->\n\n# claude body");

        File.Delete(Path.Combine(_diskPath, "CLAUDE.md"));
        var third = await ImportAsync(project.Id);
        third.SourceFile.ShouldBe("QWEN.md");
        third.Manifest.ShouldBe("<!-- imported from QWEN.md -->\n\n# qwen body");

        var fetched = await _client.GetFromJsonAsync<ProjectJson>($"/api/projects/{project.Id}", JsonOptions);
        fetched!.Manifest.ShouldBe(third.Manifest);
    }

    [Fact]
    public async Task Import_NoCandidateFile_Returns404()
    {
        var project = await CreateProjectAsync("manifest-empty");

        var response = await _client.PostAsync($"/api/projects/{project.Id}/manifest/import", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Import_RelativeDiskPath_Returns400()
    {
        var project = await CreateProjectAsync("manifest-relative");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            var row = db.Projects.Single();
            db.Entry(row).Property(p => p.DiskPath).CurrentValue = "relative/path";
            db.SaveChanges();
        }

        var response = await _client.PostAsync($"/api/projects/{project.Id}/manifest/import", null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("not absolute");
    }

    [Fact]
    public async Task Import_OversizedSource_Returns400()
    {
        var project = await CreateProjectAsync("manifest-oversized");
        await File.WriteAllTextAsync(
            Path.Combine(_diskPath, "AGENTS.md"),
            new string('x', Overmind.Core.Project.MaxManifestLength + 1));

        var response = await _client.PostAsync($"/api/projects/{project.Id}/manifest/import", null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("character limit");
    }

    private async Task<ProjectJson> CreateProjectAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = _diskPath,
            Description = "manifest test project",
        }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions))!;
    }

    private async Task PutManifestAsync(Guid id, string manifest)
    {
        var update = new { Name = "manifest", DiskPath = _diskPath, Description = "", Manifest = manifest, SetManifest = true };
        var response = await _client.PutAsJsonAsync($"/api/projects/{id}", update, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<ImportJson> ImportAsync(Guid id)
    {
        var response = await _client.PostAsync($"/api/projects/{id}/manifest/import", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ImportJson>(JsonOptions))!;
    }

    public sealed record ProjectJson(
        Guid Id,
        string Name,
        string DiskPath,
        string Description,
        string? Manifest,
        DateTime CreatedAt);

    public sealed record ImportJson(string SourceFile, string Manifest);
}
