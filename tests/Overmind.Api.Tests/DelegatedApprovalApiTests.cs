using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class DelegatedApprovalApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Guid _sessionId;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public DelegatedApprovalApiTests()
    {
        _client = _factory.CreateClient();
        using var scope = _factory.Services.CreateAsyncScope();
        _sessionId = scope.ServiceProvider
            .GetRequiredService<EmbeddedAgentSessionRegistrar>()
            .EnsureRegisteredAsync(CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task ApprovalSettings_RoundTripsAndDefaultsToOff()
    {
        var project = await CreateProjectAsync();

        var fresh = await _client.GetFromJsonAsync<ApprovalSettingsJson>(
            ApprovalSettingsUrl(project.Id), JsonOptions);
        fresh!.DelegateApprovals.ShouldBeFalse();

        var updated = await _client.PutAsJsonAsync(ApprovalSettingsUrl(project.Id), new { DelegateApprovals = true });
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await updated.Content.ReadFromJsonAsync<ApprovalSettingsJson>(JsonOptions))!.DelegateApprovals.ShouldBeTrue();

        var stored = await _client.GetFromJsonAsync<ApprovalSettingsJson>(
            ApprovalSettingsUrl(project.Id), JsonOptions);
        stored!.DelegateApprovals.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_FlagOnWithOwnerInstruction_SucceedsWithDelegatedProvenance()
    {
        var project = await CreateProjectAsync();
        await EnableDelegationAsync(project.Id);
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "spec" });
        var instructionId = await SendInstructionAsync(project.Id, "approve spec of feat");

        var response = await SendAsync(AgentValidateUrl(spec.Id), HttpMethod.Post, _sessionId, instructionId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(AgentReadUrl(spec.Id), JsonOptions);
        artifact!.Body.ShouldContain(DelegatedApprovalGate.ProvenanceMarker);
        artifact.Body.ShouldContain($"owner message {instructionId}");
        artifact.Body.ShouldContain("\"approve spec of feat\"");
    }

    [Fact]
    public async Task Validate_FlagOff_Returns409ValidationProofRequired()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "spec" });
        var instructionId = await SendInstructionAsync(project.Id, "approve spec of feat");

        var response = await SendAsync(AgentValidateUrl(spec.Id), HttpMethod.Post, _sessionId, instructionId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("ValidationProofRequired");
    }

    [Fact]
    public async Task Validate_FlagOnWithoutInstructionReference_Returns409()
    {
        var project = await CreateProjectAsync();
        await EnableDelegationAsync(project.Id);
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "spec" });

        var response = await SendAsync(AgentValidateUrl(spec.Id), HttpMethod.Post, _sessionId, null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("ValidationProofRequired");
    }

    [Fact]
    public async Task Validate_FlagOnExternalSession_StillReturns409()
    {
        var project = await CreateProjectAsync();
        await EnableDelegationAsync(project.Id);
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "spec" });
        var instructionId = await SendInstructionAsync(project.Id, "approve spec of feat");
        var external = await _client.PostAsJsonAsync(
            "/api/agent/sessions", new { Alias = "outside-agent", Vendor = "claude" }, JsonOptions);
        external.StatusCode.ShouldBe(HttpStatusCode.Created);
        var externalSessionId = (await external.Content.ReadFromJsonAsync<SessionJson>(JsonOptions))!.Id;

        var response = await SendAsync(AgentValidateUrl(spec.Id), HttpMethod.Post, externalSessionId, instructionId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("ValidationProofRequired");
    }

    [Fact]
    public async Task SetFeatureStatus_FlagOnWithOwnerInstruction_DelegatesTheDoneFlip()
    {
        var project = await CreateProjectAsync();
        await EnableDelegationAsync(project.Id);
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "result", Body = "# Result\n\nclose-out\n" });
        var instructionId = await SendInstructionAsync(project.Id, "close feat, it is done");

        var response = await SendAsync(
            AgentFeatureStatusUrl(project.Id, "feat"),
            HttpMethod.Post,
            _sessionId,
            instructionId,
            new { Status = "done", LogNote = "owner said close it" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/agent/projects/{project.Id}/planning/features/feat", JsonOptions);
        feature!.Status.ShouldBe("done");
        feature.Body.ShouldContain(DelegatedApprovalGate.ProvenanceMarker);
        feature.Body.ShouldContain($"owner message {instructionId}");
    }

    [Fact]
    public async Task SetFeatureStatus_FlagOff_DoneFlipStillRequiresTheHumanTap()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "result", Body = "# Result\n\nclose-out\n" });
        var instructionId = await SendInstructionAsync(project.Id, "close feat, it is done");

        var response = await SendAsync(
            AgentFeatureStatusUrl(project.Id, "feat"),
            HttpMethod.Post,
            _sessionId,
            instructionId,
            new { Status = "done", LogNote = "owner said close it" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("FeatureDoneTapRequired");
    }

    private async Task<HttpResponseMessage> SendAsync(
        string url, HttpMethod method, Guid sessionId, Guid? instructionId, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Overmind-Session", sessionId.ToString());
        if (instructionId is { } instruction)
        {
            request.Headers.Add(AgentLoopbackClient.DelegatedByHeader, instruction.ToString());
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return await _client.SendAsync(request);
    }

    private async Task<Guid> SendInstructionAsync(Guid projectId, string text)
    {
        var created = await _client.PostAsJsonAsync(
            "/api/agent/conversations", new { ProjectId = projectId, Title = "delegation" }, JsonOptions);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var conversationId = (await created.Content.ReadFromJsonAsync<ConversationJson>(JsonOptions))!.Id;

        var streamed = await _client.PostAsJsonAsync(
            $"/api/agent/conversations/{conversationId}/messages/stream", new { Text = text }, JsonOptions);
        streamed.StatusCode.ShouldBe(HttpStatusCode.OK);

        var history = await _client.GetFromJsonAsync<HistoryJson>(
            $"/api/agent/conversations/{conversationId}/messages", JsonOptions);
        return history!.Items.Where(m => m.Role == "user").Select(m => m.Id).Last();
    }

    private async Task EnableDelegationAsync(Guid projectId)
    {
        var updated = await _client.PutAsJsonAsync(
            ApprovalSettingsUrl(projectId), new { DelegateApprovals = true });
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var name = $"delegation-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = Path.Combine(Path.GetTempPath(), name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions))!;
    }

    private async Task CreateAgentFeatureAsync(Guid projectId, string slug, string title)
    {
        var created = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = "" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<ArtifactJson> CreateAgentArtifactAsync(Guid projectId, string slug, object body)
    {
        var created = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts", body);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await created.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions))!;
    }

    private static string ApprovalSettingsUrl(Guid projectId) =>
        $"/api/projects/{projectId}/approval-settings";

    private static string AgentValidateUrl(Guid artifactId) =>
        $"/api/agent/planning/artifacts/{artifactId}/validate";

    private static string AgentFeatureStatusUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/status";

    private static string AgentReadUrl(Guid artifactId) =>
        $"/api/agent/planning/artifacts/{artifactId}";

    private sealed record ApprovalSettingsJson(bool DelegateApprovals);

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath);

    private sealed record ArtifactJson(Guid Id, string Kind, string Status, string Body);

    private sealed record FeatureJson(Guid Id, string Slug, string Status, string Body);

    private sealed record ConversationJson(Guid Id, Guid? ProjectId, string Title);

    private sealed record SessionJson(Guid Id);

    private sealed record HistoryJson(ConversationMessageJson[] Items);

    private sealed record ConversationMessageJson(Guid Id, string Role, string Content, DateTime CreatedAt);

    private sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
