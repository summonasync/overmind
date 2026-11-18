using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Overmind.Host.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// The gate's second rule — what it does with a request it cannot classify — had never been executed by a test:
// every verb registered on the real subgroup is classifiable, so the fail-closed branch was reachable only by a
// route that does not exist yet. This file builds such routes in a throwaway TestServer (test-only, no production
// surface) and asserts the gate refuses them instead of waving them through, with the probe handler counting how
// many times it ran: a pass-through does not merely answer 200, it mutates something the boundary was supposed to
// keep shut.
//
// The mini host carries no OvermindDbContext on purpose: the refusal must not depend on being able to read a row,
// so registering it without a database is itself the assertion that the gate refuses before it looks.
public sealed class JournalWriteGateFailClosedApiTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private int _probeRuns;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder([]);
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        // Same body grammar as the real Host: an enum on the wire arrives as its camelCase name.
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

        _app = builder.Build();

        // Mirrors EntriesApi.MapEntriesApi: the gate hangs on the subgroup, so a verb registered there inherits it.
        var gated = _app.MapGroup("/api/agent").AddEndpointFilter(JournalWriteLockFilter.Instance);
        gated.MapPost("/entries/bodiless", () => Mutated());
        gated.MapPost("/entries/copy", (CopyProbeRequest _) => Mutated());
        gated.MapPost("/entries/{id:guid}/merge", (Guid id, MergeProbeRequest _) => Mutated());
        gated.MapGet("/entries/{id:guid}/read", (Guid id) => Mutated());
        gated.MapPost("/entries/create", (Overmind.Host.Api.Contracts.CreateEntryRequest request) => Mutated());

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task GateVerbWithNoBodyAndNoEntryId_IsRefusedAndTheHandlerNeverRuns()
    {
        var response = await _client.PostAsync("/api/agent/entries/bodiless", Content("{}"));

        await AssertRefused(response, "no entry id addresses it");
        _probeRuns.ShouldBe(0, "a pass-through runs the verb, which is the whole hazard this branch closes");
    }

    [Fact]
    public async Task GateVerbWithABodyTheGateHasNotVetted_IsRefusedWithoutAnEntryId()
    {
        var response = await _client.PostAsync(
            "/api/agent/entries/copy", Content("""{"targetId":"11111111-1111-1111-1111-111111111111"}"""));

        await AssertRefused(response, "its body is a request contract this gate has not vetted");
        _probeRuns.ShouldBe(0);
    }

    [Fact]
    public async Task GateVerbAddressingOneEntryById_ButCarryingASecondTarget_IsRefusedNotClassifiedFromThePrimary()
    {
        // W-C: the merge verb names a non-journal row in {id} and smuggles the row it really also writes in the
        // body. Classifying the primary target alone — what the gate used to do — would wave this through with a
        // green route table. The gate cannot know what an unread body mutates, so it refuses the shape.
        var source = Guid.NewGuid();

        var response = await _client.PostAsync(
            $"/api/agent/entries/{source}/merge",
            Content($$"""{"sourceId":"{{source}}","targetId":"22222222-2222-2222-2222-222222222222"}"""));

        await AssertRefused(response, "its body is a request contract this gate has not vetted");
        _probeRuns.ShouldBe(0, "the refused merge must not reach the verb that would write the secondary target");
    }

    [Fact]
    public async Task GateVerbWithAVettedBody_StillReachesTheHandler_TheVettingIsNotABlanketRefusal()
    {
        // The counter-case that keeps the three refusals above honest: a body the gate has read the semantics of
        // is classified by its type and handed on. Without this arm the tests above would pass on a gate that
        // refuses everything.
        var response = await _client.PostAsync(
            "/api/agent/entries/create",
            Content("""{"projectId":"33333333-3333-3333-3333-333333333333","type":"note","title":"t","content":"c"}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        _probeRuns.ShouldBe(1);
    }

    [Fact]
    public async Task GateVerbWithAVettedBodyNamingJournal_IsRefusedByTheTypeRule()
    {
        var response = await _client.PostAsync(
            "/api/agent/entries/create",
            Content("""{"projectId":"33333333-3333-3333-3333-333333333333","type":"journal","title":"t","content":"c"}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await ReadProblemTitleAsync(response)).ShouldBe("JournalEntryWriteRefused");
        _probeRuns.ShouldBe(0);
    }

    [Fact]
    public async Task ReadVerbOnTheGatedSubgroup_StaysOpenEvenWithAnUnvettedShape()
    {
        var response = await _client.GetAsync($"/api/agent/entries/{Guid.NewGuid()}/read");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        _probeRuns.ShouldBe(1, "reads are the human surface's own visibility, never gated");
    }

    private async Task AssertRefused(HttpResponseMessage response, string because)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        (await ReadProblemTitleAsync(response)).ShouldBe("AgentEntryWriteShapeUndetermined");
        body.Contains(because).ShouldBeTrue(
            $"the refusal has to name which completeness rule fired, got: {body}");
    }

    private static async Task<string?> ReadProblemTitleAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("title", out var title) ? title.GetString() : null;
    }

    private IResult Mutated()
    {
        _probeRuns++;
        return Results.Ok(new { probe = "the verb ran" });
    }

    private static StringContent Content(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");

    public sealed record CopyProbeRequest(Guid TargetId);

    public sealed record MergeProbeRequest(Guid SourceId, Guid TargetId);
}

// INJECTION PROOF (task 12, W-B + W-C). Both directions were run in this tree; the red lines are the ones the
// fail-closed branch protects:
//
//   1. Pass-through — in src/Overmind.Host/Api/JournalWriteLockFilter.cs replace the two
//      `return ShapeUndetermined(...)` statements with `return await next(context)`:
//      GateVerbWithNoBodyAndNoEntryId_IsRefusedAndTheHandlerNeverRuns,
//      GateVerbWithABodyTheGateHasNotVetted_IsRefusedWithoutAnEntryId and
//      GateVerbAddressingOneEntryById_ButCarryingASecondTarget_... all go red (200 instead of the typed 400, and
//      ProbeRuns 1 instead of 0). Before this task existed zero of those shapes, which is exactly why
//      AgentEntryWriteShapeUndetermined could be deleted without a single test noticing.
//
//   2. Vetting dropped — delete the `if (!RequestBodyIsVetted(http))` block only: the two unvetted-body cases go
//      red while the bodiless case stays green, which is how the W-C arm is discriminated from the W-B arm.
//
//   3. Vetted set widened to everything (`RequestBodyIsVetted` returns true) — the merge case goes red, the
//      create cases stay green: the multi-target blind spot is the specific thing that reopens.
//
// Both runs are recorded in the task log at ~/scratch-184/t12-inject-*.log.
