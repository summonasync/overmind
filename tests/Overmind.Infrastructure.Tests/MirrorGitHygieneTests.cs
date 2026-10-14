using System.ComponentModel;
using System.Diagnostics;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class MirrorGitHygieneTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly string _sandboxDir;

    public MirrorGitHygieneTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-git-hygiene-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sandboxDir);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_sandboxDir))
        {
            Directory.Delete(_sandboxDir, recursive: true);
        }
    }

    [Fact]
    public async Task Ensure_NoEnclosingRepository_ReportsNoRepoAndWritesNothing()
    {
        var projectDir = CreateDirectory("project");

        var outcome = await MirrorGitHygiene.EnsureAsync(projectDir, CancellationToken.None);

        outcome.Status.ShouldBe("no-repo");
        outcome.RepositoryRoot.ShouldBeNull();
        outcome.Detail.ShouldBeNull();
        Directory.Exists(Path.Combine(projectDir, ".git")).ShouldBeFalse();
    }

    [Fact]
    public async Task Ensure_ProjectAtRepositoryRoot_EnsuresPlainPatterns()
    {
        var repoRoot = await InitRepositoryAsync("repo");

        var outcome = await MirrorGitHygiene.EnsureAsync(repoRoot, CancellationToken.None);

        outcome.Status.ShouldBe("ensured");
        outcome.RepositoryRoot.ShouldBe(repoRoot);
        var lines = await File.ReadAllLinesAsync(Path.Combine(repoRoot, ".git", "info", "exclude"));
        lines.ShouldContain("BACKLOG/");
        lines.ShouldContain("ENTRIES/");
    }

    [Fact]
    public async Task Ensure_NestedProject_UsesRepoRootRelativePatterns()
    {
        var repoRoot = await InitRepositoryAsync("repo");
        var projectDir = CreateDirectory(Path.Combine("repo", "apps", "foo"));

        var outcome = await MirrorGitHygiene.EnsureAsync(projectDir, CancellationToken.None);

        outcome.Status.ShouldBe("ensured");
        outcome.RepositoryRoot.ShouldBe(repoRoot);
        var lines = await File.ReadAllLinesAsync(Path.Combine(repoRoot, ".git", "info", "exclude"));
        lines.ShouldContain("apps/foo/BACKLOG/");
        lines.ShouldContain("apps/foo/ENTRIES/");
        lines.ShouldNotContain("BACKLOG/");
    }

    [Fact]
    public async Task Ensure_MissingExcludeFile_CreatesIt()
    {
        var repoRoot = await InitRepositoryAsync("repo");
        File.Delete(Path.Combine(repoRoot, ".git", "info", "exclude"));

        var outcome = await MirrorGitHygiene.EnsureAsync(repoRoot, CancellationToken.None);

        outcome.Status.ShouldBe("ensured");
        var lines = await File.ReadAllLinesAsync(Path.Combine(repoRoot, ".git", "info", "exclude"));
        lines.ShouldContain("BACKLOG/");
        lines.ShouldContain("ENTRIES/");
    }

    [Fact]
    public async Task Ensure_SecondRun_AddsNothing()
    {
        var repoRoot = await InitRepositoryAsync("repo");
        var excludePath = Path.Combine(repoRoot, ".git", "info", "exclude");
        await MirrorGitHygiene.EnsureAsync(repoRoot, CancellationToken.None);
        var afterFirst = await File.ReadAllBytesAsync(excludePath);

        var outcome = await MirrorGitHygiene.EnsureAsync(repoRoot, CancellationToken.None);

        outcome.Status.ShouldBe("already-present");
        (await File.ReadAllBytesAsync(excludePath)).ShouldBe(afterFirst);
    }

    [Fact]
    public async Task Ensure_UserLinesSurviveAndPresentPatternIsNotDuplicated()
    {
        var repoRoot = await InitRepositoryAsync("repo");
        await File.WriteAllTextAsync(
            Path.Combine(repoRoot, ".git", "info", "exclude"), "node_modules/\nENTRIES/\n");

        var outcome = await MirrorGitHygiene.EnsureAsync(repoRoot, CancellationToken.None);

        outcome.Status.ShouldBe("ensured");
        var content = await File.ReadAllTextAsync(Path.Combine(repoRoot, ".git", "info", "exclude"));
        content.ShouldStartWith("node_modules/\nENTRIES/\n");
        content.ShouldContain("BACKLOG/");
        content.Split('\n').Count(line => line.Trim() == "ENTRIES/").ShouldBe(1);
    }

    [Fact]
    public async Task Ensure_UnwritableExclude_ReportsFailedOutcome()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var repoRoot = await InitRepositoryAsync("repo");
        var excludePath = Path.Combine(repoRoot, ".git", "info", "exclude");
        if (!File.Exists(excludePath))
        {
            await File.WriteAllTextAsync(excludePath, string.Empty);
        }

        File.SetUnixFileMode(excludePath, UnixFileMode.UserRead);

        try
        {
            var outcome = await MirrorGitHygiene.EnsureAsync(repoRoot, CancellationToken.None);

            outcome.Status.ShouldBe("failed");
            outcome.Detail.ShouldNotBeNull();
        }
        finally
        {
            File.SetUnixFileMode(excludePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public async Task Sync_FreshProjectInRepository_MirrorStaysOutOfGitStatus()
    {
        if (!await IsGitAvailableAsync())
        {
            return;
        }

        var repoRoot = await InitRepositoryAsync("repo");
        var dataDir = CreateDirectory("data");
        var projectId = await SeedProjectAsync(repoRoot);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = new PlanningMaterializer(
            ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx)), dataDir);

        var report = await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        report.GitHygiene.ShouldNotBeNull();
        report.GitHygiene!.Status.ShouldBe("ensured");
        report.GitHygiene.RepositoryRoot.ShouldBe(repoRoot);
        File.Exists(Path.Combine(repoRoot, "BACKLOG", "006-demo", "feature.md")).ShouldBeTrue();
        (await RunGitAsync(repoRoot, "status --porcelain")).ShouldBeEmpty();

        var rerun = await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        rerun.GitHygiene!.Status.ShouldBe("already-present");
        (await RunGitAsync(repoRoot, "status --porcelain")).ShouldBeEmpty();

        await File.WriteAllTextAsync(Path.Combine(repoRoot, "notes.txt"), "untracked\n");
        var porcelain = await RunGitAsync(repoRoot, "status --porcelain");
        porcelain.ShouldContain("notes.txt");
        porcelain.ShouldNotContain("BACKLOG");
        porcelain.ShouldNotContain("ENTRIES");
    }

    [Fact]
    public async Task Sync_ProjectOutsideAnyRepository_ReportsNoRepoHygieneAndStillMaterializes()
    {
        var projectDir = CreateDirectory("project");
        var dataDir = CreateDirectory("data");
        var projectId = await SeedProjectAsync(projectDir);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = new PlanningMaterializer(
            ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx)), dataDir);

        var report = await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        report.GitHygiene.ShouldNotBeNull();
        report.GitHygiene!.Status.ShouldBe("no-repo");
        File.Exists(Path.Combine(projectDir, "BACKLOG", "006-demo", "feature.md")).ShouldBeTrue();
    }

    private string CreateDirectory(string relative)
    {
        var path = Path.Combine(_sandboxDir, relative);
        Directory.CreateDirectory(path);
        return path;
    }

    private async Task<string> InitRepositoryAsync(string relative)
    {
        var root = CreateDirectory(relative);
        await RunGitAsync(root, "init");
        return root;
    }

    private static async Task<string> RunGitAsync(string workingDirectory, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        process.ExitCode.ShouldBe(0, $"git {arguments} failed: {error}");
        return output;
    }

    private static async Task<bool> IsGitAvailableAsync()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            })!;
            await process.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    private async Task<Guid> SeedProjectAsync(string projectDir)
    {
        using var ctx = _db.CreateContext();
        var project = Project.Create("demo", projectDir);
        project.SetPlanningWorkflow(PlanningMaterialization.On, HumanValidationOverride.Unset);
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();
        return project.Id;
    }

    private async Task SeedDefaultFeatureAsync(Guid projectId)
    {
        using var ctx = _db.CreateContext();
        var feature = PlanningFeature.Create(projectId, "006-demo", "Demo Feature", "<goal>\nShip it.\n</goal>");
        feature.SetStatus(PlanningFeatureStatus.Ready);
        ctx.PlanningFeatures.Add(feature);
        await ctx.SaveChangesAsync();
    }
}
