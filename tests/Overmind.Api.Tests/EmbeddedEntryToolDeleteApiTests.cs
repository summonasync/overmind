using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Tools;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// The embedded loop's delete_entry used to serialise its reason into the DELETE body, and the route reads the
// reason from the query string — so every tombstone that ever came out of the embedded loop was born without the
// one line the reason exists to write. The MCP twin sends ?reason= and has always worked, which is why no test
// caught it: the two adapters were pinned separately and the seam between them and the route was not.
//
// This file drives the real tool spec against the real Host — no stubbed handler — because a stub echoes the path
// it was handed and cannot tell a bound query parameter from an ignored body.
public sealed class EmbeddedEntryToolDeleteApiTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;

    public EmbeddedEntryToolDeleteApiTests()
    {
        _factory = new ApiFactory();
        _http = _factory.CreateClient();
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task EmbeddedDelete_WithAReason_LandsThatReasonInTheTombstoneBody()
    {
        var (tools, projectId) = await ToolsAsync("embedded-scribe", "qwen");

        var created = await tools.Single(spec => spec.Name == "write_entry").Invoke(
            Args($$"""{"projectId":"{{projectId}}","type":"note","title":"Doomed by the loop","content":"the last body"}"""),
            CancellationToken.None);
        var entryId = created.GetProperty("id").GetString();

        var tombstone = await tools.Single(spec => spec.Name == "delete_entry").Invoke(
            Args($$"""{"entryId":"{{entryId}}","reason":"superseded by the rewrite card"}"""),
            CancellationToken.None);

        var content = tombstone.GetProperty("content").GetString();
        content.ShouldNotBeNull();
        content.Contains("Reason: superseded by the rewrite card").ShouldBeTrue(
            "the embedded path's own tombstone has to carry what the caller passed: " + content);
        content.ShouldContain("via agent door");
        content.ShouldContain("by embedded-scribe (qwen)");
        tombstone.GetProperty("type").GetString().ShouldBe("tombstone");
    }

    [Fact]
    public async Task EmbeddedDelete_WithoutAReason_ProducesNoReasonLineAndStillTombstones()
    {
        // The counter-case that keeps the assertion above from being satisfied by any old body text: the same
        // route, no reason on the wire, and no Reason line in the result. It also proves the fix did not make the
        // parameter mandatory.
        var (tools, projectId) = await ToolsAsync("embedded-quiet", "qwen");

        var created = await tools.Single(spec => spec.Name == "write_entry").Invoke(
            Args($$"""{"projectId":"{{projectId}}","type":"note","title":"Undocumented death","content":"body"}"""),
            CancellationToken.None);

        var tombstone = await tools.Single(spec => spec.Name == "delete_entry").Invoke(
            Args($$"""{"entryId":"{{created.GetProperty("id").GetString()}}"}"""),
            CancellationToken.None);

        var content = tombstone.GetProperty("content").GetString()!;
        content.ShouldNotContain("Reason:");
        content.ShouldContain("via agent door");
        tombstone.GetProperty("title").GetString().ShouldBe("Undocumented death (deleted)");
    }

    [Fact]
    public async Task EmbeddedDelete_OnAJournalRow_SurfacesTheTypedRefusalInsteadOfDeleting()
    {
        // The reason plumbing rides the same request as the refusal path, so prove the gate still answers through
        // the embedded spec rather than the loop swallowing it.
        var (tools, _) = await ToolsAsync("embedded-thief", "qwen");

        var journalProject = await _http.PostAsJsonAsync("/api/entries", new
        {
            Type = "journal",
            Title = "Owner note",
            Content = "not yours",
        });
        journalProject.EnsureSuccessStatusCode();
        var journalId = JsonDocument.Parse(await journalProject.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetGuid();

        var thrown = await Should.ThrowAsync<LoopbackApiException>(() =>
            tools.Single(spec => spec.Name == "delete_entry").Invoke(
                Args($$"""{"entryId":"{{journalId}}","reason":"wanted it gone"}"""),
                CancellationToken.None));

        thrown.StatusCode.ShouldBe(400);
        thrown.Message.ShouldContain("human surface");
    }

    private async Task<(IReadOnlyList<AgentToolSpec> Tools, Guid ProjectId)> ToolsAsync(string alias, string vendor)
    {
        var projectId = (await _http.PostAsJsonAsync("/api/projects", new
        {
            Name = $"embedded-{alias}",
            DiskPath = TestPaths.DiskPath($"embedded-{alias}"),
        })).Content.ReadFromJsonAsync<JsonElement>().GetAwaiter().GetResult().GetProperty("id").GetGuid();

        var session = await _http.PostAsJsonAsync("/api/agent/sessions", new { Alias = alias, Vendor = vendor });
        session.EnsureSuccessStatusCode();
        var sessionId = (await session.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Overmind-Session", sessionId.ToString());

        var tools = EntryAgentTools.Build(new AgentLoopbackClient(_http));
        return (tools, projectId);
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
