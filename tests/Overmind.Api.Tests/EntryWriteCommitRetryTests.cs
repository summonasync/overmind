using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Overmind.Infrastructure.Entries;
using Overmind.Host.Api;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// W-1 from the structural review of task 4: the reserved-project race matcher was a sixth hand-rolled copy of
// UniqueViolation.IsOn, and the retry was silent — an operator could only notice that a save took three tries.
// Both halves are pinned here: the matcher matrix, and one warning naming each lost attempt and constraint.
public sealed class EntryWriteCommitRetryTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly IServiceScope _scope;
    private readonly OvermindDbContext _db;
    private readonly EntryLinkWriter _writer;
    private readonly CollectingLogger _logger = new();

    public EntryWriteCommitRetryTests()
    {
        _factory = new ApiFactory();
        _scope = _factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        _writer = _scope.ServiceProvider.GetRequiredService<EntryLinkWriter>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task ReservedProjectRace_RetriesToSuccess_AndNamesEveryLostAttempt()
    {
        var attempts = 0;

        IResult written = Results.Ok();
        var outcome = await EntryWriteCommit.RunAsync(
            _db,
            _writer,
            SingleLoggerFactory.For(_logger),
            _ => Task.FromResult(new EntryWriteCommit.Work(
                Guid.NewGuid(),
                "body with no markers",
                async ct =>
                {
                    attempts++;
                    if (attempts < 3)
                    {
                        throw ReservedProjectConflict();
                    }

                    await Task.Yield();
                    return written;
                })),
            CancellationToken.None);

        outcome.ShouldBeSameAs(written);
        attempts.ShouldBe(3);

        var warnings = _logger.Matches(LogLevel.Warning);
        warnings.Count.ShouldBe(2);
        warnings[0].ShouldContain("attempt 1 of 3");
        warnings[1].ShouldContain("attempt 2 of 3");
        warnings.ShouldAllBe(w => w.Contains("Projects.Name", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ThirdRaceLoss_PropagatesInsteadOfLoopingForever()
    {
        var thrown = await Should.ThrowAsync<DbUpdateException>(
            () => EntryWriteCommit.RunAsync(
                _db,
                _writer,
                SingleLoggerFactory.For(_logger),
                _ => Task.FromException<EntryWriteCommit.Work>(ReservedProjectConflict()),
                CancellationToken.None));

        thrown.InnerException!.Message.ShouldContain("UNIQUE constraint failed: Projects.Name");
        _logger.Matches(LogLevel.Warning).Count.ShouldBe(2);
    }

    [Fact]
    public void MatcherRejectsTheNeighboursOfTheReservedProjectConflict()
    {
        EntryWriteCommit.IsReservedProjectRace(ReservedProjectConflict()).ShouldBeTrue();

        // Same constraint code, different table: not this retry's business.
        EntryWriteCommit.IsReservedProjectRace(
            Unique("Microsoft.Data.Sqlite.SqliteException (0x80004005): UNIQUE constraint failed: Tags.Name", 19))
            .ShouldBeFalse();

        // Right words, wrong signal: the hand-rolled Contains matched this, the error-code check does not.
        EntryWriteCommit.IsReservedProjectRace(new DbUpdateException(
            "boom", new InvalidOperationException("UNIQUE constraint failed: Projects.Name")))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task RefusedWork_NeedsNoRetryAndLogsNothing()
    {
        IResult refused = Results.NotFound();
        var outcome = await EntryWriteCommit.RunAsync(
            _db,
            _writer,
            SingleLoggerFactory.For(_logger),
            _ => Task.FromResult(EntryWriteCommit.Work.Rejected(refused)),
            CancellationToken.None);

        outcome.ShouldBeSameAs(refused);
        _logger.Matches(LogLevel.Warning).ShouldBeEmpty();
    }

    [Fact]
    public async Task NotesStillWriteThroughTheRetriedCommit_OnBothDoors()
    {
        using var client = _factory.CreateClient();
        var project = await client.PostAsJsonAsync("/api/projects", new
        {
            Name = "retry-probe",
            DiskPath = TestPaths.DiskPath("retry-probe"),
        });
        project.StatusCode.ShouldBe(HttpStatusCode.Created);
        var projectId = (await project.Content.ReadFromJsonAsync<ProjectJson>())!.Id;

        var created = await client.PostAsJsonAsync("/api/agent/entries", new
        {
            ProjectId = projectId,
            Type = "note",
            Title = "Retried commit still writes",
            Content = "body",
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var human = await client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = "note",
            Title = "Human side of the same commit",
            Content = "body",
        });
        human.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static DbUpdateException ReservedProjectConflict() => Unique(
        "Microsoft.Data.Sqlite.SqliteException (0x80004005): UNIQUE constraint failed: Projects.Name", 19);

    private static DbUpdateException Unique(string sqliteMessage, int errorCode) =>
        new("An exception was thrown while saving entities", new SqliteException(sqliteMessage, errorCode));

    private sealed record ProjectJson(Guid Id);

    private sealed class CollectingLogger : ILogger
    {
        private readonly List<(LogLevel Level, string Message)> _entries = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            _entries.Add((logLevel, formatter(state, exception)));

        public List<string> Matches(LogLevel level) =>
            [.. _entries.Where(entry => entry.Level == level).Select(entry => entry.Message)];
    }

    private sealed class SingleLoggerFactory(ILogger logger) : ILoggerFactory
    {
        public static ILoggerFactory For(ILogger logger) => new SingleLoggerFactory(logger);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => logger;

        public void Dispose()
        {
        }
    }
}
