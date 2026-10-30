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

    private static readonly string FullSnapshotJson = new JsonObject
    {
        ["projectId"] = Canned.ProjectId.ToString(),
        ["generatedAt"] = "2026-10-17T12:00:00Z",
        ["coordinationTags"] = new JsonArray("wave-sync", "lane-marshalling"),
        ["queue"] = new JsonArray(new JsonObject
        {
            ["slug"] = "demo-feature",
            ["number"] = "42",
            ["title"] = "Demo feature",
            ["priority"] = "p1",
            ["status"] = "inProgress",
            ["queuePosition"] = 2,
            ["specStatus"] = "validated",
            ["planStatus"] = "draft",
            ["taskDone"] = 1,
            ["taskTotal"] = 2,
            ["taskCounts"] = new JsonObject { ["todo"] = 0, ["inProgress"] = 1, ["review"] = 0, ["done"] = 1, ["blocked"] = 0 },
            ["readiness"] = 2,
            ["blockedBy"] = new JsonArray("gate-locked"),
            ["isBlocked"] = true,
            ["claimedByAlias"] = "marshal",
            ["claimedByVendor"] = "claude",
            ["claimedAt"] = "2026-10-17T09:00:00Z",
            ["claimState"] = "alive",
            ["claimedByHuman"] = false,
            ["phase"] = "in progress",
            ["briefExcerpt"] = "Crash on save when the workspace file is read-only.",
            ["openDirectiveCount"] = 1,
            ["createdAt"] = "2026-09-08T09:00:00Z",
            ["updatedAt"] = "2026-10-17T08:00:00Z",
            ["lane"] = "frontend",
            ["laneBoundAlias"] = "johan",
            ["potentialOverlaps"] = new JsonArray(new JsonObject
            {
                ["withFeatureSlug"] = "other-feature",
                ["sharedGlobs"] = new JsonArray("src/web/**")
            }),
            ["pendingMutations"] = 0,
            ["lastAttemptAt"] = "2026-10-17T10:00:00Z",
            ["lastSuccessAt"] = "2026-10-17T10:00:00Z",
            ["stale"] = false
        }),
        ["claims"] = new JsonArray(new JsonObject
        {
            ["slug"] = "demo-feature",
            ["claimedByAlias"] = "marshal",
            ["claimedByVendor"] = "claude",
            ["claimedAt"] = "2026-10-17T09:00:00Z",
            ["claimState"] = "alive",
            ["claimedByHuman"] = false,
            ["lastActivityAt"] = "2026-10-17T11:00:00Z",
            ["queuePosition"] = 2,
            ["priority"] = "p1",
            ["status"] = "inProgress"
        }),
        ["codeLocations"] = new JsonArray(),
        ["openBlockers"] = new JsonArray(new JsonObject
        {
            ["slug"] = "demo-feature",
            ["openBlockers"] = new JsonArray("gate-locked")
        }),
        ["entries"] = new JsonArray(new JsonObject
        {
            ["id"] = "5c1d0000-0000-0000-0000-000000000001",
            ["type"] = "note",
            ["title"] = "wave standup",
            ["tags"] = new JsonArray("wave-sync"),
            ["snippet"] = "lane-a moving, marshal holds demo-feature",
            ["updatedAt"] = "2026-10-17T11:30:00Z"
        })
    }.ToJsonString();

    private static readonly string UnclaimedDraftSnapshotJson = new JsonObject
    {
        ["projectId"] = Canned.ProjectId.ToString(),
        ["generatedAt"] = "2026-10-17T12:00:00Z",
        ["queue"] = new JsonArray(BuildQueueRow(1, claimed: false)),
        ["claims"] = new JsonArray(),
        ["codeLocations"] = new JsonArray(),
        ["openBlockers"] = new JsonArray(),
        ["entries"] = new JsonArray()
    }.ToJsonString();

    private static readonly string LargeSnapshotJson = BuildSnapshot(BuildQueueRows(50));

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
            "projectId is required for this tool — unlike most tools it does not fall back to OVERMIND_PROJECT_ID or the working directory.");
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
    public async Task WaveSnapshot_CompactProjectsQueueRowsToCoordinationFields()
    {
        _stub.RouteGet(SnapshotRoute, FullSnapshotJson);

        var result = await Tool(new StubGitCli()).WaveSnapshot(Canned.ProjectId, compact: true);

        var row = result.GetProperty("queue").EnumerateArray().Single();
        row.GetProperty("slug").GetString().ShouldBe("demo-feature");
        row.GetProperty("number").GetString().ShouldBe("42");
        row.GetProperty("priority").GetString().ShouldBe("p1");
        row.GetProperty("status").GetString().ShouldBe("inProgress");
        row.GetProperty("phase").GetString().ShouldBe("in progress");
        row.GetProperty("readiness").GetInt32().ShouldBe(2);
        row.GetProperty("taskDone").GetInt32().ShouldBe(1);
        row.GetProperty("taskTotal").GetInt32().ShouldBe(2);
        row.GetProperty("isBlocked").GetBoolean().ShouldBeTrue();
        row.GetProperty("blockedBy").EnumerateArray().Single().GetString().ShouldBe("gate-locked");
        row.GetProperty("claimedByAlias").GetString().ShouldBe("marshal");
        row.GetProperty("claimState").GetString().ShouldBe("alive");
        row.GetProperty("lane").GetString().ShouldBe("frontend");
        row.GetProperty("laneBoundAlias").GetString().ShouldBe("johan");
        row.GetProperty("openDirectiveCount").GetInt32().ShouldBe(1);
        row.TryGetProperty("stale", out _).ShouldBeFalse();
        row.TryGetProperty("claimedByHuman", out _).ShouldBeFalse();
        row.TryGetProperty("laneHolder", out _).ShouldBeFalse();
        row.GetProperty("potentialOverlaps")[0].GetProperty("withFeatureSlug").GetString().ShouldBe("other-feature");

        row.TryGetProperty("briefExcerpt", out _).ShouldBeFalse();
        row.TryGetProperty("taskCounts", out _).ShouldBeFalse();
        row.TryGetProperty("specStatus", out _).ShouldBeFalse();
        row.TryGetProperty("planStatus", out _).ShouldBeFalse();
        row.TryGetProperty("createdAt", out _).ShouldBeFalse();
        row.TryGetProperty("updatedAt", out _).ShouldBeFalse();
        row.TryGetProperty("pendingMutations", out _).ShouldBeFalse();
        row.TryGetProperty("lastAttemptAt", out _).ShouldBeFalse();
        row.TryGetProperty("lastSuccessAt", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WaveSnapshot_CompactTrimsClaimsAndEntries()
    {
        _stub.RouteGet(SnapshotRoute, FullSnapshotJson);

        var result = await Tool(new StubGitCli()).WaveSnapshot(Canned.ProjectId, compact: true);

        var claim = result.GetProperty("claims").EnumerateArray().Single();
        claim.GetProperty("slug").GetString().ShouldBe("demo-feature");
        claim.GetProperty("claimedByAlias").GetString().ShouldBe("marshal");
        claim.GetProperty("claimedAt").GetString().ShouldBe("2026-10-17T09:00:00Z");
        claim.GetProperty("claimState").GetString().ShouldBe("alive");
        claim.TryGetProperty("lastActivityAt", out _).ShouldBeFalse();

        var blockers = result.GetProperty("openBlockers").EnumerateArray().Single();
        blockers.GetProperty("slug").GetString().ShouldBe("demo-feature");
        blockers.GetProperty("openBlockers").EnumerateArray().Single().GetString().ShouldBe("gate-locked");

        var entry = result.GetProperty("entries").EnumerateArray().Single();
        entry.GetProperty("title").GetString().ShouldBe("wave standup");
        entry.GetProperty("tags").EnumerateArray().Single().GetString().ShouldBe("wave-sync");
        entry.TryGetProperty("snippet", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WaveSnapshot_CompactKeepsAnUnclaimedDraftRowSparse_WithNoNullsOrEmptyArrays()
    {
        _stub.RouteGet(SnapshotRoute, UnclaimedDraftSnapshotJson);

        var result = await Tool(new StubGitCli()).WaveSnapshot(Canned.ProjectId, compact: true);

        var row = result.GetProperty("queue").EnumerateArray().Single();
        foreach (var property in row.EnumerateObject())
        {
            property.Value.ValueKind.ShouldNotBe(JsonValueKind.Null);
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                property.Value.GetArrayLength().ShouldBeGreaterThan(0);
            }
        }

        row.GetProperty("slug").GetString().ShouldBe("feature-001");
        row.GetProperty("readiness").GetInt32().ShouldBe(2);
        row.GetProperty("lane").GetString().ShouldBe("platform");
        row.TryGetProperty("claimedByAlias", out _).ShouldBeFalse();
        row.TryGetProperty("claimedByHuman", out _).ShouldBeFalse();
        row.TryGetProperty("taskTotal", out _).ShouldBeFalse();
        row.TryGetProperty("isBlocked", out _).ShouldBeFalse();
        row.TryGetProperty("openDirectiveCount", out _).ShouldBeFalse();
        row.TryGetProperty("stale", out _).ShouldBeFalse();
        row.TryGetProperty("blockedBy", out _).ShouldBeFalse();
        row.TryGetProperty("potentialOverlaps", out _).ShouldBeFalse();
        row.TryGetProperty("briefExcerpt", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WaveSnapshot_CompactHalvesTheRawLengthOfALargeCannedSnapshot()
    {
        _stub.RouteGet(SnapshotRoute, LargeSnapshotJson);

        var full = await Tool(new StubGitCli()).WaveSnapshot(Canned.ProjectId);
        var compact = await Tool(new StubGitCli()).WaveSnapshot(Canned.ProjectId, compact: true);

        compact.GetRawText().Length.ShouldBeLessThanOrEqualTo(full.GetRawText().Length / 2);
    }

    [Fact]
    public async Task WaveSnapshot_CompactKeepsDiscoveryAndUnknownTopLevelProperties()
    {
        var git = new StubGitCli();
        git.Stage("worktree list --porcelain", string.Empty);
        git.Stage("for-each-ref", string.Empty);
        _stub.RouteGet(SnapshotRoute, InFlightSnapshotJson);

        var result = await Tool(git).WaveSnapshot(Canned.ProjectId, compact: true);

        result.GetProperty("workspace").GetString().ShouldBe(Environment.CurrentDirectory);
        var discovery = result.GetProperty("discovery");
        discovery.GetArrayLength().ShouldBe(1);
        discovery[0].GetProperty("slug").GetString().ShouldBe("demo-feature");
        result.GetProperty("queue")[0].GetProperty("slug").GetString().ShouldBe("demo-feature");
        result.TryGetProperty("discoveryError", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WaveSnapshot_CompactDefaultsToFalse_KeepingTheFullPayload()
    {
        _stub.RouteGet(SnapshotRoute, FullSnapshotJson);

        var defaulted = await Tool(new StubGitCli()).WaveSnapshot(Canned.ProjectId);
        var explicitOff = await Tool(new StubGitCli()).WaveSnapshot(Canned.ProjectId, compact: false);

        foreach (var result in new[] { defaulted, explicitOff })
        {
            var row = result.GetProperty("queue").EnumerateArray().Single();
            row.GetProperty("briefExcerpt").GetString()
                .ShouldBe("Crash on save when the workspace file is read-only.");
            row.GetProperty("taskCounts").GetProperty("todo").GetInt32().ShouldBe(0);
            row.GetProperty("createdAt").GetString().ShouldBe("2026-09-08T09:00:00Z");
            result.GetProperty("claims").EnumerateArray().Single()
                .GetProperty("lastActivityAt").GetString().ShouldBe("2026-10-17T11:00:00Z");
            result.GetProperty("entries").EnumerateArray().Single()
                .GetProperty("snippet").GetString().ShouldContain("lane-a moving");
        }
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

    private static string BuildSnapshot(JsonArray queue) => new JsonObject
    {
        ["projectId"] = Canned.ProjectId.ToString(),
        ["generatedAt"] = "2026-10-17T12:00:00Z",
        ["queue"] = queue,
        ["claims"] = new JsonArray(),
        ["codeLocations"] = new JsonArray(),
        ["openBlockers"] = new JsonArray(),
        ["entries"] = new JsonArray()
    }.ToJsonString();

    private static JsonArray BuildQueueRows(int count)
    {
        var rows = new JsonArray();
        for (var position = 1; position <= count; position++)
        {
            rows.Add(BuildQueueRow(position, claimed: position % 2 == 0));
        }

        return rows;
    }

    private static JsonObject BuildQueueRow(int position, bool claimed) => new()
    {
        ["slug"] = $"feature-{position:000}",
        ["number"] = position.ToString(),
        ["title"] = $"Feature {position}",
        ["priority"] = "p2",
        ["status"] = "ready",
        ["queuePosition"] = position,
        ["specStatus"] = "validated",
        ["planStatus"] = "validated",
        ["taskDone"] = 0,
        ["taskTotal"] = 0,
        ["taskCounts"] = new JsonObject { ["todo"] = 0, ["inProgress"] = 0, ["review"] = 0, ["done"] = 0, ["blocked"] = 0 },
        ["readiness"] = 2,
        ["blockedBy"] = new JsonArray(),
        ["isBlocked"] = false,
        ["claimedByAlias"] = claimed ? "marshal" : null,
        ["claimedByVendor"] = claimed ? "claude" : null,
        ["claimedByHuman"] = false,
        ["claimedAt"] = claimed ? "2026-10-17T09:00:00Z" : null,
        ["claimState"] = claimed ? "alive" : null,
        ["phase"] = "plan validated",
        ["briefExcerpt"] = "Crash on save when the workspace file is read-only.",
        ["openDirectiveCount"] = 0,
        ["createdAt"] = "2026-09-08T09:00:00Z",
        ["updatedAt"] = "2026-10-17T08:00:00Z",
        ["lane"] = "platform",
        ["laneBoundAlias"] = null,
        ["laneHolder"] = null,
        ["potentialOverlaps"] = new JsonArray(),
        ["pendingMutations"] = 0,
        ["lastAttemptAt"] = null,
        ["lastSuccessAt"] = "2026-10-17T10:00:00Z",
        ["stale"] = false
    };

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
