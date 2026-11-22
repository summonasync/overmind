using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Host.Api;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// The promotion door as the owner meets it: one POST on the human prefix turns a note into a card, writes the manual
// edge that is the whole record of the act, and leaves the note's own text alone. The protected act is promotion of
// a journal note, so the agent-side half of this class attacks exactly that — the verbs an agent would need to drive
// triage itself, sent to routes that are really registered on the agent door, asserted by refusal code rather than
// by a 4xx class. Placement (spec R8) is pinned in AgentWriteSurfaceCoverageApiTests, not here.
public sealed class JournalTriagePromotionApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private Guid _boardProjectId;
    private Guid _journalProjectId;

    public JournalTriagePromotionApiTests() => _client = _factory.CreateClient();

    public Task InitializeAsync()
    {
        _boardProjectId = CreateProject("triage-board");
        _journalProjectId = EnsureJournalProject();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PromoteToFeature_CreatesTheCard_WritesTheManualEdge_LeavesTheNoteAlone()
    {
        var noteId = await CreateNoteAsync("Worth a card", "the owner's own words");

        var response = await _client.PostAsJsonAsync($"/api/entries/{noteId}/promote-to-feature", new
        {
            ProjectId = _boardProjectId,
            Title = "Promoted thought",
            Body = "<goal>Carried out of the pile.</goal>",
            Priority = "P1",
        });
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        var promotion = await response.Content.ReadFromJsonAsync<PromotionJson>(JsonOptions);
        promotion!.EntryId.ShouldBe(noteId);
        promotion.TargetKind.ShouldBe("feature");

        var stored = await FeatureByIdAsync(promotion.FeatureId);
        stored.ShouldNotBeNull();
        stored!.Title.ShouldBe("Promoted thought");
        stored.Priority.ShouldBe(FeaturePriority.P1);
        promotion.FeatureSlug.ShouldBe(stored.Slug);
        promotion.TargetId.ShouldBe(stored.Id);

        var board = await BoardAsync();
        board.ShouldContain(row => row.Slug == stored.Slug &&
                row.Priority.Equals("p1", StringComparison.OrdinalIgnoreCase),
            $"the card has to be on the board the owner reads, not only in the table; board = " +
            $"""{string.Join(" | ", board.Select(row => $"{row.Slug}/{row.Priority}"))}""");

        var edge = await PromotionEdgeAsync(noteId);
        edge.ToKind.ShouldBe(EntryLinkKind.Feature);
        edge.ToId.ShouldBe(promotion.TargetId);
        edge.Origin.ShouldBe(EntryLinkOrigin.Manual);

        (await NoteBodyAsync(noteId)).ShouldBe("the owner's own words");
        (await NoteIsInUnlinkedPileAsync(noteId)).ShouldBeFalse("the promotion edge is the triage state");
    }

    [Fact]
    public async Task PromoteToTask_EdgesTheTaskNotItsParent_AndNamesTheCardToInvalidate()
    {
        var feature = await CreateBoardFeatureAsync("010-host", "Board work");
        var noteId = await CreateNoteAsync("A sub-thought", "body");

        var response = await _client.PostAsJsonAsync($"/api/entries/{noteId}/promote-to-task", new
        {
            FeatureId = feature.Id,
            Body = "<goal>Spin-off of the note.</goal>",
            Brief = "From the journal pile",
        });
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        var promotion = await response.Content.ReadFromJsonAsync<PromotionJson>(JsonOptions);

        promotion!.TargetKind.ShouldBe("task");
        promotion.FeatureId.ShouldBe(feature.Id);
        promotion.FeatureSlug.ShouldBe("010-host");
        promotion.TargetId.ShouldNotBe(feature.Id);

        var edge = await PromotionEdgeAsync(noteId);
        edge.ToKind.ShouldBe(EntryLinkKind.Task);
        edge.ToId.ShouldBe(promotion.TargetId);

        var task = await ArtifactAsync(promotion.TargetId);
        task.Kind.ShouldBe("task");
        task.Brief.ShouldBe("From the journal pile");

        (await NoteBodyAsync(noteId)).ShouldBe("body");
    }

    [Fact]
    public async Task PromoteToFeature_TheNotesOwnOutgoingListsTheCardOnce()
    {
        var noteId = await CreateNoteAsync("Into the rails", "the owner's words");
        var before = await _client.GetFromJsonAsync<DetailViewJson>($"/api/entries/{noteId}", JsonOptions);

        before!.Outgoing.ShouldBeEmpty("the note names no card of its own before the act");

        var response = await _client.PostAsJsonAsync($"/api/entries/{noteId}/promote-to-feature", new
        {
            ProjectId = _boardProjectId,
            Title = "Rail borne thought",
            Body = "body",
            Priority = "P1",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var promotion = await response.Content.ReadFromJsonAsync<PromotionJson>(JsonOptions);

        var view = await _client.GetFromJsonAsync<DetailViewJson>($"/api/entries/{noteId}", JsonOptions);
        view.ShouldNotBeNull();

        view!.Outgoing.ShouldHaveSingleItem();
        var row = view.Outgoing[0];
        row.Kind.ShouldBe("feature");
        row.Id.ShouldBe(promotion!.TargetId);
        row.Label.ShouldBe("Rail borne thought");
        row.Slug.ShouldBe(promotion.FeatureSlug);
        row.ProjectId.ShouldBe(_boardProjectId);

        view.Content.ShouldBe("the owner's words");
        view.CurrentVersion.ShouldBe(before.CurrentVersion, "the promotion wrote an edge, not the note");
    }

    [Fact]
    public async Task PromoteToTask_TheNotesOwnOutgoingListsTheTaskAndItsFrame()
    {
        var feature = await CreateBoardFeatureAsync("021-rails-host", "Rails host");
        var briefed = await CreateNoteAsync("Briefed into rails", "body");

        var first = await _client.PostAsJsonAsync($"/api/entries/{briefed}/promote-to-task", new
        {
            FeatureId = feature.Id,
            Body = "body",
            Brief = "From the journal pile",
        });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var promoted = await first.Content.ReadFromJsonAsync<PromotionJson>(JsonOptions);

        var briefView = await _client.GetFromJsonAsync<DetailViewJson>($"/api/entries/{briefed}", JsonOptions);
        briefView!.Outgoing.ShouldHaveSingleItem();
        var briefRow = briefView.Outgoing[0];
        briefRow.Kind.ShouldBe("task");
        briefRow.Id.ShouldBe(promoted!.TargetId);
        briefRow.Label.ShouldBe("From the journal pile");
        briefRow.Slug.ShouldBe("021-rails-host");
        briefRow.ProjectId.ShouldBe(_boardProjectId);

        var bare = await CreateNoteAsync("Bare into rails", "body");
        var second = await _client.PostAsJsonAsync($"/api/entries/{bare}/promote-to-task", new
        {
            FeatureId = feature.Id,
            Body = "body",
        });
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var created = await second.Content.ReadFromJsonAsync<PromotionJson>(JsonOptions);

        var bareView = await _client.GetFromJsonAsync<DetailViewJson>($"/api/entries/{bare}", JsonOptions);
        var bareRow = bareView!.Outgoing.Single();
        bareRow.Kind.ShouldBe("task");
        bareRow.Id.ShouldBe(created!.TargetId);
        bareRow.Label.ShouldBe($"task {await ArtifactOrderAsync(created.TargetId):D2}");
        bareRow.Slug.ShouldBe("021-rails-host");
    }

    [Fact]
    public async Task PromoteTwice_Answers409NamingTheCardTheNoteAlreadyBecame()
    {
        var noteId = await CreateNoteAsync("Double click", "body");
        var payload = new
        {
            ProjectId = _boardProjectId,
            Title = "First promotion",
            Body = "body",
        };

        var first = await _client.PostAsJsonAsync($"/api/entries/{noteId}/promote-to-feature", payload);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var slug = (await first.Content.ReadFromJsonAsync<PromotionJson>(JsonOptions))!.FeatureSlug;

        var second = await _client.PostAsJsonAsync($"/api/entries/{noteId}/promote-to-feature", payload);
        var problem = await ReadProblemAsync(second);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        problem.Title.ShouldBe("TriageAlreadyPromoted");
        problem.Detail.ShouldContain(slug);

        (await BoardAsync()).ShouldHaveSingleItem().Slug.ShouldBe(slug);
        (await PromotionEdgesAsync(noteId)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task PromoteABuriedNote_Answers409ByCodeNotByStatusClass()
    {
        var noteId = await CreateNoteAsync("Lifted thought", "body");
        var lifted = await _client.PostAsJsonAsync($"/api/entries/{noteId}/lift", new { reason = "retracted" });
        lifted.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _client.PostAsJsonAsync($"/api/entries/{noteId}/promote-to-feature", new
        {
            ProjectId = _boardProjectId,
            Title = "Should not happen",
            Body = "body",
        });
        var problem = await ReadProblemAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        problem.Title.ShouldBe("TriageNoteNotPromotable");
        problem.Detail.ShouldContain(noteId.ToString());
    }

    [Theory]
    [InlineData("entry")]
    [InlineData("feature")]
    public async Task PromoteSomethingThatDoesNotExist_Answers404NamingTheMissingHalf(string missing)
    {
        var noteId = await CreateNoteAsync("Promotable note", "body");

        var response = missing == "entry"
            ? await _client.PostAsJsonAsync($"/api/entries/{Guid.NewGuid()}/promote-to-feature", new
            {
                ProjectId = _boardProjectId,
                Title = "Orphan ask",
                Body = "body",
            })
            : await _client.PostAsJsonAsync($"/api/entries/{noteId}/promote-to-task", new
            {
                FeatureId = Guid.NewGuid(),
                Body = "body",
            });

        var problem = await ReadProblemAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        problem.Title.ShouldBe(missing == "entry" ? "MissingEntry" : "MissingFeature");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        (await db.PlanningFeatures.AnyAsync(f => f.Title == "Orphan ask")).ShouldBeFalse();
    }

    [Fact]
    public async Task PromotionBody_IsValidatedBeforeTheDoorOpens()
    {
        var noteId = await CreateNoteAsync("Untyped ask", "body");

        var noTitle = await _client.PostAsJsonAsync($"/api/entries/{noteId}/promote-to-feature", new
        {
            ProjectId = _boardProjectId,
            Title = "",
            Body = "body",
        });
        noTitle.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var badPriority = await _client.PostAsJsonAsync($"/api/entries/{noteId}/promote-to-feature", new
        {
            ProjectId = _boardProjectId,
            Title = "Priority typo",
            Body = "body",
            Priority = "P9",
        });
        var problem = await ReadProblemAsync(badPriority);

        badPriority.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.Title.ShouldBe("InvalidFeaturePriority");
        problem.Detail.ShouldContain("P0");
    }

    [Fact]
    public async Task ManualTaskEdgeOntoASpecArtifact_NeitherBlocksPromotionNorCountsAsLinked()
    {
        // review-237-04 finding 1. The lift used to carry its own copy of the triage rule and its Task arm never
        // checked the artifact's kind, so this row — a manual Task edge sitting on a spec — made the note
        // simultaneously unlinked (the shared predicate ignores it) and unpromotable (the copy refused it). Both
        // halves must answer the same way, and the door is where the owner feels it.
        var host = await CreateBoardFeatureAsync("011-spec-host", "Spec host");
        var specArtifactId = await CreateSpecArtifactAsync(host.Slug);
        var noteId = await CreateNoteAsync("Note with a stray edge", "body");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            db.EntryLinks.Add(
                EntryLink.CreateManual(noteId, EntryLinkKind.Task, specArtifactId));
            await db.SaveChangesAsync();
        }

        (await NoteIsInUnlinkedPileAsync(noteId)).ShouldBeTrue(
            "an edge onto a non-task artifact is not a counting link — the shared definition says so");

        var response = await _client.PostAsJsonAsync($"/api/entries/{noteId}/promote-to-feature", new
        {
            ProjectId = _boardProjectId,
            Title = "Promoted past the stray edge",
            Body = "body",
        });
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        (await NoteIsInUnlinkedPileAsync(noteId)).ShouldBeFalse("the real promotion edge does count");
    }

    [Fact]
    public async Task TriagePromotionService_ResolvesFromTheHostContainer_NotAHandBuiltGraph()
    {
        // review-237-04 finding 3: the registration was read off DependencyInjection.cs and never exercised. The
        // endpoint tests prove handler injection; this proves the scoped lifetime the door depends on, through the
        // same container the Host pipeline was built from.
        using var first = _factory.Services.CreateScope();
        using var second = _factory.Services.CreateScope();

        var promoted = first.ServiceProvider.GetRequiredService<TriagePromotionService>();
        var again = second.ServiceProvider.GetRequiredService<TriagePromotionService>();

        promoted.ShouldNotBeNull();
        promoted.ShouldNotBe(again);
        first.ServiceProvider.GetRequiredService<TriagePromotionService>().ShouldBeSameAs(promoted);
    }

    [Theory]
    [InlineData("append")]
    [InlineData("update")]
    [InlineData("lift")]
    [InlineData("delete")]
    public async Task AgentDoor_NeededForForgingTriage_RefusesByTheTypeCode_AndTheNoteStaysOwnersOnly(
        string verb)
    {
        // The protected act is promotion of a journal note. An agent that wanted to fake it would first have to
        // write the note — mark it sorted, retitle it, bury it. Every one of those lands on a registered agent
        // route and is refused by JournalEntryWriteRefused, not by a bare 4xx and not by the completeness-rule
        // refusal (AgentEntryWriteShapeUndetermined), which is a different reason and must not be conflated.
        var noteId = await CreateNoteAsync("Owner's triage state", "not yet sorted");

        var (method, path, payload) = verb switch
        {
            "append" => ("POST", $"/api/agent/entries/{noteId}/append", """{"text":"[promoted to 012-forged]"}"""),
            "update" => ("PUT", $"/api/agent/entries/{noteId}", """{"title":"Sorted","content":"promoted"}"""),
            "lift" => ("POST", $"/api/agent/entries/{noteId}/lift", """{"reason":"sorted elsewhere"}"""),
            _ => ("DELETE", $"/api/agent/entries/{noteId}", "{}"),
        };

        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
        };
        var response = await _client.SendAsync(request);
        var detail = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"{method} {path} -> {detail}");
        var problem = JsonSerializer.Deserialize<ProblemJson>(detail, JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("JournalEntryWriteRefused", $"{method} {path} refused for the wrong reason");
        (await NoteBodyAsync(noteId)).ShouldBe("not yet sorted");
        (await PromotionEdgesAsync(noteId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task AgentDoor_CanCreateBoardWork_ButCannotDriveTriage()
    {
        // Spec R8's second acceptance half, named: agents create features through the planning door all day. What
        // they cannot do is turn the owner's note into one — no promotion edge, and the note stays in the pile.
        var noteId = await CreateNoteAsync("A thought an agent must not sort", "body");

        var created = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{_boardProjectId}/planning/features",
            new { Title = "A thought an agent must not sort", Body = "body" });
        var location = await created.Content.ReadAsStringAsync();

        created.StatusCode.ShouldBe(HttpStatusCode.Created, location);
        (await PromotionEdgesAsync(noteId)).ShouldBeEmpty();
        (await NoteIsInUnlinkedPileAsync(noteId)).ShouldBeTrue();

        var forged = await _client.PostAsJsonAsync($"/api/entries/{noteId}/promote-to-feature", new
        {
            ProjectId = _boardProjectId,
            Title = "Agent's own promotion",
            Body = "body",
        });
        forged.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PromotionEdgesAsync(noteId)).ShouldHaveSingleItem();
    }

    private async Task<bool> NoteIsInUnlinkedPileAsync(Guid noteId)
    {
        var rows = await _client.GetFromJsonAsync<List<EntryRowJson>>(
            $"/api/projects/{_journalProjectId}/entries?link=unlinked", JsonOptions);
        return rows!.Any(row => row.Id == noteId);
    }

    private async Task<string> NoteBodyAsync(Guid noteId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .ContextEntries.AsNoTracking()
            .Where(entry => entry.Id == noteId)
            .Select(entry => entry.Content)
            .SingleAsync();
    }

    private async Task<EntryLink> PromotionEdgeAsync(Guid noteId)
    {
        var edges = await PromotionEdgesAsync(noteId);
        return edges.ShouldHaveSingleItem();
    }

    private async Task<List<EntryLink>> PromotionEdgesAsync(Guid noteId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .EntryLinks.AsNoTracking()
            .Where(link => link.FromEntryId == noteId && link.Origin == EntryLinkOrigin.Manual)
            .ToListAsync();
    }

    // The board addresses features by slug and carries no id on the wire, so the fixture reads the row back from the
    // same database the door wrote — the promotion's FeatureId is a guid, and the door is what created the row.
    private async Task<List<BoardRow>> BoardAsync() =>
        await _client.GetFromJsonAsync<List<BoardRow>>(
            $"/api/projects/{_boardProjectId}/planning/features", JsonOptions) ?? [];

    private async Task<PlanningFeature?> FeatureByIdAsync(Guid featureId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .PlanningFeatures.AsNoTracking()
            .FirstOrDefaultAsync(feature => feature.Id == featureId);
    }

    private async Task<ArtifactRowJson> ArtifactAsync(Guid artifactId)
    {
        var artifact = await _client.GetFromJsonAsync<ArtifactRowJson>(
            $"/api/planning/artifacts/{artifactId}", JsonOptions);
        artifact.ShouldNotBeNull();
        return artifact!;
    }

    private async Task<PlanningFeature> CreateBoardFeatureAsync(string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_boardProjectId}/planning/features",
            new { Slug = slug, Title = title, Body = "body" });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Created, body);

        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .PlanningFeatures.AsNoTracking()
            .SingleAsync(feature => feature.ProjectId == _boardProjectId && feature.Slug == slug);
    }

    private async Task<Guid> CreateSpecArtifactAsync(string slug)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_boardProjectId}/planning/features/{slug}/artifacts",
            new { Kind = "spec", Body = "The frozen what." });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Created, body);

        var id = Element(body).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        (await db.PlanningArtifacts.Where(a => a.Id == id).Select(a => a.Kind)
            .SingleAsync()).ShouldBe(PlanningArtifactKind.Spec,
            "the fixture must really hand back a non-task artifact, or the case proves nothing");

        return id;
    }

    private async Task<Guid> CreateNoteAsync(string title, string content)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _journalProjectId,
            Type = "journal",
            Title = title,
            Content = content,
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Created, body);

        return Element(body).GetProperty("id").GetGuid();
    }

    private Guid CreateProject(string name)
    {
        var response = _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
        }).GetAwaiter().GetResult();

        response.EnsureSuccessStatusCode();
        return Element(response.Content.ReadAsStringAsync().GetAwaiter().GetResult()).GetProperty("id").GetGuid();
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

    private static JsonElement Element(string body) => JsonDocument.Parse(body).RootElement;

    private static async Task<ProblemJson> ReadProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        return problem!;
    }

    private sealed record PromotionJson(
        Guid EntryId, string TargetKind, Guid TargetId, Guid FeatureId, string FeatureSlug);

    private sealed record BoardRow(string Slug, string Title, string Priority);

    private sealed record ArtifactRowJson(string Kind, string? Brief);

    private sealed record EntryRowJson(Guid Id);

    private sealed record ProblemJson(string? Title, string? Detail);

    private sealed record OutgoingView(string Kind, Guid Id, string Label, string? Slug, Guid? ProjectId);

    private sealed record DetailViewJson(
        string Title,
        string Content,
        int CurrentVersion,
        List<OutgoingView> Outgoing);

    private async Task<int> ArtifactOrderAsync(Guid artifactId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .PlanningArtifacts.AsNoTracking()
            .Where(artifact => artifact.Id == artifactId)
            .Select(artifact => artifact.Order)
            .SingleAsync();
    }
}

// NON-VACUITY (contract AC 3). Two injections, both recorded in ~/scratch-237 and reverted:
//
// 1. THE GATE ITSELF. EntriesApi.MapEntriesApi: drop .AddEndpointFilter(JournalWriteLockFilter.Instance) from the
//    agentEntryWrites group. AgentDoor_NeededForForgingTriage_... goes red on every verb — the same requests now
//    reach the handlers and rewrite/bury the note, which is what proves the first green was the guard and not a
//    missing route. Restoring the filter returns the class to green.
//
// 2. THE SHARED-TRIAGE LIFT. TriagePromotionService.RequireNotPromotedAsync back to its own PlanningArtifacts.Any
//    lookup without the Task-kind conjunct. ManualTaskEdgeOntoASpecArtifact_... goes red with 409
//    TriageAlreadyPromoted while the note still reads as unlinked — the two-copy drift the finding named.
//
