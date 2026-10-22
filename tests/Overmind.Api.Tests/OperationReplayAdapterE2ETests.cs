using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Overmind.Host.Api;
using Overmind.Mcp;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class OperationReplayAdapterE2ETests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly OvermindApiClient _api;

    private const string FeatureSlug = "001-replay";

    public OperationReplayAdapterE2ETests()
    {
        _factory = new ApiFactory();
        _http = _factory.CreateClient();
        _api = new OvermindApiClient(_http);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Patch_RetriedWithSameOperationId_ReplaysTheStoredBody()
    {
        var artifactId = await CreateTaskArtifactAsync();
        var operationId = Guid.NewGuid();

        var first = await PatchGoalAsync(artifactId, operationId, "first shape");
        first.GetProperty("version").GetInt32().ShouldBe(2);
        first.TryGetProperty("replayed", out _).ShouldBeFalse();

        var retry = await PatchGoalAsync(artifactId, operationId, "first shape");

        retry.GetProperty("version").GetInt32().ShouldBe(2);
        retry.GetProperty("replayed").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Patch_SameOperationId_DifferentPayload_SurfacesOperationIdConflict()
    {
        var artifactId = await CreateTaskArtifactAsync();
        var operationId = Guid.NewGuid();
        await PatchGoalAsync(artifactId, operationId, "first shape");

        var conflict = await Should.ThrowAsync<ApiStatusException>(() =>
            PatchGoalAsync(artifactId, operationId, "a different shape"));

        conflict.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        conflict.Message.ShouldContain("was already used with a different request");
        conflict.Message.ShouldNotContain(OvermindApiClient.RetryableMarker);
    }

    [Fact]
    public async Task Patch_WithoutOperationId_AppliesEveryTime()
    {
        var artifactId = await CreateTaskArtifactAsync();

        var first = await PatchGoalAsync(artifactId, null, "no key");
        var second = await PatchGoalAsync(artifactId, null, "no key");

        first.GetProperty("version").GetInt32().ShouldBe(2);
        second.GetProperty("version").GetInt32().ShouldBe(3);
        second.TryGetProperty("replayed", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task NonGuidOperationId_SurfacesBadRequest()
    {
        var artifactId = await CreateTaskArtifactAsync();
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/agent/planning/artifacts/{artifactId}/log")
        {
            Content = new StringContent("""{"note":"x"}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Operation-Id", "operation-1");

        var response = await _http.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        problem.GetProperty("title").GetString().ShouldBe("InvalidOperationId");
    }

    private async Task<JsonElement> PatchGoalAsync(Guid artifactId, Guid? operationId, string content)
    {
        return await _api.PostAsync(
            $"/api/agent/planning/artifacts/{artifactId}/patch",
            new { op = "replace_section", section = "goal", content, source = "agent" },
            CancellationToken.None,
            operationId);
    }

    private async Task<Guid> CreateTaskArtifactAsync()
    {
        var name = $"op-replay-adapter-{Guid.NewGuid():N}";
        var created = await _api.PostAsync(
            "/api/projects",
            new { Name = name, DiskPath = TestPaths.DiskPath(name), Description = (string?)null },
            CancellationToken.None);
        var projectId = created.GetProperty("id").GetGuid();

        await _api.PostAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            new { Slug = FeatureSlug, Title = "Replay", Body = "" },
            CancellationToken.None);

        var artifact = await _api.PostAsync(
            $"/api/agent/projects/{projectId}/planning/features/{FeatureSlug}/artifacts",
            new { Kind = "task", Order = 0, Body = "<goal>replay probe</goal>", LogNote = "seed" },
            CancellationToken.None);
        return artifact.GetProperty("id").GetGuid();
    }
}
