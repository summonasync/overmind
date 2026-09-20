using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class HealthApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Health_WithHealthyVectorSearch_ReturnsStructuredObjectWithFlatUp()
    {
        var report = new VectorSearchHealthReport(
            true,
            new QdrantHealthLeg(true, "http://127.0.0.1:6334", null),
            new EmbeddingHealthLeg(true, "ollama", "nomic-embed-text", 768, null));

        var health = await GetHealthAsync(report);

        health.ShouldNotBeNull();
        health!.Status.ShouldBe("ok");
        health.Version.ShouldNotBeNullOrWhiteSpace();
        health.VectorSearch.Up.ShouldBeTrue();
        health.VectorSearch.Qdrant.Up.ShouldBeTrue();
        health.VectorSearch.Qdrant.Endpoint.ShouldBe("http://127.0.0.1:6334");
        health.VectorSearch.Qdrant.Error.ShouldBeNull();
        health.VectorSearch.Embedding.Up.ShouldBeTrue();
        health.VectorSearch.Embedding.Provider.ShouldBe("ollama");
        health.VectorSearch.Embedding.Model.ShouldBe("nomic-embed-text");
        health.VectorSearch.Embedding.Dimensions.ShouldBe(768);
        health.VectorSearch.Embedding.Error.ShouldBeNull();
    }

    [Fact]
    public async Task Health_WithDegradedLegs_ReportsFlatDownWithPerLegErrors()
    {
        var report = new VectorSearchHealthReport(
            false,
            new QdrantHealthLeg(false, "http://127.0.0.1:6334", "connection refused"),
            new EmbeddingHealthLeg(false, "ollama", "missing-model", null, "model not found"));

        var health = await GetHealthAsync(report);

        health.ShouldNotBeNull();
        health!.VectorSearch.Up.ShouldBeFalse();
        health.VectorSearch.Qdrant.Up.ShouldBeFalse();
        health.VectorSearch.Qdrant.Error.ShouldBe("connection refused");
        health.VectorSearch.Embedding.Up.ShouldBeFalse();
        health.VectorSearch.Embedding.Dimensions.ShouldBeNull();
        health.VectorSearch.Embedding.Error.ShouldBe("model not found");
    }

    [Fact]
    public async Task Health_WithGreenQdrantAndDeadEmbedding_FlatUpIsStillDown()
    {
        var report = new VectorSearchHealthReport(
            false,
            new QdrantHealthLeg(true, "http://127.0.0.1:6334", null),
            new EmbeddingHealthLeg(false, "onnx", "all-MiniLM-L6-v2", null, "model load failed"));

        var health = await GetHealthAsync(report);

        health.ShouldNotBeNull();
        health!.VectorSearch.Up.ShouldBeFalse();
        health.VectorSearch.Qdrant.Up.ShouldBeTrue();
        health.VectorSearch.Embedding.Up.ShouldBeFalse();
    }

    private static async Task<HealthJson?> GetHealthAsync(VectorSearchHealthReport report)
    {
        using var factory = FactoryWithReport(report);
        using var client = factory.CreateClient();
        return await client.GetFromJsonAsync<HealthJson>("/api/health", JsonOptions);
    }

    private static WebApplicationFactory<Program> FactoryWithReport(VectorSearchHealthReport report)
    {
        ISemanticSearch search = new ReportingSemanticSearch(report);
        return new ApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISemanticSearch>();
                services.AddScoped(_ => search);
            }));
    }

    private sealed class ReportingSemanticSearch(VectorSearchHealthReport report) : ISemanticSearch
    {
        public Task<bool> HealthAsync(CancellationToken ct) => Task.FromResult(report.Qdrant.Up);

        public Task<VectorSearchHealthReport> HealthReportAsync(CancellationToken ct) =>
            Task.FromResult(report);

        public Task<IReadOnlyList<EntrySearchHit>> SearchAsync(
            Guid? projectId, string query, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EntrySearchHit>>([]);
    }

    public sealed record HealthJson(string Status, VectorSearchHealthReport VectorSearch, string Version);
}
