using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class OnboardApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OnboardApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Onboard_CwdMatchingDiskPath_ReturnsExistingProjectCreatedFalse()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var project = await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.ProjectId.ShouldBe(project.Id);
        onboard.ProjectName.ShouldBe(name);
        onboard.Created.ShouldBeFalse();
        onboard.Board.CountsByStatus.ShouldBeEmpty();
        onboard.Board.CurrentSlug.ShouldBeNull();
    }

    [Fact]
    public async Task Onboard_TrailingSlashCwd_MatchesExistingProject()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var project = await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) + "/" });

        onboard.ProjectId.ShouldBe(project.Id);
        onboard.Created.ShouldBeFalse();
    }

    [Fact]
    public async Task Onboard_NonNormalizedCwdWithDotSegment_MatchesExistingProject()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var project = await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) + "/./" });

        onboard.ProjectId.ShouldBe(project.Id);
        onboard.Created.ShouldBeFalse();
    }

    [Fact]
    public async Task Onboard_CwdThroughSymlink_MatchesResolvedDiskPath()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var realPath = Path.Combine(Path.GetTempPath(), $"overmind-onboard-real-{Guid.NewGuid():N}");
        var linkPath = Path.Combine(Path.GetTempPath(), $"overmind-onboard-link-{Guid.NewGuid():N}");
        Directory.CreateDirectory(realPath);
        Directory.CreateSymbolicLink(linkPath, realPath);

        try
        {
            var project = await CreateProjectAsync($"onboard-{Guid.NewGuid():N}", realPath);

            var onboard = await OnboardAsync(new { Cwd = linkPath });

            onboard.ProjectId.ShouldBe(project.Id);
            onboard.Created.ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(linkPath);
            Directory.Delete(realPath);
        }
    }

    [Fact]
    public async Task Onboard_UnknownCwd_CreatesProjectNamedAfterDirectory()
    {
        var name = $"onboard-{Guid.NewGuid():N}";

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.ProjectId.ShouldNotBe(Guid.Empty);
        onboard.ProjectName.ShouldBe(name);
        onboard.Created.ShouldBeTrue();
        onboard.Board.CountsByStatus.ShouldBeEmpty();
        onboard.Board.CurrentSlug.ShouldBeNull();
    }

    [Fact]
    public async Task Onboard_SecondCallWithSameCwd_ReturnsCreatedFalseWithSameProject()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var cwd = TestPaths.DiskPath(name);
        var first = await OnboardAsync(new { Cwd = cwd });

        var second = await OnboardAsync(new { Cwd = cwd });

        second.ProjectId.ShouldBe(first.ProjectId);
        second.ProjectName.ShouldBe(name);
        second.Created.ShouldBeFalse();
    }

    [Fact]
    public async Task Onboard_NameCollisionWithDifferentPath_AppendsNumericSuffix()
    {
        var existingPath = Path.Combine(TestPaths.DiskPath($"parent-{Guid.NewGuid():N}"), "dup");
        await CreateProjectAsync("dup", existingPath);

        var onboard = await OnboardAsync(new
        {
            Cwd = Path.Combine(TestPaths.DiskPath($"parent-{Guid.NewGuid():N}"), "dup"),
        });

        onboard.Created.ShouldBeTrue();
        onboard.ProjectName.ShouldBe("dup-2");
    }

    [Fact]
    public async Task Onboard_RepeatedNameCollisions_AppendIncrementingSuffix()
    {
        await CreateProjectAsync("dup", Path.Combine(TestPaths.DiskPath($"parent-{Guid.NewGuid():N}"), "dup"));
        await CreateProjectAsync("dup-2", Path.Combine(TestPaths.DiskPath($"parent-{Guid.NewGuid():N}"), "dup"));

        var onboard = await OnboardAsync(new
        {
            Cwd = Path.Combine(TestPaths.DiskPath($"parent-{Guid.NewGuid():N}"), "dup"),
        });

        onboard.Created.ShouldBeTrue();
        onboard.ProjectName.ShouldBe("dup-3");
    }

    [Fact]
    public async Task Onboard_ExplicitProjectId_OverridesCwdMatch()
    {
        var firstName = $"onboard-{Guid.NewGuid():N}";
        var first = await CreateProjectAsync(firstName, TestPaths.DiskPath(firstName));
        var secondName = $"onboard-{Guid.NewGuid():N}";
        await CreateProjectAsync(secondName, TestPaths.DiskPath(secondName));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(secondName), ProjectId = first.Id });

        onboard.ProjectId.ShouldBe(first.Id);
        onboard.ProjectName.ShouldBe(firstName);
        onboard.Created.ShouldBeFalse();
    }

    [Fact]
    public async Task Onboard_ExplicitProjectIdForUnknownGuid_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/onboard",
            new { Cwd = TestPaths.DiskPath($"onboard-{Guid.NewGuid():N}"), ProjectId = Guid.NewGuid() });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Status.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Onboard_BoardSummary_CountsByStatusAndPinnedCurrentSlug()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var project = await CreateProjectAsync(name, TestPaths.DiskPath(name));
        await CreateFeatureAsync(project.Id, "feat-a", "Feature A");
        await CreateFeatureAsync(project.Id, "feat-b", "Feature B");
        await UpdateFeatureStatusAsync(project.Id, "feat-b", "InProgress");
        await PinFeatureAsync(project.Id, "feat-b");

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.Board.CurrentSlug.ShouldBe("feat-b");
        onboard.Board.CountsByStatus.Count.ShouldBe(2);
        onboard.Board.CountsByStatus["draft"].ShouldBe(1);
        onboard.Board.CountsByStatus["inProgress"].ShouldBe(1);
    }

    [Fact]
    public async Task Onboard_Quickstart_MentionsToolOrderAndEscapeHatch()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.Quickstart.ShouldContain("onboard");
        onboard.Quickstart.ShouldContain("list_features");
        onboard.Quickstart.ShouldContain("current_task");
        onboard.Quickstart.ShouldContain("upsert_artifact");
        onboard.Quickstart.ShouldContain("set_status");
        onboard.Quickstart.ShouldContain("OVERMIND_PROJECT_ID");
        onboard.Quickstart.IndexOf("list_features", StringComparison.Ordinal)
            .ShouldBeLessThan(onboard.Quickstart.IndexOf("current_task", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Onboard_EmptyCwdWithoutProjectId_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/onboard", new { Cwd = string.Empty });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<OnboardJson> OnboardAsync(object request)
    {
        var response = await _client.PostAsJsonAsync("/api/onboard", request);
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

    private async Task CreateFeatureAsync(Guid projectId, string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = string.Empty });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task UpdateFeatureStatusAsync(Guid projectId, string slug, string status)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}",
            new { Status = status });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task PinFeatureAsync(Guid projectId, string slug)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}/current",
            new { IsCurrent = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record OnboardJson(
        Guid ProjectId, string ProjectName, bool Created, OnboardBoardJson Board, string Quickstart);

    public sealed record OnboardBoardJson(Dictionary<string, int> CountsByStatus, string? CurrentSlug);

    public sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
