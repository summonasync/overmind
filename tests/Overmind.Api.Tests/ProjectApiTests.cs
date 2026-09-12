using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class ProjectApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ProjectApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Project_Lifecycle_Works()
    {
        var created = await CreateProjectAsync("alpha", TestPaths.DiskPath("alpha"));
        created.Id.ShouldNotBe(Guid.Empty);

        var listed = await _client.GetFromJsonAsync<List<ProjectJson>>("/api/projects", JsonOptions);
        listed.ShouldNotBeNull();
        listed!.Count.ShouldBe(1);
        listed[0].Name.ShouldBe("alpha");
        listed[0].DiskPath.ShouldBe(TestPaths.DiskPath("alpha"));

        var fetched = await _client.GetFromJsonAsync<ProjectJson>($"/api/projects/{created.Id}", JsonOptions);
        fetched.ShouldNotBeNull();
        fetched!.Description.ShouldBe("first project");

        var update = new { Name = "alpha-renamed", DiskPath = TestPaths.DiskPath("alpha2"), Description = "renamed" };
        var putResponse = await _client.PutAsJsonAsync($"/api/projects/{created.Id}", update);
        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await putResponse.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        updated.ShouldNotBeNull();
        updated!.Name.ShouldBe("alpha-renamed");

        var deleteResponse = await _client.DeleteAsync($"/api/projects/{created.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var missing = await _client.GetAsync($"/api/projects/{created.Id}");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await missing.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Status.ShouldBe(404);
    }

    [Fact]
    public async Task Create_DuplicateName_Returns409()
    {
        await CreateProjectAsync("dup", TestPaths.DiskPath("dup"));

        var conflict = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = "dup",
            DiskPath = TestPaths.DiskPath("other"),
            Description = (string?)null,
        });

        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_EmptyName_Returns400WithValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = "",
            DiskPath = TestPaths.DiskPath("x"),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Status.ShouldBe(400);
        problem.Errors.ShouldNotBeNull();
        problem.Errors!.ContainsKey("Name").ShouldBeTrue();
    }

    [Fact]
    public async Task Create_RelativeDiskPath_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = "rel",
            DiskPath = "some/relative/path",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("DiskPath");
    }

    [Fact]
    public async Task Update_MissingProject_Returns404()
    {
        var response = await _client.PutAsJsonAsync($"/api/projects/{Guid.NewGuid()}", new
        {
            Name = "ghost",
            DiskPath = TestPaths.DiskPath("ghost"),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<ProjectJson> CreateProjectAsync(string name, string diskPath)
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = diskPath,
            Description = "first project",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    public sealed record ProjectJson(
        Guid Id,
        string Name,
        string DiskPath,
        string Description,
        DateTime CreatedAt);

    public sealed record ProblemJson(
        string? Title,
        int? Status,
        string? Detail,
        Dictionary<string, string[]>? Errors);
}
