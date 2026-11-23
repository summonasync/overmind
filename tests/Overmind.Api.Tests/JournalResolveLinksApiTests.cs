using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class JournalResolveLinksApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Guid _projectId;
    private readonly Guid _journalId;

    public JournalResolveLinksApiTests()
    {
        _client = _factory.CreateClient();
        _projectId = CreateProject("links-project");
        _journalId = EnsureJournalProject();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Resolve_MixedContent_BucketsMatchTheResolver()
    {
        var beta = await CreateJournalAsync("Beta Note", "beta body");
        var featureId = await CreateFeatureAsync("777-resolve-preview", "Resolve preview");
        var preview = await CreateJournalAsync("Preview Note", "the note being redrafted");

        var proposed =
            "Drafting [[Beta Note]] and [[feature:777-resolve-preview]] and [[project:links-project]] " +
            "but [[Nowhere]] stays dead, a self [[Preview Note]], twice [[Beta Note]] and case [[beta note]] " +
            "and [[feature:777]] again.";

        var resolved = await ResolveAsync(proposed, preview);

        resolved.Resolved.Count.ShouldBe(3, "distinct (kind, id) pairs only — case and number forms collapse");
        resolved.Resolved.ShouldContain(link =>
            link.Kind == "entry" && link.Id == beta && link.Label == "Beta Note");
        resolved.Resolved.ShouldContain(link =>
            link.Kind == "feature" && link.Id == featureId && link.Label == "Resolve preview" &&
            link.Slug == "777-resolve-preview" && link.ProjectId == _projectId);
        resolved.Resolved.ShouldContain(link =>
            link.Kind == "project" && link.Id == _projectId && link.Label == "links-project");
        resolved.Unresolved.ShouldBe(["Nowhere"]);
        resolved.Self.ShouldBe(["Preview Note"]);
    }

    [Fact]
    public async Task Resolve_WithoutAnAnchor_SelfBucketStaysEmpty()
    {
        await CreateJournalAsync("Beta Note", "beta body");

        var resolved = await ResolveAsync("A fresh thought about [[Beta Note]] and [[Nowhere]].");

        resolved.Resolved.ShouldHaveSingleItem();
        resolved.Unresolved.ShouldBe(["Nowhere"]);
        resolved.Self.ShouldBeEmpty("a note that does not exist yet cannot reference itself");
    }

    [Fact]
    public async Task Resolve_OversizeContent_RefusesWithTheSoftCapShape()
    {
        var ceiling = SoftCap.Ceiling(SoftCapLimits.FollowUpText);

        var atTheCeiling = await PostResolveAsync(new string('x', ceiling));
        atTheCeiling.StatusCode.ShouldBe(HttpStatusCode.OK,
            await atTheCeiling.Content.ReadAsStringAsync());
        (await atTheCeiling.Content.ReadFromJsonAsync<ResolveJson>(JsonOptions))!.Resolved.ShouldBeEmpty();

        var pastTheCeiling = await PostResolveAsync(new string('x', ceiling + 1));
        pastTheCeiling.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var refusal = await pastTheCeiling.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        refusal.GetProperty("detail").GetString().ShouldContain("2000");
        refusal.TryGetProperty("fix", out var fix).ShouldBeTrue("the soft-cap refusal names its remedy");
        fix.GetString().ShouldContain("2000");
    }

    [Fact]
    public async Task Resolve_MissingAnchorEntry_RefusesNamed()
    {
        var ghost = Guid.NewGuid();

        var response = await PostResolveAsync(new
        {
            content = "mentions [[Beta Note]]",
            entryId = ghost,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        problem.GetProperty("detail").GetString().ShouldBe($"Entry '{ghost}' was not found.");
    }

    [Fact]
    public async Task Resolve_WritesNothing_EntryAndEdgeRowCountsStayByteIdentical()
    {
        var beta = await CreateJournalAsync("Beta Note", "beta body");
        var preview = await CreateJournalAsync("Preview Note", "the note being redrafted");
        var featureId = await CreateFeatureAsync("777-resolve-preview", "Resolve preview");
        await InsertEdgeRowAsync(preview, EntryLinkKind.Entry, beta, EntryLinkOrigin.Derived);
        await InsertEdgeRowAsync(preview, EntryLinkKind.Feature, featureId, EntryLinkOrigin.Manual);

        var before = await CountRowsAsync();

        var proposed =
            "Drafting [[Beta Note]] and [[feature:777-resolve-preview]] and [[project:links-project]] " +
            "but [[Nowhere]] stays dead, a self [[Preview Note]].";
        (await ResolveAsync(proposed, preview)).Resolved.Count.ShouldBe(3);
        (await ResolveAsync("A second look at [[Beta Note]].", preview)).Self.ShouldBeEmpty();
        var refused = await PostResolveAsync(
            new string('x', SoftCap.Ceiling(SoftCapLimits.FollowUpText) + 1));
        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var after = await CountRowsAsync();

        after.Entries.ShouldBe(before.Entries);
        after.Derived.ShouldBe(before.Derived, "the Derived universe stays untouched");
        after.Manual.ShouldBe(before.Manual, "the Manual universe stays untouched — triage owns those rows");
        after.Projects.ShouldBe(before.Projects, "a read door never mints a container, even for [[project:…]] markers");
    }

    private async Task<ResolveJson> ResolveAsync(string content, Guid? entryId = null) =>
        (await PostResolveAndReadAsync(new { content, entryId }))!;

    private async Task<ResolveJson?> PostResolveAndReadAsync(object body)
    {
        var response = await PostResolveAsync(body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<ResolveJson>(JsonOptions);
    }

    private Task<HttpResponseMessage> PostResolveAsync(string content) =>
        PostResolveAsync(new { content });

    private async Task<HttpResponseMessage> PostResolveAsync(object body)
    {
        var response = await _client.PostAsJsonAsync("/api/entries/resolve-links", body, JsonOptions);
        return response;
    }

    private Guid EnsureJournalProject()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<JournalProjectStore>()
            .EnsureIdAsync()
            .GetAwaiter()
            .GetResult();
    }

    private Guid CreateProject(string name)
    {
        var response = _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
        }).GetAwaiter().GetResult();

        response.EnsureSuccessStatusCode();
        return response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions).GetAwaiter().GetResult()!.Id;
    }

    private async Task<Guid> CreateJournalAsync(string title, string content)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _journalId,
            Type = "journal",
            Title = title,
            Content = content,
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        return entry!.Id;
    }

    private async Task<Guid> CreateFeatureAsync(string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/planning/features",
            new { Slug = slug, Title = title, Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .PlanningFeatures.AsNoTracking()
            .Where(f => f.Slug == slug)
            .Select(f => f.Id)
            .SingleAsync();
    }

    private async Task InsertEdgeRowAsync(
        Guid fromEntryId, EntryLinkKind toKind, Guid toId, EntryLinkOrigin origin)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        db.EntryLinks.Add(origin == EntryLinkOrigin.Manual
            ? EntryLink.CreateManual(fromEntryId, toKind, toId)
            : EntryLink.CreateDerived(fromEntryId, toKind, toId));
        await db.SaveChangesAsync();
    }

    private async Task<(int Entries, int Derived, int Manual, int Projects)> CountRowsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var entries = await db.ContextEntries.CountAsync();
        var derived = await db.EntryLinks.CountAsync(link => link.Origin == EntryLinkOrigin.Derived);
        var manual = await db.EntryLinks.CountAsync(link => link.Origin == EntryLinkOrigin.Manual);
        var projects = await db.Projects.CountAsync();
        return (entries, derived, manual, projects);
    }

    public sealed record ProjectJson(Guid Id);

    public sealed record EntryJson(Guid Id);

    public sealed record OutgoingJson(string Kind, Guid Id, string Label, string? Slug, Guid? ProjectId);

    public sealed record ResolveJson(
        List<OutgoingJson> Resolved,
        List<string> Unresolved,
        List<string> Self);
}
