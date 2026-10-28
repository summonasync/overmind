using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Overmind.Host;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class OnboardWorktreeGuardTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandbox = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "overmind-test", $"worktree-guard-{Guid.NewGuid():N}");

    public OnboardWorktreeGuardTests()
    {
        _client = _factory.CreateClient();
        Directory.CreateDirectory(_sandbox);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        try
        {
            Directory.Delete(_sandbox, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Onboard_WorktreeOfRegisteredMain_RefusesCreateAndNamesTheMatch()
    {
        var main = Path.Combine(_sandbox, "main");
        var name = $"guard-{Guid.NewGuid():N}";
        await CreateProjectAsync(name, main);
        var worktree = CreateWorktree($"{main}-wt", main);

        var response = await _client.PostAsJsonAsync("/api/onboard", new { Cwd = worktree });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain(name);
        problem.Detail.ShouldContain(main);
        problem.Detail.ShouldContain(Path.Combine(worktree, ".overmind", "project"));
        problem.Detail.ShouldContain("OVERMIND_PROJECT_ID");
        problem.Detail.ShouldContain("projectId");
        (await ListProjectsAsync()).Length.ShouldBe(1);
    }

    [Fact]
    public async Task Onboard_DeepCwdInsideWorktree_StillRefusesCreate()
    {
        var main = Path.Combine(_sandbox, "deep-main");
        await CreateProjectAsync($"guard-{Guid.NewGuid():N}", main);
        var worktree = CreateWorktree($"{main}-wt", main);
        var deep = Path.Combine(worktree, "src", "deep");
        Directory.CreateDirectory(deep);

        var response = await _client.PostAsJsonAsync("/api/onboard", new { Cwd = deep });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Onboard_WorktreeOfUnregisteredMain_CreatesProject()
    {
        var main = Path.Combine(_sandbox, "orphan-main");
        var worktree = CreateWorktree($"{main}-wt", main);

        var onboard = await OnboardAsync(new { Cwd = worktree });

        onboard.Created.ShouldBeTrue();
        onboard.ProjectName.ShouldBe(Path.GetFileName(worktree));
    }

    [Fact]
    public async Task Onboard_MainCheckoutWithGitDirectory_CreatesProject()
    {
        var checkout = Path.Combine(_sandbox, "plain-checkout");
        Directory.CreateDirectory(Path.Combine(checkout, ".git"));

        var onboard = await OnboardAsync(new { Cwd = checkout });

        onboard.Created.ShouldBeTrue();
        onboard.ProjectName.ShouldBe(Path.GetFileName(checkout));
    }

    [Fact]
    public async Task Onboard_GitFileWithoutWorktreesPointer_CreatesProject()
    {
        var submodule = Path.Combine(_sandbox, "sub");
        Directory.CreateDirectory(submodule);
        File.WriteAllText(Path.Combine(submodule, ".git"), "gitdir: ../.git/modules/sub\n");

        var onboard = await OnboardAsync(new { Cwd = submodule });

        onboard.Created.ShouldBeTrue();
        onboard.ProjectName.ShouldBe("sub");
    }

    private string CreateWorktree(string worktreePath, string main)
    {
        Directory.CreateDirectory(worktreePath);
        File.WriteAllText(Path.Combine(worktreePath, ".git"), $"gitdir: {main}/.git/worktrees/{Path.GetFileName(worktreePath)}\n");
        return worktreePath;
    }

    private async Task<OnboardJson> OnboardAsync(object request)
    {
        var response = await _client.PostAsJsonAsync("/api/onboard", request, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var onboard = await response.Content.ReadFromJsonAsync<OnboardJson>(JsonOptions);
        onboard.ShouldNotBeNull();
        return onboard!;
    }

    private async Task<ProjectJson> CreateProjectAsync(string name, string diskPath)
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = diskPath,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task<ProjectJson[]> ListProjectsAsync() =>
        await _client.GetFromJsonAsync<ProjectJson[]>("/api/projects", JsonOptions)
            ?? throw new InvalidOperationException("project list was null");

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record OnboardJson(Guid ProjectId, string ProjectName, bool Created);

    public sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
