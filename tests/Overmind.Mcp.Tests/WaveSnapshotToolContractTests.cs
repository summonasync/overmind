using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class WaveSnapshotToolContractTests : IAsyncLifetime
{
    private static readonly string SnapshotRoute =
        $"/api/agent/projects/{Canned.ProjectId}/planning/wave-snapshot";

    private static readonly string PlainSnapshotJson = new JsonObject
    {
        ["projectId"] = Canned.ProjectId.ToString(),
        ["generatedAt"] = "2026-10-17T12:00:00Z",
        ["coordinationTags"] = new JsonArray("wave-sync", "lane-marshalling"),
        ["queue"] = new JsonArray(new JsonObject { ["slug"] = "demo-feature", ["queuePosition"] = 1 }),
        ["claims"] = new JsonArray(),
        ["codeLocations"] = new JsonArray(),
        ["openBlockers"] = new JsonArray(),
        ["entries"] = new JsonArray()
    }.ToJsonString();

    private static readonly string InFlightSnapshotJson = new JsonObject
    {
        ["projectId"] = Canned.ProjectId.ToString(),
        ["generatedAt"] = "2026-10-17T12:00:00Z",
        ["coordinationTags"] = new JsonArray("wave-sync", "lane-marshalling"),
        ["queue"] = new JsonArray(new JsonObject { ["slug"] = "demo-feature", ["queuePosition"] = 1 }),
        ["claims"] = new JsonArray(new JsonObject
        {
            ["slug"] = "demo-feature",
            ["claimedByAlias"] = "marshal",
            ["claimedByVendor"] = "claude"
        }),
        ["codeLocations"] = new JsonArray(new JsonObject
        {
            ["slug"] = "demo-feature",
            ["pinnedBranch"] = "feat/999-ghost",
            ["pinnedWorktreePath"] = null
        }),
        ["openBlockers"] = new JsonArray(),
        ["entries"] = new JsonArray()
    }.ToJsonString();

    private StubOvermindApi _stub = null!;
    private OvermindApiClient _api = null!;

    public Task InitializeAsync()
    {
        _stub = StubOvermindApi.Start();
        _api = new OvermindApiClient(OvermindApiClient.CreateHttpClient(_stub.BaseUrl));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _stub.DisposeAsync();
    }

    [Fact]
    public async Task WaveSnapshot_MissingProjectId_ThrowsThePlanningGuard()
    {
        var ex = await Should.ThrowAsync<McpException>(() => Tool(new StubGitCli()).WaveSnapshot(null));

        ex.Message.ShouldBe(
            "projectId is required for planning_* tools — entries tools resolve the project from cwd, planning tools do not.");
    }

    [Fact]
    public async Task WaveSnapshot_PassesTheHostPayloadThrough_WithNoInFlightFeatures()
    {
        _stub.RouteGet(SnapshotRoute, PlainSnapshotJson);

        var result = await Tool(new StubGitCli()).WaveSnapshot(Canned.ProjectId);

        result.GetProperty("queue").GetArrayLength().ShouldBe(1);
        result.GetProperty("queue")[0].GetProperty("slug").GetString().ShouldBe("demo-feature");
        result.GetProperty("coordinationTags").GetArrayLength().ShouldBe(2);
        result.TryGetProperty("discovery", out _).ShouldBeFalse();
        result.TryGetProperty("discoveryError", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WaveSnapshot_ForwardsTagsAndEntryLimitThroughTheQuery()
    {
        _stub.Route("GET", SnapshotRoute, 200, PlainSnapshotJson);

        await Tool(new StubGitCli()).WaveSnapshot(Canned.ProjectId, tags: "custom-lane", entryLimit: 5);

        var request = _stub.Requests.Single(r =>
            r.Method == "GET" && r.Path.StartsWith(SnapshotRoute, StringComparison.Ordinal));
        request.Query.ShouldBe("tags=custom-lane&entryLimit=5");
    }

    [Fact]
    public async Task WaveSnapshot_EnrichesInFlightFeaturesWithDiscovery_AndReportsStalePins()
    {
        var git = new StubGitCli();
        git.Stage("worktree list --porcelain", string.Empty);
        git.Stage("for-each-ref", string.Empty);
        _stub.RouteGet(SnapshotRoute, InFlightSnapshotJson);

        var result = await Tool(git).WaveSnapshot(Canned.ProjectId);

        result.GetProperty("workspace").GetString().ShouldBe(Environment.CurrentDirectory);
        var discovery = result.GetProperty("discovery");
        discovery.GetArrayLength().ShouldBe(1);
        var row = discovery[0];
        row.GetProperty("slug").GetString().ShouldBe("demo-feature");
        var matches = row.GetProperty("matches");
        matches.GetArrayLength().ShouldBe(1);
        matches[0].GetProperty("kind").GetString().ShouldBe("branch");
        matches[0].GetProperty("name").GetString().ShouldBe("feat/999-ghost");
        matches[0].GetProperty("stale").GetBoolean().ShouldBeTrue();
        matches[0].GetProperty("pinned").GetBoolean().ShouldBeTrue();
        matches[0].GetProperty("lastCommit").ValueKind.ShouldBe(JsonValueKind.Null);
        result.TryGetProperty("discoveryError", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WaveSnapshot_DiscoveryFailure_DegradesToDiscoveryError()
    {
        var git = new StubGitCli { ExitCode = 128, Error = "fatal: not a git repository" };
        _stub.RouteGet(SnapshotRoute, InFlightSnapshotJson);

        var result = await Tool(git).WaveSnapshot(Canned.ProjectId);

        var discoveryError = result.GetProperty("discoveryError").GetString();
        discoveryError.ShouldNotBeNull();
        discoveryError.ShouldContain("not a git repository");
        result.GetProperty("queue").GetArrayLength().ShouldBe(1);
        result.TryGetProperty("discovery", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WaveSnapshot_CachesTheGitListing_AcrossCalls()
    {
        var git = new StubGitCli();
        git.Stage("worktree list --porcelain", string.Empty);
        git.Stage("for-each-ref", string.Empty);
        _stub.Route("GET", SnapshotRoute, 200, InFlightSnapshotJson);
        var cache = new WaveListingCache();
        var tool = Tool(git, cache);

        await tool.WaveSnapshot(Canned.ProjectId);
        await tool.WaveSnapshot(Canned.ProjectId);

        git.Calls.Count.ShouldBe(2);
    }

    private WaveSnapshotTools Tool(StubGitCli git, WaveListingCache? cache = null) =>
        new(_api, git, cache ?? new WaveListingCache());

    private sealed class StubGitCli : IGitCli
    {
        private readonly Dictionary<string, string> _outputs = new(StringComparer.Ordinal);

        public List<(string WorkingDirectory, string Arguments)> Calls { get; } = [];

        public int ExitCode { get; init; }

        public string Error { get; init; } = string.Empty;

        public void Stage(string argumentsPrefix, string output) => _outputs[argumentsPrefix] = output;

        public Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
        {
            Calls.Add((workingDirectory, arguments));
            var output = _outputs
                .Where(pair => arguments.StartsWith(pair.Key, StringComparison.Ordinal))
                .Select(pair => pair.Value)
                .FirstOrDefault() ?? string.Empty;
            return Task.FromResult(new GitCommandResult(ExitCode, output, Error));
        }
    }
}
