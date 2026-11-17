using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// Spec R2 at the human door: the first journal note on an empty database has no container id to name, and the
// container has to come out of the same write the note does. These are the contract tests for that bootstrap.
public sealed class JournalCaptureBootstrapApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WriteCapture _capture = new();
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public JournalCaptureBootstrapApiTests()
    {
        _factory = new ApiFactory(interceptor: _capture);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task HumanDoor_FirstJournalNoteWithoutProjectId_BirthsOneContainerInOneTransaction()
    {
        _capture.Clear();
        var created = await CaptureJson(await PostHumanJournalAsync("First thought", "a note with no destination"));

        created.Type.ShouldBe("journal");
        created.ProjectId.ShouldNotBe(Guid.Empty);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var container = await db.Projects.SingleAsync(p => p.Name == JournalProjectStore.ProjectName);
        container.Id.ShouldBe(created.ProjectId);
        container.DiskPath.ShouldNotBeEmpty(
            "the server derives the reserved row's path from its own database file, the client never sent one");
        (await db.ContextEntries.CountAsync(e => e.ProjectId == container.Id)).ShouldBe(1);

        var writes = _capture.Statements
            .Where(statement => statement.Sql.Contains("INSERT INTO \"Projects\"", StringComparison.Ordinal)
                || statement.Sql.Contains("INSERT INTO \"ContextEntries\"", StringComparison.Ordinal))
            .ToArray();

        writes.Length.ShouldBe(2, $"container and note, nothing else: {Describe(writes)}");
        writes.ShouldAllBe(write => write.Transaction != null);
        ReferenceEquals(writes[0].Transaction, writes[1].Transaction).ShouldBeTrue(
            "the container is born inside the note's transaction, not in a second one");
    }

    [Fact]
    public async Task HumanDoor_SecondJournalNoteWithoutProjectId_ReusesTheContainer()
    {
        var first = await CaptureJson(await PostHumanJournalAsync("First thought", "one"));
        var second = await CaptureJson(await PostHumanJournalAsync("Second thought", "two"));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        second.ProjectId.ShouldBe(first.ProjectId);
        (await db.Projects.CountAsync(p => p.Name == JournalProjectStore.ProjectName)).ShouldBe(1);
        (await db.Projects.CountAsync()).ShouldBe(1, "the bootstrap mints the container and nothing beside it");
        (await db.ContextEntries.CountAsync(e => e.ProjectId == first.ProjectId)).ShouldBe(2);
    }

    [Fact]
    public async Task HumanDoor_NoteNamingTheFirstNote_ResolvesTheMarkerAgainstTheBornContainer()
    {
        var first = await CaptureJson(await PostHumanJournalAsync("First thought", "the anchor"));
        var second = await CaptureJson(await PostHumanJournalAsync("Second thought", "reads [[First thought]]"));

        var detail = await _client.GetFromJsonAsync<DetailJson>($"/api/entries/{second.Id}", JsonOptions);
        detail!.Outgoing.ShouldHaveSingleItem();
        detail.Outgoing[0].Id.ShouldBe(first.Id);
        detail.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public async Task AgentDoor_JournalWithoutProjectId_IsRefusedAndBirthsNoContainer()
    {
        var response = await _client.PostAsJsonAsync("/api/agent/entries", new
        {
            Type = "journal",
            Title = "Agent cannot start the journal",
            Content = "would have needed a container",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("JournalEntryWriteRefused");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        (await db.Projects.AnyAsync(p => p.Name == JournalProjectStore.ProjectName)).ShouldBeFalse(
            "refusal happens before the container resolution, so R6 stays closed");
    }

    [Fact]
    public async Task HumanDoor_OtherTypeWithoutProjectId_StillRefusedByTheValidator()
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            Type = "note",
            Title = "No container for a note",
            Content = "body",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        (await db.Projects.AnyAsync(p => p.Name == JournalProjectStore.ProjectName)).ShouldBeFalse();
    }

    [Fact]
    public async Task HumanDoor_JournalWithAnExplicitProject_KeepsThatProjectAsTarget()
    {
        var projectId = CreateProject("capture-explicit");

        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = "journal",
            Title = "Filed somewhere else",
            Content = "an explicit destination wins over the reserved container",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await CaptureJson(response);
        created.ProjectId.ShouldBe(projectId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        (await db.Projects.AnyAsync(p => p.Name == JournalProjectStore.ProjectName)).ShouldBeFalse(
            "naming a project must not mint the reserved container behind the caller's back");
    }

    [Fact]
    public async Task HumanDoor_TwoConcurrentFirstJournalNotes_OneContainerBothNotes()
    {
        var first = PostHumanJournalAsync("Concurrent one", "alpha");
        var second = PostHumanJournalAsync("Concurrent two", "beta");

        var pair = await Task.WhenAll(first, second);

        foreach (var response in pair)
        {
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.ShouldBe(HttpStatusCode.Created, body);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var container = await db.Projects.SingleAsync(p => p.Name == JournalProjectStore.ProjectName);
        (await db.ContextEntries.CountAsync(e => e.ProjectId == container.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task HumanDoor_ContainerRaceLandsMidTransaction_CallerRetriesAndOneContainerSurvives()
    {
        var probe = new EntryCommitProbe();
        using var raced = new ApiFactory(interceptor: probe);
        using var client = raced.CreateClient();

        probe.LandWinnerOnNextProjectInsert = true;
        var response = await client.PostAsJsonAsync("/api/entries", new
        {
            Type = "journal",
            Title = "Lost the race on purpose",
            Content = "the retry reuses the winner",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await CaptureJson(response);
        probe.WinnerLandings.ShouldBe(1);

        // Task 4's W-2 retry is the only recovery: the losing attempt rolls the whole unit back, including the
        // winner's row it inserted inside the same transaction, and the second attempt re-resolves the container.
        var projectInserts = probe.Statements
            .Count(statement => statement.Contains("INSERT INTO \"Projects\"", StringComparison.Ordinal));
        projectInserts.ShouldBe(2, string.Join(" || ", probe.Statements));

        using var scope = raced.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var container = await db.Projects.SingleAsync(p => p.Name == JournalProjectStore.ProjectName);
        container.Id.ShouldBe(created.ProjectId);
        (await db.ContextEntries.CountAsync(e => e.ProjectId == container.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task HumanDoor_JournalWithAnExplicitNullProjectId_ResolvesTheContainerToo()
    {
        // The shape task 6's capture overlay actually sends: an empty destination is a null on the wire, not an
        // absent key, and a null must not come back as a binding failure in front of the handler.
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = (Guid?)null,
            Type = "journal",
            Title = "Sent as null",
            Content = "the browser's own shape",
        }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await CaptureJson(response);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var container = await db.Projects.SingleAsync(p => p.Name == JournalProjectStore.ProjectName);
        created.ProjectId.ShouldBe(container.Id);
    }

    private Task<HttpResponseMessage> PostHumanJournalAsync(string title, string content) =>
        _client.PostAsJsonAsync("/api/entries", new
        {
            Type = "journal",
            Title = title,
            Content = content,
        }, JsonOptions);

    private static async Task<CapturedJson> CaptureJson(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var entry = await response.Content.ReadFromJsonAsync<CapturedJson>(JsonOptions);
        entry.ShouldNotBeNull();
        return entry!;
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

    private static string Describe(IEnumerable<(string Sql, DbTransaction? Transaction)> writes) =>
        string.Join(" || ", writes.Select(write => write.Sql));

    public sealed record ProjectJson(Guid Id);

    public sealed record ProblemJson(string? Title, string? Detail);

    public sealed record OutgoingJson(string Kind, Guid Id, string Label);

    public sealed record CapturedJson(Guid Id, Guid ProjectId, string Type, string Title);

    public sealed record DetailJson(
        Guid Id,
        List<OutgoingJson> Outgoing,
        List<string> Unresolved,
        List<string> Incoming = null);
}

internal sealed class WriteCapture : DbCommandInterceptor
{
    public List<(string Sql, DbTransaction? Transaction)> Statements { get; } = [];

    public void Clear() => Statements.Clear();

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Record(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    // The Guid keys come back through INSERT ... RETURNING, so the writes reach the interceptor as reader
    // commands; a NonQuery-only capture would report an empty page and the transaction proof would be a lie.
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Record(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void Record(DbCommand command) =>
        Statements.Add((command.CommandText.Replace('\n', ' '), command.Transaction));
}
