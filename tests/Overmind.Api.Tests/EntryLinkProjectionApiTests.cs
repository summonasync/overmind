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

public sealed class EntryLinkProjectionApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Guid _projectId;
    private readonly Guid _journalId;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public EntryLinkProjectionApiTests()
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
    public async Task Detail_LinkedNote_ReturnsOutgoingIncomingAndUnresolved()
    {
        var beta = await CreateJournalAsync("Beta Note", "beta body");
        var featureId = await CreateFeatureAsync("777-read-projection", "Read projection");
        var alpha = await CreateJournalAsync(
            "Alpha Note",
            $"Mentions [[Beta Note]] and [[feature:777]] and [[project:links-project]] but [[Nowhere]] exists.");
        await RecomputeAsync(alpha);

        var detail = await GetDetailAsync(alpha);

        detail.Outgoing.ShouldContain(link =>
            link.Kind == "entry" && link.Id == beta && link.Label == "Beta Note");
        detail.Outgoing.ShouldContain(link =>
            link.Kind == "feature" && link.Id == featureId && link.Label == "Read projection");
        detail.Outgoing.ShouldContain(link =>
            link.Kind == "project" && link.Id == _projectId && link.Label == "links-project");
        detail.Unresolved.ShouldBe(["Nowhere"]);

        var gamma = await CreateJournalAsync("Gamma Note", "reads [[Alpha Note]] too");
        await RecomputeAsync(gamma);

        var withBacklink = await GetDetailAsync(alpha);
        withBacklink.Incoming.ShouldHaveSingleItem();
        withBacklink.Incoming[0].EntryId.ShouldBe(gamma);
        withBacklink.Incoming[0].Title.ShouldBe("Gamma Note");
        withBacklink.Incoming[0].Snippet.ShouldContain("[[Alpha Note]]");
    }

    [Fact]
    public async Task Detail_FencedAndInlineMarkers_AreNeitherOutgoingNorUnresolved()
    {
        var beta = await CreateJournalAsync("Beta Note", "beta body");
        var alpha = await CreateJournalAsync(
            "Alpha Note",
            "```\n[[Fenced Note]]\n```\nQuote `[[Inline Note]]` then link [[Beta Note]].");
        await RecomputeAsync(alpha);

        var detail = await GetDetailAsync(alpha);

        detail.Outgoing.ShouldHaveSingleItem();
        detail.Outgoing[0].Id.ShouldBe(beta);
        detail.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public async Task Detail_BacklinkSnippet_TrimsToTheMarkerWindow()
    {
        var target = await CreateJournalAsync("Anchor Note", "target");
        var padding = new string('x', 400);
        var source = await CreateJournalAsync(
            "Padding Note",
            $"{padding}\nsecond paragraph mentioning [[Anchor Note]] in the middle\n{padding}");
        await RecomputeAsync(source);

        var detail = await GetDetailAsync(target);

        var snippet = detail.Incoming.Single().Snippet;
        snippet.ShouldContain("[[Anchor Note]]");
        snippet.Length.ShouldBeLessThan(padding.Length * 2);
        snippet.ShouldStartWith("...");
        snippet.ShouldEndWith("...");
    }

    [Fact]
    public async Task Detail_AfterTargetRename_MovesTheLinkIntoUnresolved()
    {
        var beta = await CreateJournalAsync("Beta Note", "beta body");
        var alpha = await CreateJournalAsync("Alpha Note", "links [[Beta Note]] here");
        await RecomputeAsync(alpha);

        (await GetDetailAsync(alpha)).Outgoing.ShouldContain(link => link.Id == beta);

        var rename = await _client.PutAsJsonAsync($"/api/entries/{beta}", new
        {
            Title = "Gamma Note",
            Content = "beta body",
        });
        rename.StatusCode.ShouldBe(HttpStatusCode.OK);

        var reSave = await _client.PutAsJsonAsync($"/api/entries/{alpha}", new
        {
            Title = "Alpha Note",
            Content = "links [[Beta Note]] here",
        });
        reSave.StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = await GetDetailAsync(alpha);
        after.Outgoing.ShouldNotContain(link => link.Id == beta);
        after.Unresolved.ShouldBe(["Beta Note"]);
    }

    [Fact]
    public async Task Detail_SelfLink_IsNeitherOutgoingNorUnresolved()
    {
        var alpha = await CreateJournalAsync("Alpha Note", "echoes [[Alpha Note]] itself");
        await RecomputeAsync(alpha);

        var detail = await GetDetailAsync(alpha);

        detail.Outgoing.ShouldBeEmpty();
        detail.Unresolved.ShouldBeEmpty();
        detail.Incoming.ShouldBeEmpty();
    }

    [Fact]
    public async Task Detail_DanglingTargetRow_RendersNeitherLiveNorInvented()
    {
        var beta = await CreateJournalAsync("Beta Note", "beta body");
        var alpha = await CreateJournalAsync("Alpha Note", "links [[Beta Note]] here");
        await RecomputeAsync(alpha);
        (await GetDetailAsync(alpha)).Outgoing.ShouldContain(link => link.Id == beta);

        (await _client.DeleteAsync($"/api/entries/{beta}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var deadFeature = Guid.NewGuid();
        await InsertDanglingRowAsync(alpha, deadFeature);

        var detail = await GetDetailAsync(alpha);

        detail.Outgoing.ShouldBeEmpty();
        detail.Unresolved.ShouldBe(["Beta Note"]);
    }

    [Fact]
    public async Task Detail_Incoming_ExcludesLiftedSupersededAndTombstonedSources()
    {
        var target = await CreateJournalAsync("Anchor Note", "target");
        var active = await CreateJournalAsync("Active Source", "links [[Anchor Note]]");
        var lifted = await CreateJournalAsync("Lifted Source", "links [[Anchor Note]]");
        var superseded = await CreateJournalAsync("Superseded Source", "links [[Anchor Note]]");
        var buried = await CreateJournalAsync("Buried Source", "links [[Anchor Note]]");

        foreach (var source in new[] { active, lifted, superseded, buried })
        {
            await RecomputeAsync(source);
        }

        await _client.PostAsJsonAsync(
            $"/api/entries/{lifted}/lift",
            new { Reason = "reconsidered", Proof = (string?)null, SupersededById = (Guid?)null });

        await _client.PostAsJsonAsync(
            $"/api/entries/{superseded}/lift",
            new { Reason = "replaced", Proof = (string?)null, SupersededById = active });

        await _client.DeleteAsync($"/api/entries/{buried}");

        var detail = await GetDetailAsync(target);

        detail.Incoming.ShouldHaveSingleItem();
        detail.Incoming[0].EntryId.ShouldBe(active);
    }

    [Fact]
    public async Task ListByProject_TypeJournal_ReturnsOnlyJournalEntries()
    {
        await CreateJournalAsync("First Note", "one");
        await CreateJournalAsync("Second Note", "two");
        await CreateEntryAsync(_projectId, "note", "Plain note", "not journal");

        var listed = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_journalId}/entries?type=journal", JsonOptions);

        listed.ShouldNotBeNull();
        listed!.Select(e => e.Title).OrderBy(t => t, StringComparer.Ordinal).ShouldBe(
            ["First Note", "Second Note"]);
        listed.ShouldAllBe(e => e.Type == "journal");
    }

    [Fact]
    public async Task Detail_PlainNote_ReturnsEmptyLinkCollectionsAndExistingFields()
    {
        var note = await CreateEntryAsync(_projectId, "note", "Plain note", "no markers here");

        var detail = await GetDetailAsync(note);

        detail.Id.ShouldBe(note);
        detail.Title.ShouldBe("Plain note");
        detail.Content.ShouldBe("no markers here");
        detail.Type.ShouldBe("note");
        detail.Outgoing.ShouldBeEmpty();
        detail.Incoming.ShouldBeEmpty();
        detail.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_ResponsesKeepLinkFieldsEmpty()
    {
        await CreateJournalAsync("Alpha Note", "links [[Nowhere]]");

        var listed = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_journalId}/entries?type=journal", JsonOptions);

        listed.ShouldNotBeNull();
        listed!.ShouldHaveSingleItem();
        listed[0].Outgoing.ShouldBeEmpty();
        listed[0].Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public async Task Detail_StoredDerivedEdgeWithNoMarker_StillProjectsNothing()
    {
        // The second outgoing source is the door's manual edge, not the table in general: a derived row the
        // note's own text does not name stays invisible to the rails, exactly as it was before this source
        // existed. A reads-all-edges projection would resurrect markers the owner deleted.
        var featureId = await CreateFeatureAsync("770-derived-only", "Derived only");
        var note = await CreateJournalAsync("Derived Only Note", "no marker at all");
        await InsertDerivedRowAsync(note, featureId);

        var detail = await GetDetailAsync(note);

        detail.Outgoing.ShouldBeEmpty();
        detail.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public async Task Detail_ManualEdgeToAFeature_ListsItOnceWithItsTitle()
    {
        var featureId = await CreateFeatureAsync("771-manual-edge", "Manual edge");
        var note = await CreateJournalAsync("Manual Edge Note", "plain body, no marker");
        await InsertManualRowAsync(note, EntryLinkKind.Feature, featureId);

        var detail = await GetDetailAsync(note);

        detail.Outgoing.ShouldHaveSingleItem();
        detail.Outgoing[0].Kind.ShouldBe("feature");
        detail.Outgoing[0].Id.ShouldBe(featureId);
        detail.Outgoing[0].Label.ShouldBe("Manual edge");
    }

    [Fact]
    public async Task Detail_MarkerAndManualEdgeOnTheSameTarget_CollapseIntoOneRow()
    {
        var featureId = await CreateFeatureAsync("772-both-sources", "Both sources");
        var note = await CreateJournalAsync("Both Sources Note", "plain body");
        await InsertManualRowAsync(note, EntryLinkKind.Feature, featureId);

        // the write recomputes the derived side of the very pair the door had already claimed,
        // and the writer keeps the manual row — so the marker and the stored edge name one target
        var rewritten = await _client.PutAsJsonAsync($"/api/entries/{note}", new
        {
            Title = "Both Sources Note",
            Content = "now reads [[feature:772-both-sources]]",
        });
        rewritten.StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await GetDetailAsync(note);

        detail.Outgoing.Count.ShouldBe(1, "one pair, one row — the rails must not show it twice");
        detail.Outgoing[0].Id.ShouldBe(featureId);
        detail.Outgoing[0].Label.ShouldBe("Both sources");
    }

    [Fact]
    public async Task Detail_ManualEdgeToGoneOrBuriedTarget_ContributesNothing()
    {
        var note = await CreateJournalAsync("Buried Edge Note", "plain body");

        await InsertManualRowAsync(note, EntryLinkKind.Feature, Guid.NewGuid());

        var buried = await CreateJournalAsync("Buried Target", "target body");
        await InsertManualRowAsync(note, EntryLinkKind.Entry, buried);
        (await _client.DeleteAsync($"/api/entries/{buried}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var lifted = await CreateJournalAsync("Lifted Target", "lifted body");
        await InsertManualRowAsync(note, EntryLinkKind.Entry, lifted);
        (await _client.PostAsJsonAsync(
            $"/api/entries/{lifted}/lift",
            new { Reason = "reconsidered", Proof = (string?)null, SupersededById = (Guid?)null }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await GetDetailAsync(note);

        detail.Outgoing.ShouldBeEmpty();
    }

    [Fact]
    public async Task Detail_KeepsEveryRow_MarkersFirstThenStoredEdges()
    {
        var beta = await CreateJournalAsync("Beta Note", "beta");
        var gamma = await CreateJournalAsync("Gamma Note", "gamma");
        var firstFeature = await CreateFeatureAsync("773-order-probe", "Ordered first");
        var note = await CreateJournalAsync(
            "Ordering Note",
            "first [[Beta Note]] then [[Gamma Note]] and finally [[feature:773-order-probe]]");
        await RecomputeAsync(note);

        var secondFeature = await CreateFeatureAsync("774-second-edge", "Ordered second");
        var delta = await CreateJournalAsync("Delta Note", "delta");
        await InsertManualRowAsync(note, EntryLinkKind.Feature, secondFeature);
        await InsertManualRowAsync(note, EntryLinkKind.Entry, delta);

        var detail = await GetDetailAsync(note);

        detail.Outgoing.Count.ShouldBe(5, "the outgoing view has no cap to overflow");
        detail.Outgoing[0].Id.ShouldBe(beta);
        detail.Outgoing[1].Id.ShouldBe(gamma);
        detail.Outgoing[2].Id.ShouldBe(firstFeature);
        detail.Outgoing[3].Id.ShouldBe(secondFeature);
        detail.Outgoing[4].Id.ShouldBe(delta);
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

    private async Task<Guid> CreateJournalAsync(string title, string content) =>
        await CreateEntryAsync(_journalId, "journal", title, content);

    private async Task<Guid> CreateEntryAsync(Guid projectId, string type, string title, string content)
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

    private async Task RecomputeAsync(Guid sourceId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var body = await db.ContextEntries.AsNoTracking()
            .Where(e => e.Id == sourceId)
            .Select(e => e.Content)
            .SingleAsync();

        await using var transaction = await db.Database.BeginTransactionAsync();
        await scope.ServiceProvider.GetRequiredService<EntryLinkWriter>()
            .ReplaceDerivedAsync(sourceId, WikiLinkParser.Parse(body), transaction);
        await transaction.CommitAsync();
    }

    private async Task InsertDanglingRowAsync(Guid fromEntryId, Guid toId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        db.EntryLinks.Add(EntryLink.CreateDerived(fromEntryId, EntryLinkKind.Feature, toId));
        await db.SaveChangesAsync();
    }

    private async Task InsertManualRowAsync(Guid fromEntryId, EntryLinkKind toKind, Guid toId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        db.EntryLinks.Add(EntryLink.CreateManual(fromEntryId, toKind, toId));
        await db.SaveChangesAsync();
    }

    private async Task InsertDerivedRowAsync(Guid fromEntryId, Guid toFeatureId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        db.EntryLinks.Add(EntryLink.CreateDerived(fromEntryId, EntryLinkKind.Feature, toFeatureId));
        await db.SaveChangesAsync();
    }

    private async Task<EntryJson> GetDetailAsync(Guid id)
    {
        var detail = await _client.GetFromJsonAsync<EntryJson>($"/api/entries/{id}", JsonOptions);
        detail.ShouldNotBeNull();
        return detail!;
    }

    public sealed record ProjectJson(Guid Id);

    public sealed record OutgoingJson(string Kind, Guid Id, string Label);

    public sealed record BacklinkJson(Guid EntryId, string Title, string Snippet);

    public sealed record EntryJson(
        Guid Id,
        string Type,
        string Title,
        string Content,
        List<OutgoingJson> Outgoing,
        List<BacklinkJson> Incoming,
        List<string> Unresolved);
}
