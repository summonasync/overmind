using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class DiscoveryFastFailTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string Workspace = Directory.CreateTempSubdirectory("overmind-nongit").FullName;
    private readonly string OriginalDirectory = Environment.CurrentDirectory;
    private readonly IGitCli Git = Substitute.For<IGitCli>();

    public DiscoveryFastFailTests()
    {
        Git.When(g => g.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("git must not run on a non-repository workspace"));
        Environment.CurrentDirectory = Workspace;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = OriginalDirectory;
        try
        {
            Directory.Delete(Workspace, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task WaveSnapshot_NonRepository_ReportsDiscoveryError_KeepsBoardHalves()
    {
        var snapshot = new JsonObject
        {
            ["queue"] = new JsonArray(),
            ["claims"] = new JsonArray(),
            ["openBlockers"] = new JsonArray(),
            ["codeLocations"] = new JsonArray { new JsonObject { ["slug"] = "demo-feature" } }
        };
        var tools = new WaveSnapshotTools(Api(_ => Json(200, snapshot)), Git, new WaveListingCache());

        var result = await tools.WaveSnapshot(Canned.ProjectId);

        var payload = JsonNode.Parse(result.GetRawText())!;
        payload["discoveryError"]!.GetValue<string>().ShouldContain("not inside a git repository");
        payload["queue"].ShouldNotBeNull();
        payload["claims"].ShouldNotBeNull();
        payload["discovery"].ShouldBeNull();
    }

    [Fact]
    public async Task FeatureCode_NonRepository_DegradesDiscovery_AnswersFeatureContext()
    {
        var project = new JsonObject { ["id"] = Canned.ProjectId, ["diskPath"] = "/elsewhere/demo" };
        var feature = new JsonObject
        {
            ["title"] = "Demo feature",
            ["status"] = "ready",
            ["priority"] = "p2",
            ["updatedAt"] = "2026-10-27T10:00:00Z"
        };
        var api = Api(request =>
        {
            var body = request.RequestUri!.AbsolutePath.Contains("/planning/features/")
                ? feature
                : project;
            return Json(200, body);
        });
        var tools = new FeatureCodeTools(api, Git, new ProjectResolver(api));

        var result = await tools.FeatureCode("demo-feature", Canned.ProjectId);

        var payload = JsonNode.Parse(result.GetRawText())!;
        payload["discoveryError"]!.GetValue<string>().ShouldContain("not inside a git repository");
        payload["matches"]!.AsArray().Count.ShouldBe(0);
        payload["feature"]!["title"]!.GetValue<string>().ShouldBe("Demo feature");
    }

    private static OvermindApiClient Api(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new FakeHandler(respond))
        {
            BaseAddress = new Uri("http://127.0.0.1:4761"),
        });

    private static HttpResponseMessage Json(int status, JsonNode body) =>
        new((System.Net.HttpStatusCode)status)
        {
            Content = new StringContent(body.ToJsonString(JsonOptions), Encoding.UTF8, "application/json")
        };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
