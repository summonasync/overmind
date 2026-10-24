using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Embeddings;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.VectorSearch;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ReindexServiceTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    [Fact]
    public async Task RebuildAllAsync_DeadVectorStack_SkipsRebuildAndReportsFailedEntries()
    {
        await using var context = _temp.CreateContext();
        var project = Project.Create($"reindex-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        context.Projects.Add(project);
        context.ContextEntries.Add(
            ContextEntry.Create(project.Id, EntryType.Note, "stale vectors", "written under the old model"));
        await context.SaveChangesAsync();

        var settings = new AppSettingsService(context);
        await settings.SetAsync(SettingKeys.EmbeddingProvider, "ollama", CancellationToken.None);
        await settings.SetAsync(SettingKeys.EmbeddingOllamaBaseUrl, "http://127.0.0.1:1", CancellationToken.None);
        await settings.SetAsync(SettingKeys.EmbeddingOllamaModel, "nomic-embed-text", CancellationToken.None);
        await settings.SetAsync(SettingKeys.QdrantEndpoint, "http://127.0.0.1:1", CancellationToken.None);

        var search = new QdrantSemanticSearch(
            settings,
            new EmbeddingProviderFactory(context, NullLoggerFactory.Instance),
            new VectorSearchHealthCache(),
            NullLogger<QdrantSemanticSearch>.Instance);
        var service = new ReindexService(context, search, NullLogger<ReindexService>.Instance);

        var report = await service.RebuildAllAsync(CancellationToken.None);

        report.Collection.ShouldBeNull();
        report.Total.ShouldBe(1);
        report.Indexed.ShouldBe(0);
        report.Failed.ShouldBe(1);
    }

    public void Dispose() => _temp.Dispose();
}
