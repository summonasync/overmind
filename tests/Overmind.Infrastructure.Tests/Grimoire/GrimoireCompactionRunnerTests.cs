using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Infrastructure.Grimoire;
using Overmind.Infrastructure.Llm;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Grimoire;

public sealed class GrimoireCompactionRunnerTests : IDisposable
{
    private readonly GrimoireMemoryFixture _fixture = new();
    private readonly ILlmChatClientFactory _chatFactory = Substitute.For<ILlmChatClientFactory>();

    [Fact]
    public async Task RunAsync_CandidatesDistillAndForget_WritesFactsAndForgetsConsumedEntries()
    {
        var factId = Guid.NewGuid();
        var entryA = Guid.NewGuid();
        var entryB = Guid.NewGuid();
        _fixture.Server.CandidateGroupsJson = JsonSerializer.Serialize(new
        {
            memoryIds = new[] { entryA, entryB },
            similarity = 0.83,
            entries = new[]
            {
                new
                {
                    id = entryA,
                    content = "journal one",
                    kind = "journal",
                    tags = Array.Empty<string>(),
                    createdAt = "2026-10-20T10:00:00Z",
                    lastAccessedAt = "2026-10-21T10:00:00Z",
                    actor = new { alias = "claude-core", vendor = (string?)null },
                },
            },
        });
        _fixture.Server.NextRememberId = factId;
        await _fixture.ConfigureOverrideUrlAsync("http://stub.local");
        var runner = CreateRunner(new FixedTextChatClient("The crew standardizes deployment via feature branches."));

        var result = await runner.RunAsync(CancellationToken.None);

        result.Outcome.ShouldBe(GrimoireCompactionOutcome.Completed);
        result.GroupsSeen.ShouldBe(1);
        result.FactsWritten.ShouldBe(1);
        result.EntriesForgotten.ShouldBe(2);
        result.GroupsSkipped.ShouldBe(0);

        var write = _fixture.Server.Single(HttpMethod.Post, "/api/memories");
        var writeBody = JsonDocument.Parse(write.Body).RootElement;
        writeBody.GetProperty("kind").GetString()!.ShouldBe("fact");
        writeBody.GetProperty("scope").GetString()!.ShouldBe("global");
        writeBody.GetProperty("tome").GetString()!.ShouldBe("overmind-agent");
        writeBody.GetProperty("importance").GetDouble().ShouldBe(0.7);

        var candidates = _fixture.Server.Single(HttpMethod.Get, "/api/compaction/candidates");
        candidates.Query.ShouldContain("tome=overmind-agent");
        var writeTags = writeBody.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).ToList();
        writeTags.ShouldContain("overmind://actor/overmind-agent");
        writeTags.ShouldContain("overmind://vendor/overmind-host");
        writeBody.GetProperty("content").GetString()!.ShouldContain("feature branches");

        var forgetReasons = _fixture.Server.Requests
            .Where(request => request.Path.EndsWith("/forget"))
            .Select(request => JsonDocument.Parse(request.Body).RootElement.GetProperty("reason").GetString())
            .ToList();
        forgetReasons.Count.ShouldBe(2);
        forgetReasons.ShouldAllBe(reason => reason == $"compacted into {factId}");
    }

    [Fact]
    public async Task RunAsync_ProviderUnconfigured_SkipsThePassWithoutTouchingCandidates()
    {
        await _fixture.ConfigureOverrideUrlAsync("http://stub.local");
        _chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns((IChatClient?)null);
        var runner = CreateRunner();

        var result = await runner.RunAsync(CancellationToken.None);

        result.Outcome.ShouldBe(GrimoireCompactionOutcome.ProviderMissing);
        result.Reason.ShouldContain("no LLM provider is configured");
        _fixture.Server.Requests.ShouldNotContain(request => request.Path == "/api/compaction/candidates");
    }

    [Fact]
    public async Task RunAsync_CandidatesEndpointMissing_SkipsWithHonestReason()
    {
        _fixture.Server.CandidatesEndpointExists = false;
        await _fixture.ConfigureOverrideUrlAsync("http://stub.local");
        var runner = CreateRunner(new FixedTextChatClient("anything"));

        var result = await runner.RunAsync(CancellationToken.None);

        result.Outcome.ShouldBe(GrimoireCompactionOutcome.CandidateSourceFailed);
        result.Reason.ShouldContain("/api/compaction/candidates");
    }

    [Fact]
    public async Task RunAsync_WriteFails_GroupIsSkippedWithoutForgettingItsEntries()
    {
        var entryId = Guid.NewGuid();
        _fixture.Server.CandidateGroupsJson = JsonSerializer.Serialize(new
        {
            memoryIds = new[] { entryId },
            similarity = 0.8,
            entries = new[]
            {
                new
                {
                    id = entryId,
                    content = "journal one",
                    kind = "journal",
                    tags = Array.Empty<string>(),
                    createdAt = "2026-10-20T10:00:00Z",
                    lastAccessedAt = "2026-10-21T10:00:00Z",
                    actor = new { alias = "claude-core", vendor = (string?)null },
                },
            },
        });
        _fixture.Server.RememberStatus = HttpStatusCode.InternalServerError;
        await _fixture.ConfigureOverrideUrlAsync("http://stub.local");
        var runner = CreateRunner(new FixedTextChatClient("a fact"));

        var result = await runner.RunAsync(CancellationToken.None);

        result.Outcome.ShouldBe(GrimoireCompactionOutcome.Completed);
        result.FactsWritten.ShouldBe(0);
        result.EntriesForgotten.ShouldBe(0);
        result.GroupsSkipped.ShouldBe(1);
        _fixture.Server.Requests.ShouldNotContain(request => request.Path.EndsWith("/forget"));
    }

    [Fact]
    public async Task RunAsync_EmptyDistillation_SkipsTheGroup()
    {
        _fixture.Server.CandidateGroupsJson =
            """{"memoryIds":["22222222-2222-2222-2222-222222222222"],"similarity":0.8,"entries":[{"id":"22222222-2222-2222-2222-222222222222","content":"journal one","kind":"journal","tags":[],"createdAt":"2026-10-20T10:00:00Z","lastAccessedAt":"2026-10-21T10:00:00Z","actor":{"alias":"claude-core","vendor":null}}]}""";
        await _fixture.ConfigureOverrideUrlAsync("http://stub.local");
        var runner = CreateRunner(new FixedTextChatClient("   "));

        var result = await runner.RunAsync(CancellationToken.None);

        result.Outcome.ShouldBe(GrimoireCompactionOutcome.Completed);
        result.FactsWritten.ShouldBe(0);
        result.EntriesForgotten.ShouldBe(0);
        result.GroupsSkipped.ShouldBe(1);
        _fixture.Server.Requests.ShouldNotContain(request => request.Path.EndsWith("/forget"));
    }

    [Fact]
    public async Task RunAsync_GrimoireUnavailable_ReportsDegradedOutcome()
    {
        var runner = new GrimoireCompactionRunner(
            new UnavailableMemoryClient(), _chatFactory, NullLogger<GrimoireCompactionRunner>.Instance);

        var result = await runner.RunAsync(CancellationToken.None);

        result.Outcome.ShouldBe(GrimoireCompactionOutcome.GrimoireUnavailable);
        result.Reason.ShouldContain("unavailable");
    }

    private GrimoireCompactionRunner CreateRunner(FixedTextChatClient? client = null)
    {
        _chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns(client);
        return new GrimoireCompactionRunner(_fixture.Client, _chatFactory, NullLogger<GrimoireCompactionRunner>.Instance);
    }

    public void Dispose() => _fixture.Dispose();
}

internal sealed class FixedTextChatClient(string text) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    void IDisposable.Dispose()
    {
    }
}

internal sealed class UnavailableMemoryClient : IGrimoireMemoryClient
{
    public Task<GrimoireMemoryEndpoint> ResolveEndpointAsync(GrimoireTomeTarget target, CancellationToken ct) =>
        throw new GrimoireMemoryException("grimoire memory is unavailable: nothing is listening");

    public Task<GrimoireMemoryWriteResult> RememberAsync(
        GrimoireMemoryEndpoint endpoint, GrimoireMemoryWriteRequest request, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<GrimoireMemoryRecord>> RecallAsync(
        GrimoireMemoryEndpoint endpoint, string query, string? kind, string? tag, int limit, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<GrimoireMemoryRecord>> ListTopFactsAsync(
        GrimoireMemoryEndpoint endpoint, int limit, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<GrimoireCompactionGroup>> GetCompactionCandidatesAsync(
        GrimoireMemoryEndpoint endpoint, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task ForgetAsync(Guid memoryId, string reason, GrimoireMemoryEndpoint endpoint, CancellationToken ct) =>
        throw new NotSupportedException();
}
