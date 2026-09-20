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

public sealed class SemanticUnavailableTests
{
    private const string VectorStoreDownDetail =
        "Semantic search is unavailable because the vector store is down. Use mode=fulltext.";

    [Fact]
    public async Task SemanticMode_NamesProviderAndModel_WhenEmbeddingFails()
    {
        var search = StubSearch(
            healthy: true,
            () => throw new EmbeddingFailedException(
                "ollama", "nomic-embed-text", new HttpRequestException("connection refused")));
        using var response = await SemanticSearchAsync(search);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var detail = await ProblemDetailAsync(response);
        detail.ShouldContain("ollama");
        detail.ShouldContain("nomic-embed-text");
        detail.ShouldContain("connection refused");
    }

    [Fact]
    public async Task SemanticMode_ExplainsRecreation_WhenDimensionsChanged()
    {
        var search = StubSearch(healthy: true, () => throw new VectorDimensionMismatchException(384, 768));
        using var response = await SemanticSearchAsync(search);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var detail = await ProblemDetailAsync(response);
        detail.ShouldContain("384");
        detail.ShouldContain("768");
        detail.ShouldContain("reindex");
    }

    [Fact]
    public async Task SemanticMode_UnexplainedFailure_KeepsVectorStoreDownMessage()
    {
        var search = StubSearch(healthy: true, () => throw new InvalidOperationException("boom"));
        using var response = await SemanticSearchAsync(search);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await ProblemDetailAsync(response)).ShouldBe(VectorStoreDownDetail);
    }

    [Fact]
    public async Task SemanticMode_HealthDown_KeepsVectorStoreDownMessage()
    {
        var search = StubSearch(healthy: false, () => throw new InvalidOperationException("unreachable"));
        using var response = await SemanticSearchAsync(search);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await ProblemDetailAsync(response)).ShouldBe(VectorStoreDownDetail);
    }

    private static async Task<HttpResponseMessage> SemanticSearchAsync(ISemanticSearch search)
    {
        using var factory = new ApiFactory();
        using var client = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISemanticSearch>();
                services.AddScoped(_ => search);
            })).CreateClient();

        return await client.GetAsync("/api/search?q=anything&mode=semantic");
    }

    private static ISemanticSearch StubSearch(bool healthy, Action search) =>
        new StubSemanticSearch(
            healthy,
            () =>
            {
                search();
                return Array.Empty<EntrySearchHit>();
            });

    private static async Task<string> ProblemDetailAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("detail").GetString().ShouldNotBeNull();
    }

    private sealed class StubSemanticSearch(bool healthy, Func<IReadOnlyList<EntrySearchHit>> search) : ISemanticSearch
    {
        public Task<bool> HealthAsync(CancellationToken ct) => Task.FromResult(healthy);

        public Task<IReadOnlyList<EntrySearchHit>> SearchAsync(
            Guid? projectId, string query, int limit, CancellationToken ct) =>
            Task.FromResult(search());
    }
}
