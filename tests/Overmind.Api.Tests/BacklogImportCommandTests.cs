using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Host;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

[CollectionDefinition(BacklogImportCommandTests.CollectionName, DisableParallelization = true)]
public sealed class BacklogImportCommandCollection;

[Collection(BacklogImportCommandTests.CollectionName)]
public sealed class BacklogImportCommandTests : IDisposable
{
    public const string CollectionName = "BacklogImportCommand";

    private readonly TempRepo _repo = new();
    private readonly string _groundTruthDb;
    private readonly string _commandDb;
    private readonly Guid _groundTruthProjectId;
    private readonly Guid _commandProjectId;

    public BacklogImportCommandTests()
    {
        WriteSkippedFixture(_repo);
        (_groundTruthDb, _groundTruthProjectId) = SeedDatabase(_repo.Root);
        (_commandDb, _commandProjectId) = SeedDatabase(_repo.Root);
    }

    [Fact]
    public async Task Import_LogsEverySkippedEntryOnItsOwnLineAfterSummary()
    {
        var report = await RunImporterAsync(_groundTruthDb, _groundTruthProjectId, _repo.Root);

        report.Skipped.ShouldContain(s => s.StartsWith("skipped: ", StringComparison.Ordinal));
        report.Skipped.ShouldContain(s => s.StartsWith("ignored: ", StringComparison.Ordinal));
        report.Skipped.ShouldContain(s => s.StartsWith("unknown status '", StringComparison.Ordinal));

        var events = await RunCommandAsync(_commandDb, _repo.Root);

        events.ShouldAllBe(e => e.Level == LogEventLevel.Information);
        Rendered(events).ShouldBe(
            new[]
            {
                $"Imported planning from {_repo.Root}: 2 features (2 new), 2 artifacts, {report.Skipped.Count} skipped",
            }.Concat(report.Skipped).Append(report.NextStep!).ToArray());
    }

    [Fact]
    public async Task Import_WithoutSkips_LogsSummaryOnly()
    {
        using var repo = new TempRepo();
        repo.Write("010-clean/feature.md",
            """
            ---
            feature: 010
            slug: clean
            status: ready
            version: 1
            created: 2026-09-01
            ---

            # Feature 010 — Clean

            Clean body.
            """);
        var (dbPath, _) = SeedDatabase(repo.Root);

        try
        {
            var events = await RunCommandAsync(dbPath, repo.Root);

            events.ShouldAllBe(e => e.Level == LogEventLevel.Information);
            Rendered(events).ShouldBe(
                new[]
                {
                    $"Imported planning from {repo.Root}: 1 features (1 new), 0 artifacts, 0 skipped",
                    "1 files tracked, materialize is a no-op",
                });
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task Import_LogsNewKeysPreservedAndCreatedFallbacks()
    {
        using var repo = new TempRepo();
        var featurePath = repo.Write("010-keys/feature.md",
            """
            ---
            feature: 010
            slug: keys
            status: ready
            version: 1
            qa-owner: alice
            ---

            # Feature 010 — Keys

            Keys body.
            """);
        File.SetLastWriteTimeUtc(featurePath, new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc));
        var (dbPath, _) = SeedDatabase(repo.Root);

        try
        {
            var events = await RunCommandAsync(dbPath, repo.Root);

            var color = FrontmatterKeyPalette.ColorFor("qa-owner");
            Rendered(events).ShouldContain($"new key: qa-owner ({color})");
            Rendered(events).ShouldContain("preserved: BACKLOG/010-keys/feature.md (qa-owner)");
            Rendered(events).ShouldContain(
                "created: BACKLOG/010-keys/feature.md (no created token; fell back to file mtime 2026-09-10)");
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    public void Dispose()
    {
        _repo.Dispose();
        DeleteDatabaseFiles(_groundTruthDb);
        DeleteDatabaseFiles(_commandDb);
    }

    private static void WriteSkippedFixture(TempRepo repo)
    {
        repo.Write("010-demo/feature.md",
            """
            ---
            feature: 010
            slug: demo
            status: bogus
            version: 1
            created: 2026-09-01
            ---

            # Feature 010 — Demo

            <goal>
            Demo body.
            </goal>
            """);
        repo.Write("010-demo/spec.md",
            """
            ---
            feature: 010
            slug: demo
            status: draft
            created: 2026-09-01
            ---

            # Spec 010 — Demo

            Spec body.
            """);
        repo.Write("010-demo/tasks/01-real.md",
            """
            ---
            feature: 010
            task: 01
            slug: demo
            status: todo
            review: light
            created: 2026-09-01
            ---

            # Real task

            Task body.
            """);
        repo.Write("010-demo/tasks/notes.md", "no order prefix");
        repo.Write("010-demo/junk.txt", "ignored by the importer");
        repo.Write("020-solo/feature.md",
            """
            ---
            feature: 020
            slug: solo
            status: ready
            version: 1
            created: 2026-09-02
            ---

            # Feature 020 — Solo

            Solo body.
            """);
        repo.Write("030-empty/notes.txt", "not a feature folder");
        repo.Write("_TEMPLATE/feature.md",
            """
            ---
            feature: 000
            slug: template
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 000 — Template
            """);
        repo.Write("README.md", "stray top-level file");
    }

    private static (string DbPath, Guid ProjectId) SeedDatabase(string repoRoot)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"overmind-import-cmd-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<OvermindDbContext>()
            .UseSqlite($"Data Source={dbPath};Pooling=false")
            .Options;
        using var context = new OvermindDbContext(options);
        context.Database.Migrate();
        var project = Project.Create($"import-{Guid.NewGuid():N}", repoRoot, string.Empty);
        context.Projects.Add(project);
        context.SaveChanges();
        return (dbPath, project.Id);
    }

    private static async Task<BacklogImportReport> RunImporterAsync(string dbPath, Guid projectId, string repoRoot)
    {
        using var provider = BuildServices(dbPath);
        using var scope = provider.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<BacklogImporter>();
        return await importer.ImportAsync(new BacklogImportRequest(repoRoot, projectId), CancellationToken.None);
    }

    private static async Task<IReadOnlyList<LogEvent>> RunCommandAsync(string dbPath, string repoRoot)
    {
        using var provider = BuildServices(dbPath);
        using var scope = provider.CreateScope();

        var originalLogger = Log.Logger;
        var sink = new CaptureSink();
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Sink(sink)
            .CreateLogger();
        try
        {
            await BacklogImportCommand.TryRunAsync(
                scope.ServiceProvider, ["--import-backlog", repoRoot], CancellationToken.None);
        }
        finally
        {
            Log.Logger = originalLogger;
        }

        return sink.Events;
    }

    private static readonly MessageTemplateTextFormatter ConsoleMessageFormatter = new("{Message:l}");

    private static string[] Rendered(IReadOnlyList<LogEvent> events)
    {
        var messages = new string[events.Count];
        for (var i = 0; i < events.Count; i++)
        {
            var writer = new StringWriter(CultureInfo.InvariantCulture);
            ConsoleMessageFormatter.Format(events[i], writer);
            messages[i] = writer.ToString();
        }

        return messages;
    }

    private static ServiceProvider BuildServices(string dbPath) =>
        new ServiceCollection()
            .AddDbContext<OvermindDbContext>(options => options.UseSqlite($"Data Source={dbPath};Pooling=false"))
            .AddSingleton<IPlanningMaterializer>(NoOpMaterializer.Instance)
            .AddSingleton<ILogger<PlanningFeatureService>>(NullLogger<PlanningFeatureService>.Instance)
            .AddSingleton<ILogger<FrontmatterKeyRegistry>>(NullLogger<FrontmatterKeyRegistry>.Instance)
            .AddSingleton<ILogger<TaskDependencyService>>(NullLogger<TaskDependencyService>.Instance)
            .AddSingleton<ILogger<PlanningMaterializationDispatcher>>(NullLogger<PlanningMaterializationDispatcher>.Instance)
            .AddSingleton<PlanningMaterializationQueue>()
            .AddScoped<PlanningMaterializationDispatcher>()
            .AddScoped<AppSettingsService>()
            .AddScoped<FrontmatterKeyRegistry>()
            .AddScoped<PlanningPolicy>()
            .AddScoped<TaskDependencyService>()
            .AddScoped<PlanningFeatureService>()
            .AddScoped<BacklogImportSeeder>()
            .AddScoped<BacklogImporter>()
            .BuildServiceProvider();

    private static void DeleteDatabaseFiles(string dbPath)
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var file = dbPath + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    private sealed class CaptureSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private sealed class NoOpMaterializer : IPlanningMaterializer
    {
        public static readonly NoOpMaterializer Instance = new();

        public Task<PlanningMaterializationReport> SyncAsync(
            Guid projectId, bool force = false, bool quarantine = false, CancellationToken ct = default) =>
            Task.FromResult(new PlanningMaterializationReport(projectId, PlanningMaterialization.Off, [], [], []));

        public Task<IReadOnlyList<PlanningDriftItem>> DetectDriftAsync(Guid projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PlanningDriftItem>>([]);

        public Task<PlanningMaterializationReport> ClearAsync(
            Guid projectId, Guid? featureId = null, CancellationToken ct = default) =>
            Task.FromResult(new PlanningMaterializationReport(projectId, PlanningMaterialization.Off, [], [], []));

        public Task RecordSyncFailureAsync(Guid projectId, string error, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class TempRepo : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"overmind-import-cmd-{Guid.NewGuid():N}");

        private string BacklogDir => Path.Combine(Root, "BACKLOG");

        public TempRepo()
        {
            Directory.CreateDirectory(BacklogDir);
        }

        public string Write(string relativePath, string content)
        {
            var path = Path.Combine(BacklogDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
