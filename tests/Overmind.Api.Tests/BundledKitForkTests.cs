using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class BundledKitForkTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public BundledKitForkTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-fork-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sandboxDir);
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
    public async Task Update_ContentChange_OnExclusiveBundledEntry_ForksKitToManual()
    {
        var project = await CreateProjectAsync();
        var entry = await CreateEntryAsync(project.Id, "original body");
        var kitId = await SeedKitAsync("bundled-kit", k => k.MarkBundled(Path.Combine(_sandboxDir, "bundled-kit")), entry.Id);

        var put = await PutEntryAsync(entry.Id, "renamed", "personalized body");

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await put.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        updated.ShouldNotBeNull();
        updated!.CurrentVersion.ShouldBe(1);

        var kit = await _client.GetFromJsonAsync<KitJson>($"/api/kits/{kitId}", JsonOptions);
        kit.ShouldNotBeNull();
        kit!.SourceKind.ShouldBe("manual");
        kit.SourcePath.ShouldBeNull();
        kit.ImportedAtUtc.ShouldBeNull();
        kit.EntryIds.ShouldContain(entry.Id);

        var second = await PutEntryAsync(entry.Id, "renamed", "personalized again");
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_ContentChange_OnEntrySharedByTwoKits_DoesNotFork()
    {
        var project = await CreateProjectAsync();
        var entry = await CreateEntryAsync(project.Id, "shared body");
        var firstId = await SeedKitAsync("bundled-first", k => k.MarkBundled(Path.Combine(_sandboxDir, "bundled-first")), entry.Id);
        var secondId = await SeedKitAsync("bundled-second", k => k.MarkBundled(Path.Combine(_sandboxDir, "bundled-second")), entry.Id);

        var put = await PutEntryAsync(entry.Id, "shared", "edited shared body");

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        foreach (var kitId in new[] { firstId, secondId })
        {
            var kit = await _client.GetFromJsonAsync<KitJson>($"/api/kits/{kitId}", JsonOptions);
            kit.ShouldNotBeNull();
            kit!.SourceKind.ShouldBe("bundled");
            kit.SourcePath.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task Update_ContentChange_OnManualAndImportedKits_LeavesOriginsAlone()
    {
        var project = await CreateProjectAsync();
        var manualEntry = await CreateEntryAsync(project.Id, "manual body");
        var importedEntry = await CreateEntryAsync(project.Id, "imported body");

        var manualKit = await CreateKitViaApiAsync("stays-manual");
        await _client.PostAsync($"/api/kits/{manualKit}/entries/{manualEntry.Id}", null);
        var importedKit = await SeedKitAsync("stays-imported",
            k => k.MarkImported(Path.Combine(_sandboxDir, "stays-imported")), importedEntry.Id);

        var manualPut = await PutEntryAsync(manualEntry.Id, "manual", "edited manual body");
        var importedPut = await PutEntryAsync(importedEntry.Id, "imported", "edited imported body");

        manualPut.StatusCode.ShouldBe(HttpStatusCode.OK);
        importedPut.StatusCode.ShouldBe(HttpStatusCode.OK);

        var manual = await _client.GetFromJsonAsync<KitJson>($"/api/kits/{manualKit}", JsonOptions);
        manual!.SourceKind.ShouldBe("manual");
        manual.SourcePath.ShouldBeNull();

        var imported = await _client.GetFromJsonAsync<KitJson>($"/api/kits/{importedKit}", JsonOptions);
        imported!.SourceKind.ShouldBe("imported");
        imported.SourcePath.ShouldNotBeNull();
        imported.ImportedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Update_TitleOnly_OnExclusiveBundledEntry_DoesNotFork()
    {
        var project = await CreateProjectAsync();
        var entry = await CreateEntryAsync(project.Id, "stable body");
        var kitId = await SeedKitAsync("bundled-title", k => k.MarkBundled(Path.Combine(_sandboxDir, "bundled-title")), entry.Id);

        var put = await PutEntryAsync(entry.Id, "new title only", "stable body");

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await put.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        updated!.Title.ShouldBe("new title only");

        var kit = await _client.GetFromJsonAsync<KitJson>($"/api/kits/{kitId}", JsonOptions);
        kit.ShouldNotBeNull();
        kit!.SourceKind.ShouldBe("bundled");
        kit.SourcePath.ShouldNotBeNull();
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"fork-proj-{Guid.NewGuid():N}",
            DiskPath = _sandboxDir,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task<EntryJson> CreateEntryAsync(Guid projectId, string content)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = "Note",
            Title = "entry",
            Content = content,
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        entry.ShouldNotBeNull();
        return entry!;
    }

    private Task<HttpResponseMessage> PutEntryAsync(Guid entryId, string title, string content) =>
        _client.PutAsJsonAsync($"/api/entries/{entryId}", new
        {
            Title = title,
            Content = content,
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

    private async Task<Guid> CreateKitViaApiAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/kits", new { Name = name, Version = 1 });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var kit = await response.Content.ReadFromJsonAsync<KitJson>(JsonOptions);
        kit.ShouldNotBeNull();
        return kit!.Id;
    }

    private async Task<Guid> SeedKitAsync(string name, Action<SkillKit> configureOrigin, params Guid[] entryIds)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var kit = SkillKit.Create(name, 1, string.Empty);
        configureOrigin(kit);
        foreach (var entryId in entryIds)
        {
            kit.AddEntry(entryId);
        }
        db.SkillKits.Add(kit);
        await db.SaveChangesAsync();
        return kit.Id;
    }

    public sealed record ProjectJson(Guid Id, string Name);

    public sealed record EntryJson(Guid Id, string Title, int CurrentVersion);

    public sealed record KitJson(
        Guid Id,
        string Name,
        int Version,
        string Description,
        List<Guid> EntryIds,
        string SourceKind,
        string? SourcePath,
        DateTime? ImportedAtUtc);
}
