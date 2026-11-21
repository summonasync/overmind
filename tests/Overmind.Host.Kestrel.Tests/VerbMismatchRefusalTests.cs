using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Host.Kestrel.Tests;

[Trait("Host", "Kestrel")]
[Collection("overmind kestrel host")]
public sealed class VerbMismatchRefusalTests(KestrelApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/agent")]
    public async Task DeleteOnTheLaneCollection_Answers405_AllowCarriesEveryVerbTheRouteAnswers(string door)
    {
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);

        var deleted = await client.DeleteAsync($"{door}/projects/{projectId}/planning/lanes");

        deleted.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        var allow = deleted.Content.Headers.GetValues("Allow").ToArray();
        allow.ShouldContain("GET");
        allow.ShouldContain("POST");
        allow.ShouldContain("PUT");
    }

    [Fact]
    public async Task DeleteOnTheGetOnlyRoomsFamily_Answers405_AllowCarriesGetOnly()
    {
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);

        var deleted = await client.DeleteAsync($"/api/projects/{projectId}/wave/rooms");

        deleted.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        var allow = deleted.Content.Headers.GetValues("Allow").ToArray();
        allow.ShouldContain("GET");
        allow.ShouldNotContain("POST");
        allow.ShouldNotContain("PUT");
        allow.ShouldNotContain("DELETE");
    }

    private async Task<Guid> CreateProjectAsync(HttpClient client)
    {
        var name = $"kestrel-verbs-{Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = Path.Combine(Path.GetTempPath(), "overmind-kestrel-test", name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return project.GetProperty("id").GetGuid();
    }
}
