using System.Text;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

[Collection(nameof(ProcessEnvironment))]
public sealed class ProjectResolverPinTests : IDisposable
{
    private static readonly Guid PinnedProjectId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly string Workspace = Directory.CreateTempSubdirectory("overmind-pin-resolver").FullName;
    private readonly string? OriginalOverride = Environment.GetEnvironmentVariable("OVERMIND_PROJECT_ID");
    private readonly RecordingHandler Handler = new();

    public ProjectResolverPinTests()
    {
        Environment.SetEnvironmentVariable("OVERMIND_PROJECT_ID", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("OVERMIND_PROJECT_ID", OriginalOverride);
        try
        {
            Directory.Delete(Workspace, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Resolve_PinnedProjectRegistered_ReturnsPinnedIdAcrossFreshResolversWithoutEnv()
    {
        Directory.CreateDirectory(Path.Combine(Workspace, ".git"));
        WritePin(PinnedProjectId.ToString());
        Handler.Respond = _ => Json(200, OnboardPayload(PinnedProjectId));

        var first = await ResolveAsync();
        var second = await ResolveAsync();

        first.ShouldBe(PinnedProjectId);
        second.ShouldBe(PinnedProjectId);
        Handler.Bodies.Count.ShouldBe(2);
        Handler.Bodies[0]["projectId"]!.GetValue<Guid>().ShouldBe(PinnedProjectId);
        Handler.Bodies[1]["projectId"]!.GetValue<Guid>().ShouldBe(PinnedProjectId);
    }

    [Fact]
    public async Task Resolve_PinnedProjectUnknown_WarnsAndFallsThroughToCwdMatch()
    {
        Directory.CreateDirectory(Path.Combine(Workspace, ".git"));
        WritePin(PinnedProjectId.ToString());
        Handler.Respond = body => body.ContainsKey("projectId")
            ? Json(404, new JsonObject { ["detail"] = $"Project '{PinnedProjectId}' was not found." })
            : Json(200, OnboardPayload(Canned.ProjectId));

        var (resolved, warning) = await CaptureStderrAsync(ResolveAsync);

        resolved.ShouldBe(Canned.ProjectId);
        Handler.Bodies.Count.ShouldBe(2);
        Handler.Bodies[0].ContainsKey("projectId").ShouldBeTrue();
        Handler.Bodies[1].ContainsKey("projectId").ShouldBeFalse();
        warning.ShouldContain("WARN");
        warning.ShouldContain(PinnedProjectId.ToString());
    }

    [Fact]
    public async Task Resolve_CorruptPin_WarnsAndGoesStraightToCwdMatch()
    {
        Directory.CreateDirectory(Path.Combine(Workspace, ".git"));
        WritePin("this is not a pin");
        Handler.Respond = _ => Json(200, OnboardPayload(Canned.ProjectId));

        var (resolved, warning) = await CaptureStderrAsync(ResolveAsync);

        resolved.ShouldBe(Canned.ProjectId);
        Handler.Bodies.ShouldHaveSingleItem().ContainsKey("projectId").ShouldBeFalse();
        warning.ShouldContain("WARN");
        warning.ShouldContain("unparsable");
        warning.ShouldContain(".overmind");
    }

    [Fact]
    public async Task Resolve_NoPin_CwdMatchUnchangedWithoutWarnings()
    {
        Handler.Respond = _ => Json(200, OnboardPayload(Canned.ProjectId));

        var (resolved, warning) = await CaptureStderrAsync(ResolveAsync);

        resolved.ShouldBe(Canned.ProjectId);
        Handler.Bodies.ShouldHaveSingleItem().ContainsKey("projectId").ShouldBeFalse();
        warning.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resolve_EnvOverride_WinsOverPinWithoutHostCall()
    {
        WritePin(PinnedProjectId.ToString());
        Environment.SetEnvironmentVariable("OVERMIND_PROJECT_ID", Canned.ProjectId.ToString());
        Handler.Respond = _ => throw new InvalidOperationException("env override must resolve before any host call");

        var resolved = await ResolveAsync();

        resolved.ShouldBe(Canned.ProjectId);
        Handler.Bodies.ShouldBeEmpty();
    }

    private Task<Guid> ResolveAsync() =>
        Resolver().ResolveAsync(null, true, "project", CancellationToken.None, Workspace);

    private ProjectResolver Resolver() =>
        new(new OvermindApiClient(new HttpClient(Handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:4761"),
        }));

    private void WritePin(string content)
    {
        Directory.CreateDirectory(Path.Combine(Workspace, ".overmind"));
        File.WriteAllText(Path.Combine(Workspace, ".overmind", "project"), content);
    }

    private static JsonObject OnboardPayload(Guid projectId) => new()
    {
        ["projectId"] = projectId.ToString(),
        ["projectName"] = "demo",
    };

    private static async Task<(Guid Resolved, string Warning)> CaptureStderrAsync(Func<Task<Guid>> resolve)
    {
        var warning = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(warning);
        try
        {
            return (await resolve(), warning.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    private static HttpResponseMessage Json(int status, JsonNode body) =>
        new((System.Net.HttpStatusCode)status)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<JsonObject> Bodies { get; } = [];

        public Func<JsonObject, HttpResponseMessage> Respond { get; set; } = _ => throw new InvalidOperationException("respond not configured");

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = JsonNode.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult())!.AsObject();
            Bodies.Add(body);
            return Task.FromResult(Respond(body));
        }
    }
}
