using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Host.Kestrel.Tests;

[Trait("Host", "Kestrel")]
[Collection("overmind kestrel host")]
public sealed class DottedSegmentRoutingTests(KestrelApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task MidDotSegment_OnAnUnmappedPrefix_IsNeverCollapsed_AndFallsToTheSpaShell()
    {
        using var client = factory.CreateWireClient();

        var response = await client.GetAsync("/api/v1.x/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }

    [Fact]
    public async Task MidDotSegment_InTheRoomPosition_BindsIntact_AndTheHandlerDecides()
    {
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);

        var response = await client.GetAsync($"/api/projects/{projectId}/wave/rooms/v1.x/messages");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        problem.GetProperty("title").GetString().ShouldBe("MissingWaveRoom");
        problem.GetProperty("detail").GetString()!.ShouldContain("'v1.x'");
    }

    [Fact]
    public async Task DotDotOnTheWire_CollapsesBackIntoTheApiPrefix_BeforeRouting()
    {
        using var client = factory.CreateWireClient();

        var response = await KestrelRawWire.SendAsync(new Uri(factory.BaseUrl), "GET /api/../api/health HTTP/1.1");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Header("Content-Type").ShouldContain("application/json");
        response.Body.ShouldContain("\"status\":\"ok\"");
    }

    [Fact]
    public async Task DotDotOnTheWire_WalkingOutOfTheLanesPrefix_LandsOnNoWriter_AndRefuses405()
    {
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);

        var response = await KestrelRawWire.SendAsync(
            new Uri(factory.BaseUrl),
            $"PUT /api/projects/{projectId}/planning/lanes/.. HTTP/1.1",
            """{"lanes":[{"name":"swapped"}]}""");

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        response.Body.ShouldBeEmpty();
        response.Header("Content-Type").ShouldBeEmpty();
    }

    private async Task<Guid> CreateProjectAsync(HttpClient client)
    {
        var name = $"kestrel-dots-{Guid.NewGuid():N}";
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
