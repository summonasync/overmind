using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class KitImportApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandbox;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public KitImportApiTests()
    {
        _client = _factory.CreateClient();
        _sandbox = Path.Combine(Path.GetTempPath(), $"overmind-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sandbox);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        if (Directory.Exists(_sandbox))
        {
            Directory.Delete(_sandbox, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_ContainerFolder_PreviewCarriesCandidatesAndFlags()
    {
        var alpha = KitDir("alpha");
        WriteSkill(alpha, "deploy");
        WriteFile(Path.Combine(alpha, "gates", "review.md"), "# Review Gate\n\nAlways review.\n");

        var broken = KitDir("broken");
        WriteSkill(broken, "wrong-name", SkillYaml("something-else"));

        var collisionKit = await CreateKitAsync("ALPHA");

        var preview = await PostReadAsync<ScanPreviewJson>("/api/kits/import/scan", new { Path = _sandbox });

        preview.ShouldNotBeNull();
        preview!.ScannedPath.ShouldBe(Path.GetFullPath(_sandbox));
        preview.Truncated.ShouldBeFalse();
        preview.Candidates.Count.ShouldBe(2);

        var alphaJson = preview.Candidates.Single(c => c.Name == "alpha");
        alphaJson.RelativePath.ShouldBe("alpha");
        alphaJson.NameCollision.ShouldBeTrue();
        alphaJson.NameCollisionKitId.ShouldBe(collisionKit.Id);
        alphaJson.ReImportMatchKitId.ShouldBeNull();

        var skill = alphaJson.Skills.ShouldHaveSingleItem();
        skill.Name.ShouldBe("deploy");
        skill.Platform.ShouldBeNull();
        skill.FileCount.ShouldBe(1);
        skill.RelativePath.ShouldBe(Combine("alpha", "deploy"));

        alphaJson.Gates.ShouldHaveSingleItem().Title.ShouldBe("Review Gate");
        alphaJson.Guards.ShouldBeEmpty();
        alphaJson.Issues.ShouldBeEmpty();

        var brokenJson = preview.Candidates.Single(c => c.Name == "broken");
        brokenJson.NameCollision.ShouldBeFalse();
        brokenJson.Issues.ShouldHaveSingleItem().Code.ShouldBe("invalidName");
    }

    [Fact]
    public async Task Scan_MissingPath_Returns400PathMissing()
    {
        var missing = Path.Combine(_sandbox, "nope");
        var response = await _client.PostAsJsonAsync("/api/kits/import/scan", new { Path = missing });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("PathMissing");
        problem!.Detail!.ShouldContain("does not exist");
    }

    [Fact]
    public async Task Scan_FileInsteadOfDirectory_Returns400NotADirectory()
    {
        var file = Path.Combine(_sandbox, "plain.txt");
        await File.WriteAllTextAsync(file, "not a folder");
        var response = await _client.PostAsJsonAsync("/api/kits/import/scan", new { Path = file });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("NotADirectory");
    }

    [Fact]
    public async Task Scan_EmptyPath_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/kits/import/scan", new { Path = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.TryGetProperty("errors", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Scan_RootAndFolderCandidates_CarryRelativePaths()
    {
        WriteSkill(_sandbox, "deploy");
        var alpha = KitDir("alpha");
        WriteSkill(alpha, "lint");

        var preview = await PostReadAsync<ScanPreviewJson>("/api/kits/import/scan", new { Path = _sandbox });

        preview.ShouldNotBeNull();
        preview!.Candidates.Count.ShouldBe(2);

        var root = preview.Candidates.Single(c => c.RelativePath.Length == 0);
        root.Name.ShouldBe(new DirectoryInfo(_sandbox).Name);
        root.Path.ShouldBe(Path.GetFullPath(_sandbox));
        root.Skills.ShouldHaveSingleItem().Name.ShouldBe("deploy");

        var folder = preview.Candidates.Single(c => c.RelativePath == "alpha");
        folder.Name.ShouldBe("alpha");
        folder.Path.ShouldBe(Path.Combine(Path.GetFullPath(_sandbox), "alpha"));
    }

    [Fact]
    public async Task Import_CommitsKitWithOriginAndCreatesLibraryProject()
    {
        var sample = KitDir("sample");
        WriteSkill(sample, "deploy");
        WriteFile(Path.Combine(sample, "guards", "privacy.md"), "# Privacy Guard\n\nNever leak secrets.\n");

        var report = await PostReadAsync<ImportReportJson>(
            "/api/kits/import",
            new { Path = _sandbox, Kits = new[] { new { RelativePath = "sample", Name = "sample" } } });

        report.ShouldNotBeNull();
        var kitResult = report!.Kits.ShouldHaveSingleItem();
        kitResult.Success.ShouldBeTrue();
        kitResult.Name.ShouldBe("sample");
        kitResult.KitId.ShouldNotBeNull();
        kitResult.FailureKind.ShouldBeNull();
        kitResult.Error.ShouldBeNull();

        var kit = await _client.GetFromJsonAsync<KitJson>($"/api/kits/{kitResult.KitId}", JsonOptions);
        kit.ShouldNotBeNull();
        kit!.SourceKind.ShouldBe("imported");
        kit.SourcePath.ShouldBe(Path.Combine(Path.GetFullPath(_sandbox), "sample"));
        kit.ImportedAtUtc.ShouldNotBeNull();
        kit.EntryIds.Count.ShouldBe(2);

        var projects = await _client.GetFromJsonAsync<List<ProjectJson>>("/api/projects", JsonOptions);
        projects.ShouldNotBeNull();
        projects!.Select(p => p.Name).ShouldContain("Kit Library");

        var rescan = await PostReadAsync<ScanPreviewJson>("/api/kits/import/scan", new { Path = _sandbox });
        rescan.ShouldNotBeNull();
        rescan!.Candidates.ShouldHaveSingleItem().ReImportMatchKitId.ShouldBe(kitResult.KitId);
    }

    [Fact]
    public async Task Import_MixedSelections_ReturnsPerKitSuccessAndFailure()
    {
        await CreateKitAsync("taken");
        var alpha = KitDir("alpha");
        WriteSkill(alpha, "deploy");
        var beta = KitDir("beta");
        WriteSkill(beta, "lint");

        var report = await PostReadAsync<ImportReportJson>(
            "/api/kits/import",
            new
            {
                Path = _sandbox,
                Kits = new[]
                {
                    new { RelativePath = "alpha", Name = "taken" },
                    new { RelativePath = "beta", Name = "fresh-kit" },
                },
            });

        report.ShouldNotBeNull();
        report!.Kits.Count.ShouldBe(2);

        var failed = report.Kits.Single(k => k.Name == "taken");
        failed.Success.ShouldBeFalse();
        failed.FailureKind.ShouldBe("duplicateName");
        failed.Error!.ShouldContain("already taken");
        failed.KitId.ShouldBeNull();

        var imported = report.Kits.Single(k => k.Name == "fresh-kit");
        imported.Success.ShouldBeTrue();
        imported.KitId.ShouldNotBeNull();

        var kits = await _client.GetFromJsonAsync<List<KitJson>>("/api/kits", JsonOptions);
        kits!.Count(k => k.Name == "taken").ShouldBe(1);
    }

    [Fact]
    public async Task Import_UnknownCandidate_FailsPerKit()
    {
        var response = await _client.PostAsJsonAsync("/api/kits/import", new
        {
            Path = _sandbox,
            Kits = new[] { new { RelativePath = "ghost", Name = "ghost-kit" } },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<ImportReportJson>(JsonOptions);
        var kit = report!.Kits.ShouldHaveSingleItem();
        kit.Success.ShouldBeFalse();
        kit.FailureKind.ShouldBe("candidateNotFound");
    }

    [Fact]
    public async Task Import_RootCandidate_EmptyRelativePath_ImportsRootFolder()
    {
        WriteSkill(_sandbox, "deploy");

        var report = await PostReadAsync<ImportReportJson>(
            "/api/kits/import",
            new { Path = _sandbox, Kits = new[] { new { RelativePath = "", Name = "root-kit" } } });

        report.ShouldNotBeNull();
        var kitResult = report!.Kits.ShouldHaveSingleItem();
        kitResult.Success.ShouldBeTrue();
        kitResult.KitId.ShouldNotBeNull();

        var kit = await _client.GetFromJsonAsync<KitJson>($"/api/kits/{kitResult.KitId}", JsonOptions);
        kit.ShouldNotBeNull();
        kit!.SourcePath.ShouldBe(Path.GetFullPath(_sandbox));
        kit.EntryIds.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Import_CandidateNameInsteadOfRelativePath_FailsCandidateNotFound()
    {
        WriteSkill(_sandbox, "deploy");

        var preview = await PostReadAsync<ScanPreviewJson>("/api/kits/import/scan", new { Path = _sandbox });
        var rootName = preview!.Candidates.ShouldHaveSingleItem().Name;

        var response = await _client.PostAsJsonAsync("/api/kits/import", new
        {
            Path = _sandbox,
            Kits = new[] { new { RelativePath = rootName, Name = "root-kit" } },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<ImportReportJson>(JsonOptions);
        var kit = report!.Kits.ShouldHaveSingleItem();
        kit.Success.ShouldBeFalse();
        kit.FailureKind.ShouldBe("candidateNotFound");
    }

    [Fact]
    public async Task Import_RootBasenameEqualsSubfolderName_ResolvesBothByRelativePath()
    {
        var scanRoot = KitDir("dupe");
        WriteSkill(scanRoot, "deploy");
        WriteSkill(Path.Combine(scanRoot, "dupe"), "lint");

        var preview = await PostReadAsync<ScanPreviewJson>("/api/kits/import/scan", new { Path = scanRoot });
        preview!.Candidates.Count(c => c.Name == "dupe").ShouldBe(2);

        var report = await PostReadAsync<ImportReportJson>(
            "/api/kits/import",
            new
            {
                Path = scanRoot,
                Kits = new[]
                {
                    new { RelativePath = "", Name = "root-dupe" },
                    new { RelativePath = "dupe", Name = "folder-dupe" },
                },
            });

        report.ShouldNotBeNull();
        report!.Kits.Count.ShouldBe(2);
        report.Kits.ShouldAllBe(k => k.Success);

        var rootKit = await _client.GetFromJsonAsync<KitJson>(
            $"/api/kits/{report.Kits.Single(k => k.Name == "root-dupe").KitId}", JsonOptions);
        rootKit!.SourcePath.ShouldBe(Path.GetFullPath(scanRoot));

        var folderKit = await _client.GetFromJsonAsync<KitJson>(
            $"/api/kits/{report.Kits.Single(k => k.Name == "folder-dupe").KitId}", JsonOptions);
        folderKit!.SourcePath.ShouldBe(Path.Combine(Path.GetFullPath(scanRoot), "dupe"));
    }

    [Fact]
    public async Task Import_CandidateAlreadyImported_FailsWithReImportRequired()
    {
        var alpha = KitDir("alpha");
        WriteSkill(alpha, "deploy");

        var first = await PostReadAsync<ImportReportJson>(
            "/api/kits/import",
            new { Path = _sandbox, Kits = new[] { new { RelativePath = "alpha", Name = "alpha" } } });

        var kitId = first!.Kits.ShouldHaveSingleItem().KitId!.Value;

        var second = await PostReadAsync<ImportReportJson>(
            "/api/kits/import",
            new { Path = _sandbox, Kits = new[] { new { RelativePath = "alpha", Name = "alpha-retry" } } });

        var retry = second!.Kits.ShouldHaveSingleItem();
        retry.Success.ShouldBeFalse();
        retry.FailureKind.ShouldBe("reImportRequired");
        retry.Error!.ShouldContain(kitId.ToString());
    }

    [Fact]
    public async Task Import_EmptyKitsOrBlankName_ReturnsValidationProblem()
    {
        var emptyKits = await _client.PostAsJsonAsync("/api/kits/import", new
        {
            Path = _sandbox,
            Kits = Array.Empty<object>(),
        });
        emptyKits.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var blankName = await _client.PostAsJsonAsync("/api/kits/import", new
        {
            Path = _sandbox,
            Kits = new[] { new { RelativePath = "alpha", Name = "   " } },
        });
        blankName.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await blankName.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.TryGetProperty("errors", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Reimport_DiffReportDistinguishesRowDeletedFromLinkRetained()
    {
        var sample = KitDir("sample");
        WriteSkill(sample, "deploy", SkillYaml("deploy", "First version."));
        WriteFile(Path.Combine(sample, "gates", "review.md"), "# Review Gate\n\nAlways review.\n");
        WriteFile(Path.Combine(sample, "gates", "checklist.md"), "# Checklist\n\nTick every box.\n");

        var import = await PostReadAsync<ImportReportJson>(
            "/api/kits/import",
            new { Path = _sandbox, Kits = new[] { new { RelativePath = "sample", Name = "sample" } } });
        var kitId = import!.Kits.ShouldHaveSingleItem().KitId!.Value;

        var libraryProject = (await _client.GetFromJsonAsync<List<ProjectJson>>("/api/projects", JsonOptions))!
            .Single(p => p.Name == "Kit Library");
        var gates = await _client.GetFromJsonAsync<List<EntryListJson>>(
            $"/api/projects/{libraryProject.Id}/entries?type=Gate", JsonOptions);
        var reviewGate = gates!.Single(e => e.Title == "Review Gate");
        var checklistGate = gates!.Single(e => e.Title == "Checklist");

        var holder = await CreateKitAsync("holder");
        await _client.PostAsync($"/api/kits/{holder.Id}/entries/{reviewGate.Id}", null);

        WriteSkill(sample, "deploy", SkillYaml("deploy", "Second version."));
        WriteSkill(sample, "lint");
        File.Delete(Path.Combine(sample, "gates", "review.md"));
        File.Delete(Path.Combine(sample, "gates", "checklist.md"));

        var reimportResponse = await _client.PostAsync($"/api/kits/{kitId}/reimport", null);
        reimportResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await reimportResponse.Content.ReadFromJsonAsync<ReimportReportJson>(JsonOptions);

        report.ShouldNotBeNull();
        report!.KitId.ShouldBe(kitId);
        report.AddedCount.ShouldBe(1);
        report.UpdatedCount.ShouldBe(1);
        report.RemovedCount.ShouldBe(2);
        report.KeptLocalCount.ShouldBe(0);
        report.ConflictedCount.ShouldBe(0);

        var removed = report.Entries.Where(e => e.Outcome == "removed").ToList();
        removed.Count.ShouldBe(2);
        removed.Single(e => e.EntryId == reviewGate.Id).EntryRowRetained.ShouldBeTrue();
        removed.Single(e => e.EntryId == checklistGate.Id).EntryRowRetained.ShouldBeFalse();

        var updated = report.Entries.Single(e => e.Outcome == "updated");
        updated.EntryType.ShouldBe("skill");
        updated.Title.ShouldBe("deploy");
        updated.ImportedContentHash.ShouldNotBeNull();

        var added = report.Entries.Single(e => e.Outcome == "added");
        added.Title.ShouldBe("lint");
        added.EntryId.ShouldNotBeNull();

        var retainedEntry = await _client.GetAsync($"/api/entries/{reviewGate.Id}");
        retainedEntry.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deletedEntry = await _client.GetAsync($"/api/entries/{checklistGate.Id}");
        deletedEntry.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var holderKit = await _client.GetFromJsonAsync<KitJson>($"/api/kits/{holder.Id}", JsonOptions);
        holderKit!.EntryIds.ShouldContain(reviewGate.Id);
    }

    [Fact]
    public async Task Reimport_ManualKit_Returns409NoSource()
    {
        var kit = await CreateKitAsync("manual-kit");

        var response = await _client.PostAsync($"/api/kits/{kit.Id}/reimport", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("NoSource");
        problem!.Detail!.ShouldContain("no import source");
    }

    [Fact]
    public async Task Reimport_UnknownKit_Returns404()
    {
        var response = await _client.PostAsync($"/api/kits/{Guid.NewGuid()}/reimport", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("Not Found");
    }

    [Fact]
    public async Task Reimport_DeletedSourceFolder_Returns409SourceMissing()
    {
        var source = Path.Combine(_sandbox, "volatile-src");
        Directory.CreateDirectory(source);
        WriteSkill(source, "deploy");
        var import = await PostReadAsync<ImportReportJson>(
            "/api/kits/import",
            new { Path = _sandbox, Kits = new[] { new { RelativePath = "volatile-src", Name = "volatile" } } });
        var kitId = import!.Kits.ShouldHaveSingleItem().KitId!.Value;

        Directory.Delete(source, recursive: true);
        var response = await _client.PostAsync($"/api/kits/{kitId}/reimport", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("SourceMissing");
    }

    [Fact]
    public async Task ClearSource_FlipsKitToManualAndKeepsEntries()
    {
        var sample = KitDir("sample");
        WriteSkill(sample, "deploy");

        var import = await PostReadAsync<ImportReportJson>(
            "/api/kits/import",
            new { Path = _sandbox, Kits = new[] { new { RelativePath = "sample", Name = "sample" } } });
        var kitId = import!.Kits.ShouldHaveSingleItem().KitId!.Value;

        var clearResponse = await _client.PostAsync($"/api/kits/{kitId}/clear-source", null);
        clearResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cleared = await clearResponse.Content.ReadFromJsonAsync<KitJson>(JsonOptions);

        cleared.ShouldNotBeNull();
        cleared!.SourceKind.ShouldBe("manual");
        cleared.SourcePath.ShouldBeNull();
        cleared.ImportedAtUtc.ShouldBeNull();
        cleared.EntryIds.ShouldNotBeEmpty();

        var reimport = await _client.PostAsync($"/api/kits/{kitId}/reimport", null);
        reimport.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ClearSource_UnknownKit_Returns404()
    {
        var response = await _client.PostAsync($"/api/kits/{Guid.NewGuid()}/clear-source", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reimport_BundledKit_Returns409AndKeepsOrigin()
    {
        var kitId = await SeedBundledKitAsync("bundled-kit");

        var response = await _client.PostAsync($"/api/kits/{kitId}/reimport", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("BundledReimportBlocked");
        problem!.Detail!.ShouldContain("bundled");

        var kit = await _client.GetFromJsonAsync<KitJson>($"/api/kits/{kitId}", JsonOptions);
        kit.ShouldNotBeNull();
        kit!.SourceKind.ShouldBe("bundled");
        kit.SourcePath.ShouldNotBeNull();
        kit.ImportedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task ClearSource_BundledKit_Returns409AndKeepsOrigin()
    {
        var kitId = await SeedBundledKitAsync("bundled-kit");

        var response = await _client.PostAsync($"/api/kits/{kitId}/clear-source", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("BundledSourceProtected");
        problem!.Detail!.ShouldContain("bundled");

        var kit = await _client.GetFromJsonAsync<KitJson>($"/api/kits/{kitId}", JsonOptions);
        kit.ShouldNotBeNull();
        kit!.SourceKind.ShouldBe("bundled");
        kit.SourcePath.ShouldNotBeNull();
        kit.ImportedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Delete_ProjectOwningKitLinkedEntries_Returns409ThenSucceedsAfterUnlink()
    {
        var project = await CreateProjectAsync();
        var gate = await CreateEntryAsync(project.Id, "gate", "Linked Gate");
        var kit = await CreateKitAsync("guard-kit");
        await _client.PostAsync($"/api/kits/{kit.Id}/entries/{gate.Id}", null);

        var blocked = await _client.DeleteAsync($"/api/projects/{project.Id}");
        blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await blocked.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Detail!.ShouldContain("linked into skill kits");

        await _client.DeleteAsync($"/api/kits/{kit.Id}/entries/{gate.Id}");
        var allowed = await _client.DeleteAsync($"/api/projects/{project.Id}");
        allowed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_KitLibraryProjectAfterImport_Returns409()
    {
        var sample = KitDir("sample");
        WriteSkill(sample, "deploy");
        await _client.PostAsJsonAsync("/api/kits/import",
            new { Path = _sandbox, Kits = new[] { new { RelativePath = "sample", Name = "sample" } } });

        var libraryProject = (await _client.GetFromJsonAsync<List<ProjectJson>>("/api/projects", JsonOptions))!
            .Single(p => p.Name == "Kit Library");

        var response = await _client.DeleteAsync($"/api/projects/{libraryProject.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private async Task<T?> PostReadAsync<T>(string url, object body) where T : class
    {
        var response = await _client.PostAsJsonAsync(url, body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    private string KitDir(string name)
    {
        var path = Path.Combine(_sandbox, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string SkillYaml(string name, string description = "Does a thing.") =>
        $"---\nname: {name}\ndescription: {description}\n---\n\nBody.\n";

    private static string Combine(params string[] parts) =>
        string.Join(Path.DirectorySeparatorChar, parts);

    private void WriteSkill(string kitDir, string name, string? content = null)
    {
        var dir = Path.Combine(kitDir, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), content ?? SkillYaml(name));
    }

    private async Task<KitJson> CreateKitAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/kits", new { Name = name, Version = 1 });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var kit = await response.Content.ReadFromJsonAsync<KitJson>(JsonOptions);
        kit.ShouldNotBeNull();
        return kit!;
    }

    private async Task<Guid> SeedBundledKitAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var kit = SkillKit.Create(name, 1, string.Empty);
        kit.MarkBundled(Path.Combine(_sandbox, name));
        db.SkillKits.Add(kit);
        await db.SaveChangesAsync();
        return kit.Id;
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"import-proj-{Guid.NewGuid():N}",
            DiskPath = _sandbox,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task<EntryListJson> CreateEntryAsync(Guid projectId, string type, string title)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = type,
            Title = title,
            Content = $"{title} body",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await response.Content.ReadFromJsonAsync<EntryListJson>(JsonOptions);
        entry.ShouldNotBeNull();
        return entry!;
    }

    public sealed record ProjectJson(Guid Id, string Name);

    public sealed record EntryListJson(Guid Id, string Type, string Title);

    public sealed record KitJson(
        Guid Id,
        string Name,
        int Version,
        string Description,
        List<Guid> EntryIds,
        string SourceKind,
        string? SourcePath,
        DateTime? ImportedAtUtc);

    public sealed record ScanPreviewJson(string ScannedPath, bool Truncated, List<CandidateJson> Candidates);

    public sealed record CandidateJson(
        string Name,
        string Path,
        string RelativePath,
        bool NameCollision,
        Guid? NameCollisionKitId,
        Guid? ReImportMatchKitId,
        List<SkillJson> Skills,
        List<RuleJson> Gates,
        List<RuleJson> Guards,
        List<IssueJson> Issues);

    public sealed record SkillJson(string Name, string RelativePath, string? Platform, int FileCount);

    public sealed record RuleJson(string RelativePath, string Title);

    public sealed record IssueJson(string RelativePath, string Code, string Message);

    public sealed record ImportReportJson(string ScannedPath, List<KitResultJson> Kits);

    public sealed record KitResultJson(string Name, Guid? KitId, bool Success, string? FailureKind, string? Error);

    public sealed record ReimportReportJson(
        Guid KitId,
        string ScannedPath,
        int AddedCount,
        int UpdatedCount,
        int KeptLocalCount,
        int ConflictedCount,
        int RemovedCount,
        List<ReimportEntryJson> Entries);

    public sealed record ReimportEntryJson(
        string Outcome,
        string EntryType,
        string Title,
        string? RelativePath,
        Guid? EntryId,
        string? ImportedContentHash,
        bool EntryRowRetained);

    public sealed record ProblemJson(int? Status, string? Title, string? Detail);
}
