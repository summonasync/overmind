using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class HealthApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HealthApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Health_ReturnsOk_WithVectorSearchDown()
    {
        var health = await _client.GetFromJsonAsync<HealthJson>("/api/health", JsonOptions);
        health.ShouldNotBeNull();
        health!.Status.ShouldBe("ok");
        health.VectorSearch.ShouldBe("down");
        health.Version.ShouldNotBeNullOrWhiteSpace();
    }

    public sealed record HealthJson(string Status, string VectorSearch, string Version);
}
