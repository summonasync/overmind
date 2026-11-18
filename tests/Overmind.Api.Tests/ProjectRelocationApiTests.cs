using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class ProjectRelocationApiTests : IDisposable
{
    private const string PinMarker = "code pin moved with project root";
    private const string CommitFaultMarker = "overmind-test-relocation-commit-fault";

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ProjectRelocationApiTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-relocation-{Guid.NewGuid():N}");
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
    public async Task Relocate_VerifiedMove_RemapPinsAndListCompanionRegistries()
    {
        var (oldRoot, newRoot) = CreateRoots("verified");
        var project = await CreateProjectAsync("verified", oldRoot);
        await DisableMaterializationAsync(project.Id);
        var moved = await SeedKitRowAsync(project.Id, newRoot, "docs/orphan.md", "# orphaned skill bytes\n");
        Directory.CreateDirectory(Path.GetDirectoryName(moved)!);
        await File.WriteAllTextAsync(moved, "# orphaned skill bytes\n");

        var pinned = await CreateFeatureAsync(project.Id, "020-pinned");
        await PinAsync(project.Id, pinned, "feat/020", Path.Combine(oldRoot, ".lanes", "020"));
        var foreign = await CreateFeatureAsync(project.Id, "021-foreign");
        await PinAsync(project.Id, foreign, null, "/var/other-product/wt");

        var response = await RelocateAsync(project.Id, project.Name, oldRoot, newRoot);
        var payload = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, payload);

        var body = JsonSerializer.Deserialize<ProjectJson>(payload, JsonOptions);
        body.ShouldNotBeNull();
        body!.DiskPath.ShouldBe(newRoot);
        var relocation = body.Relocation.ShouldNotBeNull();
        relocation.PreviousDiskPath.ShouldBe(oldRoot);
        relocation.PinsRemapped.ShouldBe(1);
        relocation.RemappedFeatureSlugs.ShouldHaveSingleItem().ShouldBe(pinned);
        relocation.CompanionSteps.ShouldContain(
            step => step.Contains("grimoire", StringComparison.OrdinalIgnoreCase));
        relocation.CompanionSteps.ShouldContain(
            step => step.Contains("squeezr", StringComparison.OrdinalIgnoreCase));

        var door = await ReadFeatureAsync(project.Id, pinned);
        door.PinnedWorktreePath.ShouldBe(Path.Combine(newRoot, ".lanes", "020"));
        door.PinnedBranch.ShouldBe("feat/020");
        CountMarkerLines(door.Body).ShouldBe(1);
        MarkerLineSource(door.Body).ShouldBe(PlanningLogSource.Ui);

        var untouched = await ReadFeatureAsync(project.Id, foreign);
        untouched.PinnedWorktreePath.ShouldBe("/var/other-product/wt");
        CountMarkerLines(untouched.Body).ShouldBe(0);

        var notification = (await ListNotificationsAsync())
            .SingleOrDefault(n => n.Text.Contains("relocated", StringComparison.Ordinal));
        notification.ShouldNotBeNull();
        notification!.Kind.ShouldBe("info");
        notification.Text.ShouldContain("grimoire");
        notification.Text.ShouldContain("squeezr");
        notification.Text.ShouldContain(newRoot);
        notification.Text.ShouldContain("1 worktree pin(s) remapped");
        notification.ProjectId.ShouldBe(project.Id);
    }

    [Fact]
    public async Task Relocate_MultiplePins_RemapsEachAndCountsOneProvenanceLinePerPin()
    {
        var (oldRoot, newRoot) = CreateRoots("multi");
        var project = await CreateProjectAsync("multi", oldRoot);
        await DisableMaterializationAsync(project.Id);

        var lane = await CreateFeatureAsync(project.Id, "025-lane");
        await PinAsync(project.Id, lane, "feat/025", Path.Combine(oldRoot, ".lanes", "025"));
        var nested = await CreateFeatureAsync(project.Id, "026-nested");
        await PinAsync(project.Id, nested, null, Path.Combine(oldRoot, "worktrees", "026"));

        var response = await RelocateAsync(project.Id, project.Name, oldRoot, newRoot);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        payload.ShouldNotBeNull();
        payload!.DiskPath.ShouldBe(newRoot);
        var relocation = payload.Relocation.ShouldNotBeNull();
        relocation.PinsRemapped.ShouldBe(2);
        relocation.RemappedFeatureSlugs.Length.ShouldBe(2);

        var movedLane = await ReadFeatureAsync(project.Id, lane);
        movedLane.PinnedWorktreePath.ShouldBe(Path.Combine(newRoot, ".lanes", "025"));
        CountMarkerLines(movedLane.Body).ShouldBe(1);
        MarkerLineSource(movedLane.Body).ShouldBe(PlanningLogSource.Ui);

        var movedNested = await ReadFeatureAsync(project.Id, nested);
        movedNested.PinnedWorktreePath.ShouldBe(Path.Combine(newRoot, "worktrees", "026"));
        CountMarkerLines(movedNested.Body).ShouldBe(1);
        MarkerLineSource(movedNested.Body).ShouldBe(PlanningLogSource.Ui);
    }

    [Fact]
    public async Task Relocate_CommitFaultsOnThePinWrite_LeavesNeitherPathNorPinsMoved()
    {
        var (oldRoot, newRoot) = CreateRoots("commit-fault");
        var project = await CreateProjectAsync("commit-fault", oldRoot);
        await DisableMaterializationAsync(project.Id);
        var slug = await CreateFeatureAsync(project.Id, "027-pinned");
        var pinnedPath = Path.Combine(oldRoot, ".lanes", "027");
        await PinAsync(project.Id, slug, null, pinnedPath);

        await ArmCommitFaultAsync();

        var (moved, evidence) = await TryRelocateAsync(project.Id, project.Name, oldRoot, newRoot);
        moved.ShouldBeFalse();
        evidence.ShouldContain(CommitFaultMarker);

        var fetched = await _client.GetFromJsonAsync<ProjectJson>($"/api/projects/{project.Id}", JsonOptions);
        fetched!.DiskPath.ShouldBe(oldRoot);
        fetched.Relocation.ShouldBeNull();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var persisted = await db.PlanningFeatures.AsNoTracking().SingleAsync(f => f.Slug == slug);
        persisted.PinnedWorktreePath.ShouldBe(pinnedPath);
        CountMarkerLines(persisted.Body).ShouldBe(0);

        await SeedControlNotificationAsync();
        var notifications = await ListNotificationsAsync();
        notifications.ShouldContain(n => n.Text == "control-baseline");
        notifications.Any(n => n.Text.Contains("relocated", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public async Task Relocate_VerificationFails_LeavesPathPinsAndNotificationUntouched()
    {
        var (oldRoot, newRoot) = CreateRoots("drift");
        var project = await CreateProjectAsync("drift", oldRoot);
        await DisableMaterializationAsync(project.Id);
        await SeedControlNotificationAsync();
        await SeedKitRowAsync(project.Id, newRoot, "docs/missing.md", "# never copied\n");

        var slug = await CreateFeatureAsync(project.Id, "022-pinned");
        await PinAsync(project.Id, slug, null, Path.Combine(oldRoot, "wt"));

        var response = await RelocateAsync(project.Id, project.Name, oldRoot, newRoot);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Detail!.ShouldContain("docs/missing.md");

        var fetched = await _client.GetFromJsonAsync<ProjectJson>($"/api/projects/{project.Id}", JsonOptions);
        fetched!.DiskPath.ShouldBe(oldRoot);
        fetched.Relocation.ShouldBeNull();

        var feature = await ReadFeatureAsync(project.Id, slug);
        feature.PinnedWorktreePath.ShouldBe(Path.Combine(oldRoot, "wt"));
        CountMarkerLines(feature.Body).ShouldBe(0);

        var blocked = await ListNotificationsAsync();
        blocked.ShouldContain(n => n.Text == "control-baseline");
        blocked.Any(n => n.Text.Contains("relocated", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public async Task Relocate_WithoutPins_StillListCompanionRegistries()
    {
        var (oldRoot, newRoot) = CreateRoots("nopins");
        var project = await CreateProjectAsync("nopins", oldRoot);

        var response = await RelocateAsync(project.Id, project.Name, oldRoot, newRoot);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        var relocation = body!.Relocation.ShouldNotBeNull();
        relocation.PinsRemapped.ShouldBe(0);
        relocation.RemappedFeatureSlugs.ShouldBeEmpty();
        relocation.CompanionSteps.Length.ShouldBe(2);

        var notification = (await ListNotificationsAsync())
            .Single(n => n.Text.Contains("relocated", StringComparison.Ordinal));
        notification.Text.ShouldContain("grimoire");
        notification.Text.ShouldContain("squeezr");
        notification.Text.ShouldContain("0 worktree pin(s) remapped");
    }

    [Fact]
    public async Task Update_SameDiskPath_CarriesNoRelocationSection()
    {
        var (oldRoot, controlRoot) = CreateRoots("unchanged");
        var project = await CreateProjectAsync("unchanged", oldRoot);
        await CreateFeatureAsync(project.Id, "023-plain");
        await CreateProjectAsync("unchanged-control", controlRoot);

        var put = await RelocateAsync(project.Id, project.Name, oldRoot, oldRoot);
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var putDocument = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
        FieldNames(putDocument.RootElement).ShouldNotContain("relocation");

        using var getDocument = JsonDocument.Parse(await _client.GetStringAsync($"/api/projects/{project.Id}"));
        var fetchedFields = FieldNames(getDocument.RootElement);
        fetchedFields.ShouldContain("diskPath");
        fetchedFields.ShouldNotContain("relocation");

        using var listDocument = JsonDocument.Parse(await _client.GetStringAsync("/api/projects"));
        var listRows = listDocument.RootElement.EnumerateArray().ToArray();
        listRows.Length.ShouldBe(2);
        listRows.SelectMany(FieldNames).ShouldNotContain("relocation");

        await SeedControlNotificationAsync();
        var notifications = await ListNotificationsAsync();
        notifications.ShouldContain(n => n.Text == "control-baseline");
        notifications.Any(n => n.Text.Contains("relocated", StringComparison.Ordinal)).ShouldBeFalse();
    }

    private async Task DisableMaterializationAsync(Guid projectId)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/workflow",
            new { Materialization = "off", RequireHumanValidation = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task SeedControlNotificationAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/agent/notifications", new { Kind = "info", Text = "control-baseline" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private (string OldRoot, string NewRoot) CreateRoots(string name)
    {
        var oldRoot = Path.Combine(_sandboxDir, $"{name}-old");
        var newRoot = Path.Combine(_sandboxDir, $"{name}-new");
        Directory.CreateDirectory(oldRoot);
        Directory.CreateDirectory(newRoot);
        return (oldRoot, newRoot);
    }

    private async Task<ProjectJson> CreateProjectAsync(string name, string diskPath)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/projects", new { Name = name, DiskPath = diskPath, Description = "relocation subject" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task<string> CreateFeatureAsync(Guid projectId, string slug)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = slug, Body = $"<goal>{slug}</goal>\n" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return slug;
    }

    private async Task PinAsync(Guid projectId, string slug, string? branch, string worktreePath)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}/pin-code",
            new { Branch = branch, WorktreePath = worktreePath });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> RelocateAsync(Guid projectId, string name, string oldRoot, string newRoot) =>
        await _client.PutAsJsonAsync($"/api/projects/{projectId}", new
        {
            Name = name,
            DiskPath = newRoot,
            Description = "relocation subject",
        });

    private async Task<FeatureJson> ReadFeatureAsync(Guid projectId, string slug)
    {
        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/agent/projects/{projectId}/planning/features/{slug}?includeArtifacts=false", JsonOptions);

        feature.ShouldNotBeNull();
        return feature!;
    }

    private async Task<NotificationJson[]> ListNotificationsAsync()
    {
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/api/notifications"));
        var items = document.RootElement.GetProperty("items");

        return [.. items.EnumerateArray()
            .Select(element => element.Deserialize<NotificationJson>(JsonOptions)!)];
    }

    private static string[] FieldNames(JsonElement element) =>
        [.. element.EnumerateObject().Select(property => property.Name)];

    private static int CountMarkerLines(string body) =>
        PlanningLog.Parse(body).Count(line => line.Text.Contains(PinMarker, StringComparison.Ordinal));

    private static PlanningLogSource? MarkerLineSource(string body) =>
        PlanningLog.Parse(body)
            .Single(line => line.Text.Contains(PinMarker, StringComparison.Ordinal))
            .Source;

    private async Task ArmCommitFaultAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            $"""
            CREATE TRIGGER relocation_commit_fault
            BEFORE UPDATE OF PinnedWorktreePath ON PlanningFeatures
            BEGIN SELECT RAISE(ABORT, '{CommitFaultMarker}'); END
            """);
    }

    private async Task<(bool Moved, string Evidence)> TryRelocateAsync(
        Guid projectId, string name, string oldRoot, string newRoot)
    {
        try
        {
            var response = await RelocateAsync(projectId, name, oldRoot, newRoot);
            return (response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, ex.ToString());
        }
    }

    private async Task<string> SeedKitRowAsync(Guid projectId, string root, string relativePath, string content)
    {
        var kit = new { Name = $"relocation-{Guid.NewGuid():N}", Version = 1, Description = (string?)null };
        var created = await _client.PostAsJsonAsync("/api/kits", kit);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdKit = await created.Content.ReadFromJsonAsync<KitJson>(JsonOptions);
        createdKit.ShouldNotBeNull();

        var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            db.MaterializedFiles.Add(MaterializedFile.Create(
                projectId, createdKit!.Id, AgentPlatform.Claude, relativePath, sha));
            db.SaveChanges();
        }

        return Path.Combine(root, relativePath);
    }

    public sealed record ProjectJson(
        Guid Id,
        string Name,
        string DiskPath,
        RelocationJson? Relocation);

    public sealed record RelocationJson(
        string PreviousDiskPath,
        int PinsRemapped,
        string[] RemappedFeatureSlugs,
        string[] CompanionSteps);

    public sealed record FeatureJson(string Slug, string? PinnedBranch, string? PinnedWorktreePath, string Body);

    public sealed record NotificationJson(Guid Id, Guid? ProjectId, string Kind, string Text);

    public sealed record KitJson(Guid Id, string Name, int Version);

    public sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
