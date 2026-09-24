using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Search;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class FtsUnionSearchTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly Guid _projectId;
    private readonly Guid _otherProjectId;

    public FtsUnionSearchTests()
    {
        using var ctx = _db.CreateContext();
        var project = Project.Create($"union-main-{Guid.NewGuid():N}", "/tmp/union-main");
        var other = Project.Create($"union-other-{Guid.NewGuid():N}", "/tmp/union-other");
        ctx.Projects.AddRange(project, other);
        _projectId = project.Id;
        _otherProjectId = other.Id;
        ctx.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Union_SharedTerm_FindsEntriesFeaturesAndArtifacts()
    {
        await SeedEntryAsync("marmot enclosure", "the marmot burrows deep");
        await SeedFeatureAsync("101-marmot-feature", "Marmot feature", "feature body mentions marmot once");
        var featureId = await SeedFeatureAsync("101-marmot-spec-host", "Spec host", "host body stays quiet");
        await SeedArtifactAsync(featureId, PlanningArtifactKind.Spec, "spec body talks about marmot too");

        var hits = await UnionAsync("marmot");

        hits.Count.ShouldBe(3);
        hits.Count(h => h is EntryFulltextHit).ShouldBe(1);
        hits.Count(h => h is FeatureFulltextHit).ShouldBe(1);
        hits.Count(h => h is ArtifactFulltextHit).ShouldBe(1);
    }

    [Fact]
    public async Task Union_InterleavePerSourceRankSlots_EntriesFeaturesArtifactsPrecedence()
    {
        await SeedEntryAsync("marmot one", "first marmot entry body");
        await SeedEntryAsync("marmot two", "second marmot entry body");
        await SeedFeatureAsync("102-marmot-alpha", "Alpha", "alpha feature marmot body");
        await SeedFeatureAsync("102-marmot-beta", "Beta", "beta feature marmot body");
        var host = await SeedFeatureAsync("102-marmot-host", "Host", "host body");
        await SeedArtifactAsync(host, PlanningArtifactKind.Spec, "alpha artifact marmot body");
        await SeedArtifactAsync(host, PlanningArtifactKind.Plan, "beta artifact marmot body");

        var hits = await UnionAsync("marmot", limit: 10);

        hits.Count.ShouldBe(6);
        hits.Select(h => h.GetType().Name).ShouldBe(
        [
            nameof(EntryFulltextHit),
            nameof(FeatureFulltextHit),
            nameof(ArtifactFulltextHit),
            nameof(EntryFulltextHit),
            nameof(FeatureFulltextHit),
            nameof(ArtifactFulltextHit)
        ]);
    }

    [Fact]
    public async Task Union_LimitAppliesAfterMerge_NotPerSource()
    {
        await SeedEntryAsync("marmot one", "first marmot entry body");
        await SeedEntryAsync("marmot two", "second marmot entry body");
        var host = await SeedFeatureAsync("103-marmot-host", "Host", "host body");
        await SeedArtifactAsync(host, PlanningArtifactKind.Spec, "alpha artifact marmot body");
        await SeedArtifactAsync(host, PlanningArtifactKind.Plan, "beta artifact marmot body");

        var hits = await UnionAsync("marmot", limit: 3);

        hits.Count.ShouldBe(3);
        hits.Select(h => h.GetType().Name).ShouldBe(
        [
            nameof(EntryFulltextHit),
            nameof(ArtifactFulltextHit),
            nameof(EntryFulltextHit)
        ]);
    }

    [Fact]
    public async Task Union_WithinSource_Bm25RankOrderIsPreserved()
    {
        var host = await SeedFeatureAsync("104-density-host", "Host", "host body");
        await SeedArtifactAsync(host, PlanningArtifactKind.Spec, "sparse body mentions marmot");
        await SeedArtifactAsync(host, PlanningArtifactKind.Plan, "dense body marmot marmot marmot mention");
        await SeedArtifactAsync(host, PlanningArtifactKind.Result, "middle body marmot marmot mention");

        var hits = (await UnionAsync("marmot")).OfType<ArtifactFulltextHit>().ToArray();

        hits.Length.ShouldBe(3);
        for (var i = 1; i < hits.Length; i++)
        {
            hits[i].Rank.ShouldBeGreaterThanOrEqualTo(hits[i - 1].Rank);
        }
    }

    [Fact]
    public async Task Union_ProjectFilter_ScopesFeaturesAndArtifactsToProject()
    {
        var featureId = await SeedFeatureAsync(
            "105-capybara-feature", "Capybara feature", "feature body mentions capybara", _otherProjectId);
        await SeedArtifactAsync(featureId, PlanningArtifactKind.Task, "task body mentions capybara", order: 1);

        (await UnionAsync("capybara", projectId: _projectId)).ShouldBeEmpty();

        var scoped = await UnionAsync("capybara", projectId: _otherProjectId);
        scoped.Count.ShouldBe(2);
        scoped.ShouldAllBe(h => h is FeatureFulltextHit || h is ArtifactFulltextHit);
    }

    [Fact]
    public async Task Union_FeatureHit_CarriesSlugTitleAndSnippet()
    {
        await SeedFeatureAsync("106-otter-feature", "Otter feature", "otters groom their fur meticulously");

        var hit = (await UnionAsync("groom")).ShouldHaveSingleItem();

        var feature = hit.ShouldBeOfType<FeatureFulltextHit>();
        feature.FeatureSlug.ShouldBe("106-otter-feature");
        feature.Title.ShouldBe("Otter feature");
        feature.Snippet.ShouldContain("groom");
        feature.Snippet.ShouldContain("<mark>");
    }

    [Fact]
    public async Task Union_ArtifactHit_CarriesIdsKindOrderAndFeatureNumberedLabel()
    {
        var featureId = await SeedFeatureAsync("039-label-probe", "Label probe", "host body stays quiet");
        var taskId = await SeedArtifactAsync(
            featureId, PlanningArtifactKind.Task, "the pangolin rolls tight", order: 7);
        await SeedArtifactAsync(featureId, PlanningArtifactKind.Spec, "spec talks about quolls", order: 0);

        var taskHit = (await UnionAsync("pangolin")).ShouldHaveSingleItem().ShouldBeOfType<ArtifactFulltextHit>();
        taskHit.ArtifactId.ShouldBe(taskId);
        taskHit.FeatureSlug.ShouldBe("039-label-probe");
        taskHit.Kind.ShouldBe(PlanningArtifactKind.Task);
        taskHit.Order.ShouldBe(7);
        taskHit.Label.ShouldBe("039 task");
        taskHit.Snippet.ShouldContain("pangolin");
        taskHit.Snippet.ShouldContain("<mark>");

        var specHit = (await UnionAsync("quolls")).ShouldHaveSingleItem().ShouldBeOfType<ArtifactFulltextHit>();
        specHit.Label.ShouldBe("039 spec");
    }

    [Fact]
    public async Task Union_EntryHit_MatchesEntriesOnlySearchShape()
    {
        var entryId = await SeedEntryAsync("puffin notes", "the puffin nests on cliffs");

        var unionHit = (await UnionAsync("puffin")).ShouldHaveSingleItem().ShouldBeOfType<EntryFulltextHit>();
        var entryHit = (await EntriesAsync("puffin")).ShouldHaveSingleItem();

        unionHit.EntryId.ShouldBe(entryId);
        unionHit.Title.ShouldBe("puffin notes");
        unionHit.Type.ShouldBe(EntryType.Note);
        unionHit.Rank.ShouldBe(entryHit.Rank);
        unionHit.Snippet.ShouldBe(entryHit.Snippet);
    }

    private async Task<Guid> SeedEntryAsync(string title, string content, Guid? projectId = null)
    {
        using var ctx = _db.CreateContext();
        var entry = ContextEntry.Create(projectId ?? _projectId, EntryType.Note, title, content);
        ctx.ContextEntries.Add(entry);
        await ctx.SaveChangesAsync();
        return entry.Id;
    }

    private async Task<Guid> SeedFeatureAsync(string slug, string title, string body, Guid? projectId = null)
    {
        using var ctx = _db.CreateContext();
        var feature = PlanningFeature.Create(projectId ?? _projectId, slug, title, body);
        ctx.PlanningFeatures.Add(feature);
        await ctx.SaveChangesAsync();
        return feature.Id;
    }

    private async Task<Guid> SeedArtifactAsync(
        Guid featureId, PlanningArtifactKind kind, string body, int order = 0)
    {
        using var ctx = _db.CreateContext();
        var artifact = PlanningArtifact.Create(featureId, kind, body, order);
        ctx.PlanningArtifacts.Add(artifact);
        await ctx.SaveChangesAsync();
        return artifact.Id;
    }

    private async Task<IReadOnlyList<FulltextHit>> UnionAsync(
        string query, Guid? projectId = null, int limit = 10)
    {
        await using var ctx = _db.CreateContext();
        var search = new FtsSearchService(ctx);
        return await search.SearchUnionAsync(projectId, query, limit, CancellationToken.None);
    }

    private async Task<IReadOnlyList<EntrySearchHit>> EntriesAsync(
        string query, Guid? projectId = null, int limit = 10)
    {
        await using var ctx = _db.CreateContext();
        var search = new FtsSearchService(ctx);
        return await search.SearchAsync(projectId, query, limit, CancellationToken.None);
    }
}
