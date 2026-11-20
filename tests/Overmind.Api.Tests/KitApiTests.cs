using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class KitApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;
    private readonly string _projectDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public KitApiTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-api-{Guid.NewGuid():N}");
        _projectDir = Path.Combine(_sandboxDir, "project");
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();

        if (Directory.Exists(_sandboxDir))
        {
            Directory.Delete(_sandboxDir, recursive: true);
        }
    }

    [Fact]
    public async Task Kit_Lifecycle_MaterializesAndDematerializesOnDisk()
    {
        var project = await CreateProjectAsync();
        var skill = await CreateSkillEntryAsync(project.Id);
        var gate = await CreateEntryAsync(project.Id, "gate", "Tests Green", "All tests must pass before merge.");
        var kit = await CreateKitAsync("quality", 1);

        var addSkill = await _client.PostAsync($"/api/kits/{kit.Id}/entries/{skill.Id}", null);
        addSkill.StatusCode.ShouldBe(HttpStatusCode.OK);
        var addGate = await _client.PostAsync($"/api/kits/{kit.Id}/entries/{gate.Id}", null);
        addGate.StatusCode.ShouldBe(HttpStatusCode.OK);

        var duplicate = await _client.PostAsync($"/api/kits/{kit.Id}/entries/{skill.Id}", null);
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var activate = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kit.Id}/activate", null);
        activate.StatusCode.ShouldBe(HttpStatusCode.OK);

        var claudeFile = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        var kimiFile = Path.Combine(_projectDir, ".kimi-code", "skills", "reviewer", "reviewer", "SKILL.md");
        var bootstrap = Path.Combine(_projectDir, ".claude", "skills", "overmind-bootstrap-quality", "SKILL.md");

        File.Exists(claudeFile).ShouldBeTrue();
        File.Exists(kimiFile).ShouldBeTrue();
        File.Exists(bootstrap).ShouldBeTrue();

        var bootstrapText = await File.ReadAllTextAsync(bootstrap);
        bootstrapText.ShouldContain($"overmind://projects/{project.Id}/gates");
        bootstrapText.ShouldContain("All tests must pass before merge.");

        var drift = await _client.GetFromJsonAsync<List<DriftJson>>($"/api/projects/{project.Id}/drift", JsonOptions);
        drift.ShouldNotBeNull();
        drift!.Count.ShouldBe(0);

        var deactivate = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kit.Id}/deactivate", null);
        deactivate.StatusCode.ShouldBe(HttpStatusCode.OK);

        File.Exists(claudeFile).ShouldBeFalse();
        File.Exists(kimiFile).ShouldBeFalse();
        File.Exists(bootstrap).ShouldBeFalse();
    }

    [Fact]
    public async Task Activate_WithTraversalRelativePath_Returns400()
    {
        var project = await CreateProjectAsync();
        var skill = await CreateSkillEntryAsync(project.Id, "../../evil.md");
        var kit = await CreateKitAsync("evil-kit", 1);

        var add = await _client.PostAsync($"/api/kits/{kit.Id}/entries/{skill.Id}", null);
        add.StatusCode.ShouldBe(HttpStatusCode.OK);

        var activate = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kit.Id}/activate", null);
        activate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await activate.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Status.ShouldBe(400);
        problem!.Detail!.ShouldContain("escapes");
    }

    [Fact]
    public async Task Activate_WithUntrackedFileOnDisk_Returns409AndKeepsFile()
    {
        var project = await CreateProjectAsync();
        var skill = await CreateSkillEntryAsync(project.Id);
        var kit = await CreateKitAsync("blocked", 1);

        var add = await _client.PostAsync($"/api/kits/{kit.Id}/entries/{skill.Id}", null);
        add.StatusCode.ShouldBe(HttpStatusCode.OK);

        var target = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllTextAsync(target, "precious user file");

        var activate = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kit.Id}/activate", null);
        activate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await File.ReadAllTextAsync(target)).ShouldBe("precious user file");
    }

    [Fact]
    public async Task Activate_WithUntrackedFile_Returns409ThenForceAdoptsAndWarns()
    {
        var project = await CreateProjectAsync();
        var skill = await CreateSkillEntryAsync(project.Id);
        var kit = await CreateKitAsync("adopted", 1);
        await _client.PostAsync($"/api/kits/{kit.Id}/entries/{skill.Id}", null);

        var target = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllTextAsync(target, "precious user file");

        var blocked = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kit.Id}/activate", null);
        blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var forced = await _client.PostAsync(
            $"/api/projects/{project.Id}/kits/{kit.Id}/activate?force=true", null);
        forced.StatusCode.ShouldBe(HttpStatusCode.OK);

        var report = await forced.Content.ReadFromJsonAsync<MaterializationReportJson>(JsonOptions);
        report.ShouldNotBeNull();
        report!.Warnings.ShouldHaveSingleItem().ShouldBe($"Overwrote untracked file: {target}");
        (await File.ReadAllTextAsync(target)).ShouldNotContain("precious user file");

        var drift = await _client.GetFromJsonAsync<List<DriftJson>>($"/api/projects/{project.Id}/drift", JsonOptions);
        drift.ShouldNotBeNull();
        drift!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Drift_ReportsModifiedFileThroughApi()
    {
        var project = await CreateProjectAsync();
        var skill = await CreateSkillEntryAsync(project.Id);
        var kit = await CreateKitAsync("drift-kit", 1);

        await _client.PostAsync($"/api/kits/{kit.Id}/entries/{skill.Id}", null);
        await _client.PostAsync($"/api/projects/{project.Id}/kits/{kit.Id}/activate", null);

        var claudeFile = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        await File.AppendAllTextAsync(claudeFile, "edited by hand\n");

        var drift = await _client.GetFromJsonAsync<List<DriftJson>>($"/api/projects/{project.Id}/drift", JsonOptions);
        drift.ShouldNotBeNull();
        drift!.Count.ShouldBe(1);
        drift[0].Kind.ShouldBe("modified");
        drift[0].RelativePath.ShouldBe(".claude/skills/reviewer/reviewer/SKILL.md");
    }

    [Fact]
    public async Task Activate_TrackedSkillFileHandEdited_Returns409ThenForceRegeneratesAndWarns()
    {
        var project = await CreateProjectAsync();
        var skill = await CreateSkillEntryAsync(project.Id);
        var kit = await CreateKitAsync("drifted", 1);
        await _client.PostAsync($"/api/kits/{kit.Id}/entries/{skill.Id}", null);

        var activate = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kit.Id}/activate", null);
        activate.StatusCode.ShouldBe(HttpStatusCode.OK);

        var claudeFile = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        await File.AppendAllTextAsync(claudeFile, "hand edit\n");

        var blocked = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kit.Id}/activate", null);
        blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await blocked.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Status.ShouldBe(409);
        problem!.Detail!.ShouldContain(claudeFile);
        (await File.ReadAllTextAsync(claudeFile)).ShouldContain("hand edit");

        var forced = await _client.PostAsync(
            $"/api/projects/{project.Id}/kits/{kit.Id}/activate?force=true", null);
        forced.StatusCode.ShouldBe(HttpStatusCode.OK);

        var report = await forced.Content.ReadFromJsonAsync<MaterializationReportJson>(JsonOptions);
        report.ShouldNotBeNull();
        report!.Warnings.ShouldHaveSingleItem().ShouldBe($"Overwrote locally modified file: {claudeFile}");
        (await File.ReadAllTextAsync(claudeFile)).ShouldNotContain("hand edit");

        var drift = await _client.GetFromJsonAsync<List<DriftJson>>($"/api/projects/{project.Id}/drift", JsonOptions);
        drift.ShouldNotBeNull();
        drift!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Update_DiskPathChangeWhileKitActive_Returns409ThenSucceedsAfterDeactivate()
    {
        var project = await CreateProjectAsync();
        var skill = await CreateSkillEntryAsync(project.Id);
        var kit = await CreateKitAsync("pinned", 1);
        await _client.PostAsync($"/api/kits/{kit.Id}/entries/{skill.Id}", null);

        var activate = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kit.Id}/activate", null);
        activate.StatusCode.ShouldBe(HttpStatusCode.OK);

        var otherDir = Path.Combine(_sandboxDir, "moved-project");
        Directory.CreateDirectory(otherDir);
        var blocked = await _client.PutAsJsonAsync($"/api/projects/{project.Id}", new
        {
            Name = project.Name,
            DiskPath = otherDir,
            Description = project.Description,
        });

        blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await blocked.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Detail!.ShouldContain("while kits are active");
        problem.Detail.ShouldContain("deactivate kits first");
        problem.Detail.ShouldNotContain("files are materialized");

        var deactivate = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kit.Id}/deactivate", null);
        deactivate.StatusCode.ShouldBe(HttpStatusCode.OK);

        var allowed = await _client.PutAsJsonAsync($"/api/projects/{project.Id}", new
        {
            Name = project.Name,
            DiskPath = otherDir,
            Description = project.Description,
        });

        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await allowed.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        updated.ShouldNotBeNull();
        updated!.DiskPath.ShouldBe(otherDir);
    }

    [Fact]
    public async Task Update_OrphanedKitRows_AreVerifiedAtCandidateDiskPath()
    {
        var project = await CreateProjectAsync();
        var kit = await CreateKitAsync("orphan-rows", 1);

        var candidate = Path.Combine(_sandboxDir, "relocated");
        Directory.CreateDirectory(candidate);
        var orphanPath = Path.Combine(candidate, "docs", "orphan.md");
        const string orphanContent = "# orphaned skill bytes\n";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            db.MaterializedFiles.Add(MaterializedFile.Create(
                project.Id,
                kit.Id,
                AgentPlatform.Claude,
                "docs/orphan.md",
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(orphanContent)))));
            db.SaveChanges();
        }

        var blocked = await _client.PutAsJsonAsync($"/api/projects/{project.Id}", new
        {
            Name = project.Name,
            DiskPath = candidate,
            Description = project.Description,
        });

        blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await blocked.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Detail!.ShouldContain("1 of 1 files drifted or missing");
        problem.Detail.ShouldContain("docs/orphan.md");

        Directory.CreateDirectory(Path.GetDirectoryName(orphanPath)!);
        await File.WriteAllTextAsync(orphanPath, orphanContent);

        var allowed = await _client.PutAsJsonAsync($"/api/projects/{project.Id}", new
        {
            Name = project.Name,
            DiskPath = candidate,
            Description = project.Description,
        });

        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await allowed.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        updated!.DiskPath.ShouldBe(candidate);
    }

    [Theory]
    [InlineData("../escape.md")]
    [InlineData("docs/../../escape.md")]
    [InlineData("/etc/passwd")]
    public async Task Update_MirrorRowPathEscapesCandidate_Returns400AndKeepsProject(string relativePath)
    {
        var project = await CreateProjectAsync();
        var kit = await CreateKitAsync("escaped-rows", 1);

        var candidate = Path.Combine(_sandboxDir, "relocated");
        Directory.CreateDirectory(candidate);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            db.MaterializedFiles.Add(MaterializedFile.Create(
                project.Id,
                kit.Id,
                AgentPlatform.Claude,
                relativePath,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("stale bytes")))));
            db.SaveChanges();
        }

        var blocked = await _client.PutAsJsonAsync($"/api/projects/{project.Id}", new
        {
            Name = project.Name,
            DiskPath = candidate,
            Description = project.Description,
        });

        blocked.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await blocked.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Status.ShouldBe(400);
        problem.Title.ShouldBe("Invalid materialization target");
        problem.Detail!.ShouldContain(relativePath);
        problem.Detail.ShouldContain("escapes the project directory");

        var untouched = await _client.GetFromJsonAsync<ProjectJson>($"/api/projects/{project.Id}", JsonOptions);
        untouched!.DiskPath.ShouldBe(project.DiskPath);
    }

    [Fact]
    public async Task Activate_WithUntrackedFile_PersistsNothing()
    {
        var project = await CreateProjectAsync();
        var skill = await CreateSkillEntryAsync(project.Id);
        var kit = await CreateKitAsync("atomic", 1);
        await _client.PostAsync($"/api/kits/{kit.Id}/entries/{skill.Id}", null);

        var target = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllTextAsync(target, "precious user file");

        var activate = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kit.Id}/activate", null);
        activate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await File.ReadAllTextAsync(target)).ShouldBe("precious user file");

        var drift = await _client.GetFromJsonAsync<List<DriftJson>>($"/api/projects/{project.Id}/drift", JsonOptions);
        drift.ShouldNotBeNull();
        drift!.Count.ShouldBe(0);

        var deactivate = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kit.Id}/deactivate", null);
        deactivate.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_KitIncludesEntryIds()
    {
        var project = await CreateProjectAsync();
        var skill = await CreateSkillEntryAsync(project.Id);
        var kit = await CreateKitAsync("listed", 2);

        await _client.PostAsync($"/api/kits/{kit.Id}/entries/{skill.Id}", null);

        var fetched = await _client.GetFromJsonAsync<KitJson>($"/api/kits/{kit.Id}", JsonOptions);
        fetched.ShouldNotBeNull();
        fetched!.Name.ShouldBe("listed");
        fetched.Version.ShouldBe(2);
        fetched.EntryIds.ShouldBe([skill.Id]);
    }

    [Fact]
    public async Task Get_KitCarriesManualOriginDefaults()
    {
        var kit = await CreateKitAsync("origin", 1);

        var fetched = await _client.GetFromJsonAsync<KitJson>($"/api/kits/{kit.Id}", JsonOptions);
        fetched.ShouldNotBeNull();
        fetched!.SourceKind.ShouldBe("manual");
        fetched.SourcePath.ShouldBeNull();
        fetched.ImportedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Create_WithTakenName_Returns409DuplicateName()
    {
        await CreateKitAsync("twin", 1);

        var duplicate = await _client.PostAsJsonAsync("/api/kits", new
        {
            Name = "twin",
            Version = 2,
            Description = (string?)null,
        });

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await duplicate.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("DuplicateName");
        problem!.Detail!.ShouldContain("'twin'");
    }

    [Fact]
    public async Task Update_RenameToTakenName_Returns409AndKeepsKit()
    {
        var occupied = await CreateKitAsync("occupied", 1);
        var free = await CreateKitAsync("free-name", 1);

        var rename = await _client.PutAsJsonAsync($"/api/kits/{free.Id}", new
        {
            Name = "occupied",
            Version = occupied.Version,
            Description = "renamed",
        });

        rename.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await rename.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("DuplicateName");
        problem!.Detail!.ShouldContain("'occupied'");

        var fetched = await _client.GetFromJsonAsync<KitJson>($"/api/kits/{free.Id}", JsonOptions);
        fetched.ShouldNotBeNull();
        fetched!.Name.ShouldBe("free-name");
    }

    [Fact]
    public async Task Activate_SecondOptionalKit_StacksBothKitsFiles()
    {
        var project = await CreateProjectAsync();
        var skillA = await CreateSinglePlatformSkillAsync(project.Id, "Reviewer", "claude", "reviewer/SKILL.md");
        var skillB = await CreateSinglePlatformSkillAsync(project.Id, "Tester", "claude", "tester/SKILL.md");
        var kitA = await CreateKitAsync("pack-alpha", 1);
        var kitB = await CreateKitAsync("pack-beta", 1);
        await _client.PostAsync($"/api/kits/{kitA.Id}/entries/{skillA.Id}", null);
        await _client.PostAsync($"/api/kits/{kitB.Id}/entries/{skillB.Id}", null);

        var activateA = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kitA.Id}/activate", null);
        activateA.StatusCode.ShouldBe(HttpStatusCode.OK);

        var activateB = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kitB.Id}/activate", null);
        activateB.StatusCode.ShouldBe(HttpStatusCode.OK);

        var reviewerFile = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        var testerFile = Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md");
        var bootstrapAlpha = Path.Combine(
            _projectDir, ".claude", "skills", "overmind-bootstrap-pack-alpha", "SKILL.md");
        var bootstrapBeta = Path.Combine(
            _projectDir, ".claude", "skills", "overmind-bootstrap-pack-beta", "SKILL.md");
        File.Exists(reviewerFile).ShouldBeTrue();
        File.Exists(testerFile).ShouldBeTrue();
        File.Exists(bootstrapAlpha).ShouldBeTrue();
        File.Exists(bootstrapBeta).ShouldBeTrue();

        var drift = await _client.GetFromJsonAsync<List<DriftJson>>($"/api/projects/{project.Id}/drift", JsonOptions);
        drift.ShouldNotBeNull();
        drift!.Count.ShouldBe(0);

        var deactivateA = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kitA.Id}/deactivate", null);
        deactivateA.StatusCode.ShouldBe(HttpStatusCode.OK);

        File.Exists(reviewerFile).ShouldBeFalse();
        File.Exists(bootstrapAlpha).ShouldBeFalse();
        File.Exists(testerFile).ShouldBeTrue();
        File.Exists(bootstrapBeta).ShouldBeTrue();
    }

    [Fact]
    public async Task Activate_MandatoryGlobalKit_Returns409AndWritesNothing()
    {
        var project = await CreateProjectAsync();
        var skill = await CreateSinglePlatformSkillAsync(project.Id, "Ceremony", "claude", "ceremony/SKILL.md");
        var kit = await CreateKitAsync("overmind-ceremony", 1);
        await _client.PostAsync($"/api/kits/{kit.Id}/entries/{skill.Id}", null);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            var tracked = db.SkillKits.Find(kit.Id);
            tracked!.SetMandatory(true);
            await db.SaveChangesAsync();
        }

        var activate = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kit.Id}/activate", null);
        activate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await activate.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("MandatoryGlobal");
        problem!.Detail!.ShouldContain("mandatory-global");

        var ceremonyFile = Path.Combine(_projectDir, ".claude", "skills", "ceremony", "ceremony", "SKILL.md");
        File.Exists(ceremonyFile).ShouldBeFalse();

        var listed = await _client.GetFromJsonAsync<List<KitJson>>("/api/kits", JsonOptions);
        listed.ShouldNotBeNull();
        listed!.Single(k => k.Id == kit.Id).Mandatory.ShouldBeTrue();
    }

    [Fact]
    public async Task Activate_UnknownProject_Returns404()
    {
        var kit = await CreateKitAsync("solo", 1);

        var activate = await _client.PostAsync($"/api/projects/{Guid.NewGuid()}/kits/{kit.Id}/activate", null);

        activate.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Activate_UnknownKit_Returns404()
    {
        var project = await CreateProjectAsync();

        var activate = await _client.PostAsync($"/api/projects/{project.Id}/kits/{Guid.NewGuid()}/activate", null);

        activate.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Activate_AlreadyActiveKit_Returns200Idempotent()
    {
        var project = await CreateProjectAsync();
        var skill = await CreateSkillEntryAsync(project.Id);
        var kit = await CreateKitAsync("solo", 1);
        await _client.PostAsync($"/api/kits/{kit.Id}/entries/{skill.Id}", null);

        var first = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kit.Id}/activate", null);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await _client.PostAsync($"/api/projects/{project.Id}/kits/{kit.Id}/activate", null);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        var claudeFile = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        File.Exists(claudeFile).ShouldBeTrue();

        var drift = await _client.GetFromJsonAsync<List<DriftJson>>($"/api/projects/{project.Id}/drift", JsonOptions);
        drift.ShouldNotBeNull();
        drift!.Count.ShouldBe(0);
    }

    private async Task<EntryJson> CreateSinglePlatformSkillAsync(
        Guid projectId, string title, string platform, string relativePath)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = "skill",
            Title = title,
            Content = $"{title} skill",
            Tags = (string[]?)null,
            SkillFiles = new[]
            {
                new { Platform = platform, RelativePath = relativePath, Content = $"# {title}\n" },
            },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        entry.ShouldNotBeNull();
        return entry!;
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"kit-proj-{Guid.NewGuid():N}",
            DiskPath = _projectDir,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task<EntryJson> CreateSkillEntryAsync(Guid projectId, string relativePath = "reviewer/SKILL.md")
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = "skill",
            Title = "Reviewer",
            Content = "reviews code",
            Tags = (string[]?)null,
            SkillFiles = new[]
            {
                new { Platform = "claude", RelativePath = relativePath, Content = "# Reviewer\nRun on every change.\n" },
                new { Platform = "kimiCode", RelativePath = relativePath, Content = "# Reviewer\nRun on every change.\n" },
            },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        entry.ShouldNotBeNull();
        return entry!;
    }

    private async Task<EntryJson> CreateEntryAsync(Guid projectId, string type, string title, string content)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = type,
            Title = title,
            Content = content,
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        entry.ShouldNotBeNull();
        return entry!;
    }

    private async Task<KitJson> CreateKitAsync(string name, int version)
    {
        var response = await _client.PostAsJsonAsync("/api/kits", new
        {
            Name = name,
            Version = version,
            Description = "test kit",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var kit = await response.Content.ReadFromJsonAsync<KitJson>(JsonOptions);
        kit.ShouldNotBeNull();
        return kit!;
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record EntryJson(Guid Id, Guid ProjectId, string Type, string Title);

    public sealed record KitJson(
        Guid Id,
        string Name,
        int Version,
        string Description,
        List<Guid> EntryIds,
        string SourceKind,
        string? SourcePath,
        DateTime? ImportedAtUtc,
        bool Mandatory);

    public sealed record DriftJson(string Platform, string RelativePath, string Kind);

    public sealed record MaterializationReportJson(
        Guid ProjectId, Guid KitId, List<MaterializedFileJson> Files, List<string> Warnings);

    public sealed record MaterializedFileJson(string Platform, string RelativePath, string Sha256);

    public sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
