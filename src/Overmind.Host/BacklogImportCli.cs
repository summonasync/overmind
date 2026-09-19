using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Serilog;
using Serilog.Events;

namespace Overmind.Host;

internal static class BacklogImportCli
{
    public static bool IsRequested(string[] args) =>
        args.Contains("--import-backlog", StringComparer.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();
        await using var provider = BuildProvider(configuration);
        return await ExecuteAsync(args, provider, Console.Out, Console.Error, ct);
    }

    internal static ServiceProvider BuildProvider(IConfiguration configuration)
    {
        var cliLog = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Warning)
            .CreateLogger();
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging(builder => builder.AddSerilog(cliLog))
            .AddBacklogImportPipeline()
            .BuildServiceProvider();
    }

    internal static ServiceProvider BuildProvider(string databasePath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Overmind:DatabasePath"] = databasePath })
            .Build();
        return BuildProvider(configuration);
    }

    internal static async Task<int> ExecuteAsync(
        string[] args, ServiceProvider provider, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var backlogPath = BacklogImportCommand.FlagValue(args, "--import-backlog");
        if (string.IsNullOrWhiteSpace(backlogPath))
        {
            await error.WriteLineAsync("--import-backlog requires a repository root or BACKLOG folder path.");
            return 2;
        }

        var reportPath = BacklogImportCommand.FlagValue(args, "--report");
        if (reportPath is null && args.Contains("--report", StringComparer.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("--report requires a file path.");
            return 2;
        }

        if (reportPath is not null
            && !Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(reportPath))!))
        {
            await error.WriteLineAsync($"--report: directory for {reportPath} does not exist.");
            return 2;
        }

        if (BacklogImporter.ResolveBacklogDirectory(backlogPath) is null)
        {
            await error.WriteLineAsync(
                $"--import-backlog: {backlogPath} is neither a repository root containing a BACKLOG folder nor a BACKLOG folder itself.");
            return 1;
        }

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            await db.Database.MigrateAsync(ct);

            var repoRoot = Path.GetFullPath(
                Path.GetDirectoryName(BacklogImporter.ResolveBacklogDirectory(backlogPath)!)!);
            var (project, resolveError) = await BacklogImportCommand.ResolveProjectResultAsync(
                db, repoRoot, BacklogImportCommand.FlagValue(args, "--project"), ct);
            if (project is null)
            {
                await error.WriteLineAsync(resolveError);
                return 1;
            }

            var importer = scope.ServiceProvider.GetRequiredService<BacklogImporter>();
            var report = await importer.ImportAsync(new BacklogImportRequest(backlogPath, project.Id), ct);
            var text = BacklogImportReportText.Text(report, backlogPath);
            await output.WriteAsync(text.AsMemory(), ct);
            if (reportPath is not null)
            {
                await File.WriteAllTextAsync(
                    reportPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await error.WriteLineAsync($"Backlog import from {backlogPath} failed: {ex.Message}");
            return 1;
        }
    }
}
