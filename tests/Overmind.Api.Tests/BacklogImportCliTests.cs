using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Host;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class BacklogImportCliTests : IDisposable
{
    private static readonly DateTime FeatureMtime = new(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly string _repoRoot;
    private readonly string _dbPath;

    public BacklogImportCliTests()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"overmind-import-cli-{Guid.NewGuid():N}");
        var backlogDir = Path.Combine(_repoRoot, "BACKLOG");
        Directory.CreateDirectory(Path.Combine(backlogDir, "010-demo"));
        File.WriteAllText(
            Path.Combine(backlogDir, "010-demo", "feature.md"),
            """
            ---
            feature: 010
            slug: demo
            status: ready
            version: 1
            qa-owner: alice
            ---

            # Feature 010 — Demo

            Demo body.
            """);
        File.SetLastWriteTimeUtc(Path.Combine(backlogDir, "010-demo", "feature.md"), FeatureMtime);
        File.WriteAllText(Path.Combine(backlogDir, "README.md"), "stray top-level file");
        (_dbPath, _) = SeedDatabase(_repoRoot);
    }

    [Fact]
    public async Task Run_PrintsFullReportAndReturnsZero()
    {
        using var provider = BacklogImportCli.BuildProvider(_dbPath);
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await BacklogImportCli.ExecuteAsync(
            ["--import-backlog", _repoRoot], provider, output, error, CancellationToken.None);

        code.ShouldBe(0);
        error.ToString().ShouldBeEmpty();
        var expectedColor = FrontmatterKeyPalette.ColorFor("qa-owner");
        output.ToString().ShouldBe(
            string.Join(
                Environment.NewLine,
                $"Imported planning from {_repoRoot}: 1 features (1 new), 0 artifacts, 1 skipped",
                "ignored: BACKLOG/README.md",
                $"new key: qa-owner ({expectedColor})",
                "preserved: BACKLOG/010-demo/feature.md (qa-owner)",
                "created: BACKLOG/010-demo/feature.md (no created token; fell back to file mtime 2026-09-10)",
                "materialization is 'off'; set planning materialization to 'on' to write imported features to the repo")
            + Environment.NewLine);
    }

    [Fact]
    public async Task Run_WithReport_WritesStdoutIdenticalTextWithoutBom()
    {
        using var provider = BacklogImportCli.BuildProvider(_dbPath);
        var output = new StringWriter();
        var error = new StringWriter();
        var reportPath = Path.Combine(_repoRoot, "import-report.txt");

        var code = await BacklogImportCli.ExecuteAsync(
            ["--import-backlog", _repoRoot, "--report", reportPath], provider, output, error, CancellationToken.None);

        code.ShouldBe(0);
        var stdoutText = output.ToString();
        File.ReadAllText(reportPath).ShouldBe(stdoutText);
        var bytes = File.ReadAllBytes(reportPath);
        bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_WithReportInMissingDirectory_ReturnsUsageError()
    {
        using var provider = BacklogImportCli.BuildProvider(_dbPath);
        var output = new StringWriter();
        var error = new StringWriter();
        var reportPath = Path.Combine(_repoRoot, "no-such-dir", "report.txt");

        var code = await BacklogImportCli.ExecuteAsync(
            ["--import-backlog", _repoRoot, "--report", reportPath], provider, output, error, CancellationToken.None);

        code.ShouldBe(2);
        output.ToString().ShouldBeEmpty();
        error.ToString().ShouldContain("--report: directory for");
        File.Exists(reportPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_WithoutFlagValue_ReturnsUsageError()
    {
        using var provider = BacklogImportCli.BuildProvider(_dbPath);
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await BacklogImportCli.ExecuteAsync(["--import-backlog"], provider, output, error, CancellationToken.None);

        code.ShouldBe(2);
        error.ToString().ShouldContain("--import-backlog requires");
    }

    [Fact]
    public async Task Run_WithoutReportValue_ReturnsUsageError()
    {
        using var provider = BacklogImportCli.BuildProvider(_dbPath);
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await BacklogImportCli.ExecuteAsync(
            ["--import-backlog", _repoRoot, "--report"], provider, output, error, CancellationToken.None);

        code.ShouldBe(2);
        error.ToString().ShouldContain("--report requires");
    }

    [Fact]
    public async Task Run_UnknownBacklogPath_ReturnsFailure()
    {
        using var provider = BacklogImportCli.BuildProvider(_dbPath);
        var output = new StringWriter();
        var error = new StringWriter();
        var missing = Path.Combine(_repoRoot, "does-not-exist");

        var code = await BacklogImportCli.ExecuteAsync(
            ["--import-backlog", missing], provider, output, error, CancellationToken.None);

        code.ShouldBe(1);
        output.ToString().ShouldBeEmpty();
        error.ToString().ShouldContain("is neither a repository root containing a BACKLOG folder");
    }

    public void Dispose()
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var file = _dbPath + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        try
        {
            Directory.Delete(_repoRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static (string DbPath, Guid ProjectId) SeedDatabase(string repoRoot)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"overmind-import-cli-{Guid.NewGuid():N}.db");
        var provider = BacklogImportCli.BuildProvider(dbPath);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        db.Database.Migrate();
        var project = Project.Create($"import-{Guid.NewGuid():N}", repoRoot, string.Empty);
        db.Projects.Add(project);
        db.SaveChanges();
        provider.Dispose();
        return (dbPath, project.Id);
    }
}
