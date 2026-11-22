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
public sealed class LaneSlashNameRoundTripTests(KestrelApiFactory factory)
{
    private const string LegacyLane = "legacy/edge";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task EncodedLegacySlashLane_UpdateDoor_ReachesTheStoredLane()
    {
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);
        await SeedLegacyLaneAsync(projectId, LegacyLane);

        var segment = Uri.EscapeDataString(LegacyLane);
        var put = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/lanes/{segment}",
            new { Contour = "src/legacy/**" });

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        LaneShouldBeOnTheRoster(client, projectId, LegacyLane, "src/legacy/**");
    }

    [Fact]
    public async Task EncodedLegacySlashLane_DeleteDoor_RemovesTheStoredLane()
    {
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);
        await SeedLegacyLaneAsync(projectId, LegacyLane);

        var segment = Uri.EscapeDataString(LegacyLane);
        var delete = await client.DeleteAsync($"/api/projects/{projectId}/planning/lanes/{segment}");

        delete.StatusCode.ShouldBe(HttpStatusCode.OK);
        RosterNames(client, projectId).ShouldNotContain(LegacyLane);
    }

    [Fact]
    public async Task EncodedSingleSegmentName_KeepsItsExactStoredSpelling()
    {
        using var client = factory.CreateWireClient();
        var projectId = await CreateProjectAsync(client);
        var percentLane = "release 50%";
        await SeedLegacyLaneAsync(projectId, percentLane);

        var put = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/lanes/{Uri.EscapeDataString(percentLane)}",
            new { Contour = "src/release/**" });

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        LaneShouldBeOnTheRoster(client, projectId, percentLane, "src/release/**");

        var delete = await client.DeleteAsync(
            $"/api/projects/{projectId}/planning/lanes/{Uri.EscapeDataString(percentLane)}");

        delete.StatusCode.ShouldBe(HttpStatusCode.OK);
        RosterNames(client, projectId).ShouldNotContain(percentLane);
    }

    private static void LaneShouldBeOnTheRoster(HttpClient client, Guid projectId, string name, string contour)
    {
        var roster = client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{projectId}/planning/lanes", JsonOptions).GetAwaiter().GetResult();
        var lane = roster.GetProperty("lanes").EnumerateArray()
            .FirstOrDefault(row => row.GetProperty("name").GetString() == name);
        lane.ValueKind.ShouldBe(JsonValueKind.Object, $"lane '{name}' is missing from the roster");
        lane.GetProperty("contour").GetString().ShouldBe(contour);
    }

    private static string[] RosterNames(HttpClient client, Guid projectId)
    {
        var roster = client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{projectId}/planning/lanes", JsonOptions).GetAwaiter().GetResult();
        return [.. roster.GetProperty("lanes").EnumerateArray()
            .Select(row => row.GetProperty("name").GetString()!)];
    }

    private async Task<Guid> CreateProjectAsync(HttpClient client)
    {
        var name = $"kestrel-slash-lane-{Guid.NewGuid():N}";
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

    private async Task SeedLegacyLaneAsync(Guid projectId, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        db.PlanningLanes.Add(PlanningLane.Create(projectId, name, contour: null, serialOrder: null));
        await db.SaveChangesAsync();
    }
}
