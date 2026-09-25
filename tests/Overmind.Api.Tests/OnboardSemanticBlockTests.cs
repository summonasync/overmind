using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class OnboardSemanticBlockTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Onboard_HealthyProbe_CarriesAvailableSemanticBlock()
    {
        var report = new VectorSearchHealthReport(
            true,
            new QdrantHealthLeg(true, "http://127.0.0.1:6334", null),
            new EmbeddingHealthLeg(true, "ollama", "nomic-embed-text", 768, null));

        var onboard = await OnboardAsync(report);

        onboard.Semantic.ShouldNotBeNull();
        onboard.Semantic.Available.ShouldBeTrue();
        onboard.Semantic.Reason.ShouldBeNull();
    }

    [Fact]
    public async Task Onboard_DeadEmbeddingProbe_CarriesVerbatimReason()
    {
        var report = new VectorSearchHealthReport(
            false,
            new QdrantHealthLeg(true, "http://127.0.0.1:6334", null),
            new EmbeddingHealthLeg(
                false, "ollama", "all-minilm-l6-v2", null, "model 'all-minilm-l6-v2' not found, HTTP 404"));

        var onboard = await OnboardAsync(report);

        onboard.Semantic.Available.ShouldBeFalse();
        onboard.Semantic.Reason.ShouldBe("model 'all-minilm-l6-v2' not found, HTTP 404");
    }

    [Fact]
    public async Task Onboard_BothLegsDown_ReasonCarriesBothErrorsVerbatim()
    {
        var report = new VectorSearchHealthReport(
            false,
            new QdrantHealthLeg(false, "http://127.0.0.1:6334", "Error connecting to 127.0.0.1:6334"),
            new EmbeddingHealthLeg(false, "ollama", "nomic-embed-text", null, "Connection refused (127.0.0.1:11434)"));

        var onboard = await OnboardAsync(report);

        onboard.Semantic.Available.ShouldBeFalse();
        onboard.Semantic.Reason.ShouldBe(
            "Error connecting to 127.0.0.1:6334; Connection refused (127.0.0.1:11434)");
    }

    private static async Task<OnboardSemanticPayloadJson> OnboardAsync(VectorSearchHealthReport report)
    {
        var name = $"onboard-sem-{Guid.NewGuid():N}";
        ISemanticSearch search = new ReportingSemanticSearch(report);
        using var factory = new ApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISemanticSearch>();
                services.AddScoped(_ => search);
            }));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/onboard", new { Cwd = TestPaths.DiskPath(name) });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var onboard = await response.Content.ReadFromJsonAsync<OnboardSemanticPayloadJson>(JsonOptions);
        onboard.ShouldNotBeNull();
        return onboard!;
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

    public sealed record OnboardSemanticPayloadJson(OnboardSemanticBlockJson Semantic);

    public sealed record OnboardSemanticBlockJson(bool Available, string? Reason);
}
