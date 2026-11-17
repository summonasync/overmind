using System.Net;
using System.Text;
using System.Text.Json;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Tools;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

// The embedded agent's entry tools are thin: they carry no per-verb flag, they call the agent door and surface what
// the route gate answers. Both halves are pinned here — the door each spec calls, and the refusal reaching the loop
// instead of a silent tombstone.
public sealed class EntryAgentToolRouteTests
{
    private static readonly Guid EntryId = Guid.Parse("6b6b6b6b-6b6b-6b6b-6b6b-6b6b6b6b6b6b");

    [Fact]
    public async Task DeleteEntrySpec_CallsTheAgentDoor_NotTheHumanRoute()
    {
        var recorder = new RecordingHandler(() => JsonResponse(HttpStatusCode.OK, """{"id":"x"}"""));
        var spec = Spec(recorder, "delete_entry");

        await spec.Invoke(Args($$"""{"entryId":"{{EntryId}}","reason":"retired"}"""), CancellationToken.None);

        recorder.Requests.Single().Method.ShouldBe(HttpMethod.Delete);
        recorder.Requests.Single().Path.ShouldBe($"/api/agent/entries/{EntryId}");
        recorder.Requests.ShouldNotContain(r => r.Path == $"/api/entries/{EntryId}");
    }

    [Fact]
    public async Task DeleteEntrySpec_SurfacesTheTypedJournalRefusal()
    {
        var recorder = new RecordingHandler(() => JsonResponse(
            HttpStatusCode.BadRequest,
            """{"status":400,"title":"JournalEntryWriteRefused","detail":"Journal entries are the human surface."}"""));
        var spec = Spec(recorder, "delete_entry");

        var thrown = await Should.ThrowAsync<LoopbackApiException>(
            () => spec.Invoke(Args($$"""{"entryId":"{{EntryId}}"}"""), CancellationToken.None));

        // The loopback client surfaces detail over title, so the code itself is proven at the route (API tests);
        // what this pins is that the embedded delete cannot swallow the refusal into a silent success.
        thrown.StatusCode.ShouldBe(400);
        thrown.Message.ShouldContain("human surface");
        thrown.Message.ShouldContain($"/api/agent/entries/{EntryId}");
        recorder.Requests.Single().Path.ShouldBe($"/api/agent/entries/{EntryId}");
    }

    [Fact]
    public async Task WriteAndUpdateSpecs_StayOnTheAgentDoor()
    {
        var recorder = new RecordingHandler(() => JsonResponse(HttpStatusCode.Created, """{"id":"x"}"""));

        await Spec(recorder, "write_entry").Invoke(
            Args($$"""{"projectId":"{{Guid.NewGuid()}}","type":"note","title":"t","content":"c"}"""),
            CancellationToken.None);
        await Spec(recorder, "update_entry").Invoke(
            Args($$"""{"entryId":"{{EntryId}}","content":"c"}"""),
            CancellationToken.None);

        recorder.Requests.Select(r => r.Path).ShouldBe(["/api/agent/entries", $"/api/agent/entries/{EntryId}"]);
    }

    [Fact]
    public void DeleteEntrySpec_DescriptionNamesTheDoorAndTheRefusal()
    {
        var specs = EntryAgentTools.Build(new AgentLoopbackClient(new HttpClient()));

        var delete = specs.Single(s => s.Name == "delete_entry");
        delete.Description.ShouldContain("agent door");
        delete.Description.ShouldContain("JournalEntryWriteRefused");

        // The prose list of writable types must keep naming every type the door accepts, and no more.
        var write = specs.Single(s => s.Name == "write_entry");
        foreach (var type in EntryTypeSurface.AgentWritable)
        {
            write.Description.ShouldContain(EntryTypeSurface.Normalize(type));
        }

        // The prose clause that enumerates what the door writes must list every writable type and nothing gated.
        // "journal" appears elsewhere in the sentence (naming what is refused), so the check is scoped to the list.
        var typeClause = write.Description.Split(';', StringSplitOptions.RemoveEmptyEntries)[0];
        typeClause.ShouldNotContain("journal");
    }

    private static AgentToolSpec Spec(RecordingHandler handler, string name) =>
        EntryAgentTools.Build(new AgentLoopbackClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost"),
        })).Single(s => s.Name == name);

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class RecordingHandler(Func<HttpResponseMessage> respond) : DelegatingHandler
    {
        public List<(HttpMethod Method, string Path)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            return Task.FromResult(respond());
        }
    }
}
