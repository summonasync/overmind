using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Host.Kestrel.Tests;

[Trait("Host", "Kestrel")]
[Collection("overmind kestrel host")]
public sealed class EncodedQueryContractTests(KestrelApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task EncodedQueryValue_DeliversDecodedToTheHandler_AndComesBackDecoded()
    {
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);
        var entryId = await CreateEntryAsync(client, projectId);

        var deleted = await client.DeleteAsync($"/api/entries/{entryId}?reason=raise%2Bbar%26replot");

        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tombstone = await deleted.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        tombstone.GetProperty("type").GetString().ShouldBe("tombstone");
        tombstone.GetProperty("content").GetString()!.ShouldContain("Reason: raise+bar&replot");
    }

    private async Task<Guid> CreateProjectAsync(HttpClient client)
    {
        var name = $"kestrel-query-{Guid.NewGuid():N}";
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

    private async Task<Guid> CreateEntryAsync(HttpClient client, Guid projectId)
    {
        var created = await client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = "Decision",
            Title = "buried by the wire",
            Content = "the old way",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return entry.GetProperty("id").GetGuid();
    }
}
