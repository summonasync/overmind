using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class JsonEscapingApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public JsonEscapingApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task PlanningFeatures_CyrillicAndAngleBrackets_TravelAsLiteralUtf8()
    {
        var projectId = await CreateProjectAsync();

        var title = "статусы в onboarding — тест <goal>";
        var created = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = "cyr-feature", Title = title, Body = "" });
        created.EnsureSuccessStatusCode();

        var createdBody = Encoding.UTF8.GetString(await created.Content.ReadAsByteArrayAsync());
        createdBody.ShouldContain(title);
        createdBody.ShouldNotContain("\\u04");
        createdBody.ShouldNotContain("\\u003C");

        var raw = await _client.GetByteArrayAsync($"/api/projects/{projectId}/planning/features");
        var body = Encoding.UTF8.GetString(raw);

        body.ShouldContain("статусы в onboarding — тест <goal>");
        body.ShouldNotContain("\\u04");
        body.ShouldNotContain("\\u003C");
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"escaping-proj-{Guid.NewGuid():N}",
            DiskPath = Path.Combine(Path.GetTempPath(), $"overmind-esc-{Guid.NewGuid():N}"),
            Description = (string?)null,
        });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }
}
