using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class JournalEdgeWiringApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly EntryCommitProbe _probe = new();
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly Guid _projectId;
    private readonly Guid _journalId;

    public JournalEdgeWiringApiTests()
    {
        _factory = new ApiFactory(interceptor: _probe);
        _client = _factory.CreateClient();
        _projectId = CreateProject("wiring-project");
        _journalId = EnsureJournalProject();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Create_BodyWithThreeMarkers_CommitsThreeDerivedEdgesWithTheEntry()
    {
        var target = await CreateJournalNoteAsync("Anchor Note", "the target");
        var featureId = await CreateFeatureAsync("777-edge-wiring", "Edge wiring");

        var created = await CreateEntryAsync(_journalId, "journal", "Wiring Note",
            "links [[Anchor Note]] and [[feature:777]] and [[project:wiring-project]]");

        var rows = await DerivedRowsAsync(created);

        rows.Count.ShouldBe(3);
        rows.Single(row => row.Kind == "entry").Id.ShouldBe(target);
        rows.Single(row => row.Kind == "feature").Id.ShouldBe(featureId);
        rows.Single(row => row.Kind == "project").Id.ShouldBe(_projectId);

        (await GetDetailAsync(created)).Outgoing!.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Update_EditingMarkersOutOfTheBody_DropsExactlyThoseEdges()
    {
        var target = await CreateJournalNoteAsync("Anchor Note", "the target");
        var featureId = await CreateFeatureAsync("777-edge-wiring", "Edge wiring");
        var created = await CreateEntryAsync(_journalId, "journal", "Wiring Note",
            "links [[Anchor Note]] and [[feature:777]] and [[project:wiring-project]]");
        (await DerivedRowsAsync(created)).Count.ShouldBe(3);

        var trimmed = await _client.PutAsJsonAsync($"/api/entries/{created}", new
        {
            Title = "Wiring Note",
            Content = "links [[feature:777]] and [[project:wiring-project]]",
        });
        trimmed.StatusCode.ShouldBe(HttpStatusCode.OK);

        var afterTrim = await DerivedRowsAsync(created);
        afterTrim.Count.ShouldBe(2);
        afterTrim.ShouldNotContain(row => row.Id == target);
        afterTrim.ShouldContain(row => row.Id == featureId);
        (await GetDetailAsync(created)).Outgoing!.ShouldNotContain(row => row.Id == target);

        var bare = await _client.PutAsJsonAsync($"/api/entries/{created}", new
        {
            Title = "Wiring Note",
            Content = "no markers left here",
        });
        bare.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await DerivedRowsAsync(created)).ShouldBeEmpty();
        (await GetDetailAsync(created)).Outgoing!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_RecomputeFaultsBeforeCommit_LeavesNeitherBodyNorEdges()
    {
        await CreateJournalNoteAsync("Anchor Note", "the target");
        await CreateFeatureAsync("777-edge-wiring", "Edge wiring");
        var created = await CreateEntryAsync(_journalId, "journal", "Wiring Note", "links [[Anchor Note]]");
        (await DerivedRowsAsync(created)).ShouldHaveSingleItem();

        _probe.FaultNextLinksDelete = true;
        HttpResponseMessage? response = null;
        Exception? fault = null;
        try
        {
            response = await _client.PutAsJsonAsync($"/api/entries/{created}", new
            {
                Title = "Wiring Note",
                Content = "links [[feature:777]]",
            });
        }
        catch (Exception ex)
        {
            fault = ex;
        }

        _probe.Faults.ShouldBe(1);
        (fault is not null || response!.StatusCode == HttpStatusCode.InternalServerError)
            .ShouldBeTrue("the injected fault must reach the caller, not be swallowed by the endpoint");

        (await ReadContentAsync(created)).ShouldBe("links [[Anchor Note]]");
        var rows = await DerivedRowsAsync(created);
        rows.ShouldHaveSingleItem();
        rows.Single().Kind.ShouldBe("entry");
    }

    [Fact]
    public async Task Create_ReservedProjectLandsMidTransaction_CallerRetriesTheWholeUnitOfWork()
    {
        await CreateFeatureAsync("777-edge-wiring", "Edge wiring");
        await DeleteJournalContainerAsync();

        _probe.LandWinnerOnNextProjectInsert = true;
        _probe.Statements.Clear();

        var created = await CreateEntryAsync(_projectId, "note", "Container Birth",
            "mentions [[Some Note]] and [[feature:777]]");

        _probe.WinnerLandings.ShouldBe(1, string.Join(" || ", _probe.Statements));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        (await db.Projects.CountAsync(p => p.Name == JournalProjectStore.ProjectName)).ShouldBe(1);
        (await db.ContextEntries.CountAsync(e => e.ProjectId == _projectId)).ShouldBe(1);

        var rows = await DerivedRowsAsync(created);
        rows.ShouldHaveSingleItem();
        rows.Single().Kind.ShouldBe("feature");
    }

    [Fact]
    public async Task Create_TwoConcurrentFirstJournalWrites_BothSucceedOnOneContainer()
    {
        await DeleteJournalContainerAsync();

        var first = _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _projectId,
            Type = "note",
            Title = "Concurrent One",
            Content = "mentions [[Shared Title]]",
        });
        var second = _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _projectId,
            Type = "note",
            Title = "Concurrent Two",
            Content = "mentions [[Shared Title]]",
        });

        var pair = await Task.WhenAll(first, second);

        foreach (var response in pair)
        {
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.ShouldBe(HttpStatusCode.Created, body);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        (await db.Projects.CountAsync(p => p.Name == JournalProjectStore.ProjectName)).ShouldBe(1);
        (await db.ContextEntries.CountAsync(e => e.ProjectId == _projectId)).ShouldBe(2);
    }

    [Fact]
    public async Task Update_RecomputeTouchesEntryLinksOnlyAfterTheDerivedDelete()
    {
        await CreateJournalNoteAsync("Anchor Note", "the target");
        await CreateFeatureAsync("777-edge-wiring", "Edge wiring");
        var created = await CreateEntryAsync(_journalId, "journal", "Wiring Note", "links [[Anchor Note]]");

        _probe.Statements.Clear();
        var response = await _client.PutAsJsonAsync($"/api/entries/{created}", new
        {
            Title = "Wiring Note",
            Content = "links [[Anchor Note]] and [[feature:777]]",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var linkStatements = _probe.Statements
            .Where(statement => statement.Contains("\"EntryLinks\"", StringComparison.Ordinal))
            .ToArray();

        linkStatements.Length.ShouldBeGreaterThan(0, "the recompute must touch the edge table at all");
        linkStatements[0].ShouldContain("DELETE FROM", Case.Sensitive);
        linkStatements.Skip(1).ShouldContain(
            statement => statement.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Append_AddsAMarkerInTheFollowUp_AndTheEdgeLandsInTheSameCommit()
    {
        await CreateFeatureAsync("777-edge-wiring", "Edge wiring");
        var created = await CreateEntryAsync(_journalId, "journal", "Appended Note", "plain body");
        (await DerivedRowsAsync(created)).ShouldBeEmpty();

        var response = await _client.PostAsJsonAsync($"/api/entries/{created}/append", new { Text = "later [[feature:777]]" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rows = await DerivedRowsAsync(created);
        rows.ShouldHaveSingleItem();
        rows.Single().Kind.ShouldBe("feature");
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

    private Guid EnsureJournalProject()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<JournalProjectStore>()
            .EnsureIdAsync()
            .GetAwaiter()
            .GetResult();
    }

    private async Task DeleteJournalContainerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        await db.Projects
            .Where(p => p.Name == JournalProjectStore.ProjectName)
            .ExecuteDeleteAsync();
        (await db.Projects.CountAsync(p => p.Name == JournalProjectStore.ProjectName)).ShouldBe(0);
    }

    private async Task<Guid> CreateJournalNoteAsync(string title, string content) =>
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

    private async Task<List<DerivedRow>> DerivedRowsAsync(Guid entryId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();

        return await db.EntryLinks
            .AsNoTracking()
            .Where(link => link.FromEntryId == entryId && link.Origin == EntryLinkOrigin.Derived)
            .Select(link => new DerivedRow(link.ToKind.ToString().ToLowerInvariant(), link.ToId))
            .ToListAsync();
    }

    private async Task<string> ReadContentAsync(Guid entryId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .ContextEntries.AsNoTracking()
            .Where(e => e.Id == entryId)
            .Select(e => e.Content)
            .SingleAsync();
    }

    private async Task<EntryJson> GetDetailAsync(Guid id)
    {
        var detail = await _client.GetFromJsonAsync<EntryJson>($"/api/entries/{id}", JsonOptions);
        detail.ShouldNotBeNull();
        return detail!;
    }

    public sealed record ProjectJson(Guid Id);

    public sealed record DerivedRow(string Kind, Guid Id);

    public sealed record OutgoingJson(string Kind, Guid Id, string Label);

    public sealed record EntryJson(
        Guid Id,
        string Type,
        string Title,
        string Content,
        List<OutgoingJson>? Outgoing = null);
}

internal sealed class EntryCommitProbe : DbCommandInterceptor
{
    public List<string> Statements { get; } = [];

    public bool FaultNextLinksDelete;

    public bool LandWinnerOnNextProjectInsert;

    public int Faults;

    public int WinnerLandings;

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Observe(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Observe(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Observe(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Observe(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void Observe(DbCommand command)
    {
        Statements.Add(command.CommandText.Replace('\n', ' '));

        if (command.CommandText.Contains("DELETE FROM \"EntryLinks\"", StringComparison.Ordinal))
        {
            if (!FaultNextLinksDelete)
            {
                return;
            }

            FaultNextLinksDelete = false;
            Faults++;
            throw new InvalidOperationException("injected recompute failure inside the write transaction");
        }

        if (!LandWinnerOnNextProjectInsert ||
            !command.CommandText.Contains("INSERT INTO \"Projects\"", StringComparison.Ordinal))
        {
            return;
        }

        LandWinnerOnNextProjectInsert = false;
        WinnerLandings++;
        using var steal = new SqliteCommand(
            """
            INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt)
            VALUES (@id, 'Journal', '/tmp/probe-winner', 'the concurrent winner', @createdAt)
            """,
            (SqliteConnection)command.Connection,
            (SqliteTransaction?)command.Transaction);

        steal.Parameters.AddWithValue("@id", Guid.NewGuid().ToString().ToUpperInvariant());
        steal.Parameters.AddWithValue(
            "@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffff"));
        steal.ExecuteNonQuery();
    }
}
