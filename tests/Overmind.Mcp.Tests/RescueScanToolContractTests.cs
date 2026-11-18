using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using NSubstitute;
using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

[Collection(nameof(ProcessEnvironment))]
public sealed class RescueScanToolContractTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string DeadBranch = "feat/235-dead-claim";
    private const string MainSha = "1111111111111111111111111111111111111111";

    private readonly string _root = Directory.CreateTempSubdirectory("overmind-rescue-root").FullName;
    private readonly string _scanRoot;
    private readonly string _worktree;
    private readonly string _originalDirectory = Environment.CurrentDirectory;
    private readonly List<(string Directory, string Arguments)> _gitCalls = [];

    public RescueScanToolContractTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        _worktree = Path.Combine(_root, "235-bound");
        Directory.CreateDirectory(_worktree);
        _scanRoot = Path.TrimEndingDirectorySeparator(_root);
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDirectory;
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Scan_PinnedWorktreeWithUnpushedWork_IsRescueWorthyAndQuotesTheFacts()
    {
        var tools = Tools(Git(
            ("status --porcelain", 0, " M src/a.cs\n?? src/b.cs\n M src/c.cs\n"),
            ("rev-parse --abbrev-ref", 0, $"origin/{DeadBranch}\n"),
            ("rev-list --count", 0, "2\n"),
            ("merge-base --is-ancestor", 1, ""),
            ("log -1", 0, "cceb4fe\t2026-11-16T18:40:00Z\n")), [DeadClaim()]);

        var payload = await ScanAsync(tools);
        var row = Only(payload);

        row["slug"]!.GetValue<string>().ShouldBe("235-dead-claim");
        row["claimState"]!.GetValue<string>().ShouldBe("ended");
        row["claimedByAlias"]!.GetValue<string>().ShouldBe("mark-wolff");
        row["claimedByVendor"]!.GetValue<string>().ShouldBe("claude");
        row["code"]!["source"]!.GetValue<string>().ShouldBe(RescueCodeLocation.SourcePin);
        row["code"]!["worktreeExists"]!.GetValue<bool>().ShouldBe(true);
        row["code"]!["branchExists"]!.GetValue<bool>().ShouldBe(true);
        row["code"]!["stale"]!.GetValue<bool>().ShouldBe(false);
        row["code"]!["measuredIn"]!.GetValue<string>().ShouldBe(_worktree);
        row["git"]!["dirtyFiles"]!.GetValue<int>().ShouldBe(3);
        row["git"]!["untrackedFiles"]!.GetValue<int>().ShouldBe(1);
        row["git"]!["unpushedCommits"]!.GetValue<int>().ShouldBe(2);
        row["git"]!["unpushedBase"]!.GetValue<string>().ShouldBe($"origin/{DeadBranch}");
        row["git"]!["unmerged"]!.GetValue<bool>().ShouldBe(true);
        row["git"]!["mergedInto"]!.GetValue<string>().ShouldBe("origin/main");
        row["git"]!["lastCommit"]!["hash"]!.GetValue<string>().ShouldBe("cceb4fe");
        row["git"]!["lastCommit"]!["date"]!.GetValue<string>().ShouldBe("2026-11-16T18:40:00Z");
        row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.RescueWorthy);
        row["rescueAllowed"]!.GetValue<bool>().ShouldBe(true);
        Reasons(row).ShouldContain("3 uncommitted files in the worktree");
        Reasons(row).ShouldContain($"2 unpushed commits over origin/{DeadBranch}");
        Reasons(row).ShouldContain("not merged into origin/main");
        _gitCalls.ShouldContain(call =>
            call.Directory == _worktree && call.Arguments.StartsWith("status", StringComparison.Ordinal));

        payload["counts"]!["rescueWorthy"]!.GetValue<int>().ShouldBe(1);
        payload["counts"]!["candidates"]!.GetValue<int>().ShouldBe(1);
        payload["scanRoot"]!["source"]!.GetValue<string>().ShouldBe("project-disk-path");
        payload["scanRoot"]!["path"]!.GetValue<string>().ShouldBe(_scanRoot);
        payload["policy"]!["autonomy"]!.GetValue<string>().ShouldBe(RescueScanSource.OwnerSet);
        payload["policy"]!["text"]!.GetValue<string>().ShouldBe(Project.DefaultRescuePolicy);
        payload["generatedAt"]!.GetValue<string>().ShouldBe("2026-11-17T18:00:00Z");
        payload.ContainsKey("discoveryError").ShouldBeFalse();
    }

    [Fact]
    public async Task Scan_BranchWithoutUpstream_CountsAheadOfTheMainlineFallback()
    {
        var tools = Tools(Git(
            ("status --porcelain", 0, ""),
            ("rev-parse --abbrev-ref", 128, ""),
            ("rev-list --count", 0, "4\n"),
            ("merge-base --is-ancestor", 0, ""),
            ("log -1", 0, "aaaaaaa\t2026-11-15T09:00:00Z\n")), [DeadClaim()]);

        var row = Only(await ScanAsync(tools));

        row["git"]!["unpushedBase"]!.GetValue<string>().ShouldBe("origin/main");
        row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.RescueWorthy);
        Reasons(row).ShouldContain("4 unpushed commits over origin/main");
    }

    [Fact]
    public async Task Scan_WorktreeGoneBranchAlive_IsStaleMarkedAndStillCountsTheBranch()
    {
        const string swept = "/repo/.lanes/235-swept";
        var tools = Tools(Git(
            ("for-each-ref", 0, "main\nfeat/235-swept\n"),
            ("rev-parse --abbrev-ref", 128, ""),
            ("rev-list --count", 0, "1\n"),
            ("merge-base --is-ancestor", 1, ""),
            ("log -1", 0, "bbbbbbb\t2026-11-14T09:00:00Z\n")),
            [Candidate("235-swept", "ended", swept, "feat/235-swept")]);

        var row = Only(await ScanAsync(tools));

        row["code"]!["worktreePath"]!.GetValue<string>().ShouldBe(swept);
        row["code"]!["worktreeExists"]!.GetValue<bool>().ShouldBe(false);
        row["code"]!["stale"]!.GetValue<bool>().ShouldBe(true);
        row["git"]!["dirtyFiles"].ShouldBeNull();
        row["git"]!["unpushedCommits"]!.GetValue<int>().ShouldBe(1);
        row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.RescueWorthy);
        Reasons(row).ShouldContain($"worktree path {swept} is gone, only the branch is left");
        NoGitCall("status");
        _gitCalls.ShouldContain(call =>
            call.Directory == _scanRoot && call.Arguments.StartsWith("rev-list", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Scan_WorktreeAndBranchBothGone_IsStaleAndQueriesNothingButTheListing()
    {
        var tools = Tools(Git(("for-each-ref", 0, "main\n")),
            [Candidate("235-nothing-left", "ended", "/repo/.lanes/235-nothing-left", "feat/235-nothing-left")]);

        var row = Only(await ScanAsync(tools));

        row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.Stale);
        row["git"]!["dirtyFiles"].ShouldBeNull();
        row["git"]!["unpushedCommits"].ShouldBeNull();
        row["git"]!["error"]!.GetValue<string>().ShouldContain("no branch or worktree");
        Reasons(row).Single().ShouldContain("no branch or worktree");
        NoGitCall("rev-list");
        NoGitCall("status");
    }

    [Fact]
    public async Task Scan_WithoutPins_FallsBackToTheNamingConventionDiscovery()
    {
        var tools = Tools(Git(
            ("worktree list --porcelain", 0,
                $"worktree {_root}\nHEAD {MainSha}\nbranch refs/heads/main\n\n" +
                $"worktree {_worktree}\nHEAD {new string('2', 40)}\nbranch refs/heads/feat/235-convention\n\n"),
            ("for-each-ref", 0, "main\nfeat/235-convention\n"),
            ("status --porcelain", 0, " M only.cs\n"),
            ("rev-parse --abbrev-ref", 128, ""),
            ("rev-list --count", 0, "0\n"),
            ("merge-base --is-ancestor", 1, ""),
            ("log -1", 0, "ccccccc\t2026-11-13T09:00:00Z\n")),
            [Candidate("235-convention", "stale", null, null)]);

        var row = Only(await ScanAsync(tools));

        row["code"]!["source"]!.GetValue<string>().ShouldBe(RescueCodeLocation.SourceDiscovery);
        row["code"]!["branch"]!.GetValue<string>().ShouldBe("feat/235-convention");
        row["code"]!["worktreePath"]!.GetValue<string>().ShouldBe(_worktree);
        row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.RescueWorthy);
        Reasons(row).ShouldContain("1 uncommitted file in the worktree");
    }

    [Fact]
    public async Task Scan_ConventionMatchesNothingLocally_IsStaleWithTheReason()
    {
        var tools = Tools(Git(("for-each-ref", 0, "main\nfeat/900-unrelated\n")),
            [Candidate("235-unmatched", "stale", null, null)]);

        var row = Only(await ScanAsync(tools));

        row["code"]!["source"]!.GetValue<string>().ShouldBe(RescueCodeLocation.SourceNone);
        row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.Stale);
        Reasons(row).Single().ShouldContain("no branch or worktree");
    }

    [Fact]
    public async Task Scan_NothingDirtyOrAhead_IsCleanAndNamesEveryHalfIncludingTheFailure()
    {
        var tools = Tools(Git(
            ("status --porcelain", 128, "fatal: this worktree is unreadable"),
            ("rev-parse --abbrev-ref", 0, $"origin/{DeadBranch}\n"),
            ("rev-list --count", 0, "0\n"),
            ("merge-base --is-ancestor", 0, ""),
            ("log -1", 0, "ddddddd\t2026-11-12T09:00:00Z\n")), [DeadClaim()]);

        var row = Only(await ScanAsync(tools));

        row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.Clean);
        row["git"]!["dirtyFiles"].ShouldBeNull();
        Reasons(row).ShouldContain("worktree state unmeasurable");
        Reasons(row).ShouldContain($"nothing ahead of origin/{DeadBranch}");
        Reasons(row).ShouldContain("already merged into origin/main");
        Reasons(row).ShouldContain(reason => reason.Contains("status --porcelain failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Scan_SweptWorktreeWithNeitherBaseNorMainline_IsUnknownAndNeverClaimsMergedOrPushed()
    {
        const string swept = "/repo/.lanes/235-swept-no-remote";
        var tools = Tools(
            GitStub(
                [
                    ("for-each-ref", 0, "main\nfeat/235-swept-no-remote\n"),
                    ("rev-parse --abbrev-ref", 128, ""),
                    ("log -1", 0, "eeeeeee\t2026-11-11T09:00:00Z\n"),
                ],
                ["origin/main", "main", "master"],
                null),
            [Candidate("235-swept-no-remote", "ended", swept, "feat/235-swept-no-remote")]);

        var payload = await ScanAsync(tools);
        var row = Only(payload);

        row["code"]!["stale"]!.GetValue<bool>().ShouldBe(true);
        row["git"]!["dirtyFiles"].ShouldBeNull();
        row["git"]!["unpushedCommits"].ShouldBeNull();
        row["git"]!["unmerged"].ShouldBeNull();
        row["git"]!["lastCommit"]!["hash"]!.GetValue<string>().ShouldBe("eeeeeee");
        row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.Unknown);
        Reasons(row).ShouldContain("worktree state unmeasurable");
        Reasons(row).ShouldContain("unpushed count unknown");
        Reasons(row).ShouldContain("merge state unknown");
        Reasons(row).ShouldNotContain(reason => reason.Contains("merged and pushed", StringComparison.Ordinal));
        Reasons(row).ShouldNotContain(reason => reason.Contains("nothing ahead of", StringComparison.Ordinal));
        payload["counts"]!["clean"]!.GetValue<int>().ShouldBe(0);
        payload["counts"]!["unknown"]!.GetValue<int>().ShouldBe(1);
    }

    [Fact]
    public async Task Scan_WorktreeMeasuredCleanWithTheBranchHalvesUnknown_IsStillCleanAndTheGuardDoesNotOvercorrect()
    {
        var tools = Tools(
            GitStub(
                [
                    ("status --porcelain", 0, ""),
                    ("rev-parse --abbrev-ref", 128, ""),
                    ("log -1", 0, "fffffff\t2026-11-10T09:00:00Z\n"),
                ],
                ["origin/main", "main", "master"],
                null),
            [DeadClaim()]);

        var row = Only(await ScanAsync(tools));

        row["git"]!["dirtyFiles"]!.GetValue<int>().ShouldBe(0);
        row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.Clean);
        Reasons(row).ShouldContain("nothing uncommitted in the worktree");
        Reasons(row).ShouldContain("unpushed count unknown");
        Reasons(row).ShouldContain("merge state unknown");
    }

    [Fact]
    public async Task Scan_StaleRowWithMergeMeasuredButPushUnknown_IsCleanWithoutClaimingThePush()
    {
        const string swept = "/repo/.lanes/235-half-known";
        var tools = Tools(
            GitStub(
                [
                    ("for-each-ref", 0, "main\nfeat/235-half-known\n"),
                    ("rev-parse --abbrev-ref", 128, ""),
                    ("merge-base --is-ancestor", 0, ""),
                    ("log -1", 0, "ggggggg\t2026-11-09T09:00:00Z\n"),
                ],
                ["origin/main"],
                null),
            [Candidate("235-half-known", "ended", swept, "feat/235-half-known")]);

        var row = Only(await ScanAsync(tools));

        row["code"]!["stale"]!.GetValue<bool>().ShouldBe(true);
        row["git"]!["unpushedCommits"].ShouldBeNull();
        row["git"]!["unmerged"]!.GetValue<bool>().ShouldBe(false);
        row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.Clean);
        Reasons(row).ShouldContain("already merged into main");
        Reasons(row).ShouldNotContain(reason => reason.Contains("merged and pushed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Scan_NoUpstreamAndNoMainlineAnywhere_LeavesUnpushedUnknownWithoutCountingAndSaysWhy()
    {
        var tools = Tools(
            GitStub(
                [
                    ("status --porcelain", 0, " M orphaned.cs\n"),
                    ("rev-parse --abbrev-ref", 128, ""),
                    ("log -1", 0, "hhhhhhh\t2026-11-08T09:00:00Z\n"),
                ],
                ["origin/main", "main", "master"],
                null),
            [DeadClaim()]);

        var row = Only(await ScanAsync(tools));

        row["git"]!["unpushedCommits"].ShouldBeNull();
        row["git"]!["unpushedBase"].ShouldBeNull();
        row["git"]!["error"]!.GetValue<string>()
            .ShouldContain("origin/main is not present locally, so unpushed work cannot be counted");
        row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.RescueWorthy);
        NoGitCall("rev-list");
    }

    [Fact]
    public async Task Scan_ListingCommandFails_DegradesEveryRowAndNeverStartsPerRowGit()
    {
        foreach (var (prefix, message) in new[]
                 {
                     ("worktree list", "fatal: cannot read worktree list"),
                     ("for-each-ref", "fatal: cannot iterate branches"),
                 })
        {
            _gitCalls.Clear();
            var tools = Tools(Git((prefix, 128, message)), [DeadClaim()]);
            var payload = await ScanAsync(tools);

            payload["discoveryError"]!.GetValue<string>().ShouldContain("listing failed");
            payload["counts"]!["unknown"]!.GetValue<int>().ShouldBe(1);
            var row = Only(payload);
            row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.Unknown);
            row["rescueAllowed"]!.GetValue<bool>().ShouldBe(false);
            row["code"].ShouldBeNull();
            row["git"].ShouldBeNull();
            NoGitCall("status");
            NoGitCall("rev-list");
        }
    }

    [Fact]
    public async Task Scan_GitNeverAnswersOnOneRow_DegradesThatRowAndKeepsTheSiblingFacts()
    {
        var healthy = Path.Combine(_root, "235-healthy");
        Directory.CreateDirectory(healthy);
        var tools = Tools(
            GitStub(
                [
                    ("status --porcelain", 0, " M survivor.cs\n"),
                    ("rev-parse --abbrev-ref", 0, $"origin/{DeadBranch}\n"),
                    ("rev-list --count", 0, "3\n"),
                    ("merge-base --is-ancestor", 1, ""),
                    ("log -1", 0, "iiiiiii\t2026-11-07T09:00:00Z\n"),
                ],
                [],
                _worktree),
            [
                Candidate("235-hung", "ended", _worktree, DeadBranch),
                Candidate("235-dead-claim", "ended", healthy, DeadBranch),
            ]);

        var payload = await ScanAsync(tools);
        var rows = payload["candidates"]!.AsArray();
        rows.Count.ShouldBe(2);

        var hung = rows[0]!.AsObject();
        hung["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.Unknown);
        hung["git"]!["dirtyFiles"].ShouldBeNull();
        hung["git"]!["error"]!.GetValue<string>().ShouldContain("git scan failed for 235-hung");
        Reasons(hung).ShouldContain(reason => reason.Contains("timed out", StringComparison.Ordinal));

        var survivor = rows[1]!.AsObject();
        survivor["git"]!["dirtyFiles"]!.GetValue<int>().ShouldBe(1);
        survivor["git"]!["unpushedCommits"]!.GetValue<int>().ShouldBe(3);
        survivor["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.RescueWorthy);

        payload["counts"]!["unknown"]!.GetValue<int>().ShouldBe(1);
        payload["counts"]!["rescueWorthy"]!.GetValue<int>().ShouldBe(1);
    }

    [Fact]
    public async Task Scan_NonRepositoryAnywhere_DegradesHonestlyAndKeepsEveryBoardRow()
    {
        var stray = Directory.CreateTempSubdirectory("overmind-rescue-nongit").FullName;
        Environment.CurrentDirectory = stray;
        try
        {
            var tools = Tools(
                Substitute.For<IGitCli>(),
                [Candidate("235-orphan", "ended", _worktree, DeadBranch)],
                diskPath: Path.Combine(stray, "plain-folder"));

            var payload = await ScanAsync(tools);

            payload["discoveryError"]!.GetValue<string>().ShouldContain("no git repository");
            payload["scanRoot"]!["source"]!.GetValue<string>().ShouldBe("adapter-workspace");
            var row = Only(payload);
            row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.Unknown);
            row["rescueAllowed"]!.GetValue<bool>().ShouldBe(false);
            row["title"]!.GetValue<string>().ShouldBe("235-orphan");
            row["claimState"]!.GetValue<string>().ShouldBe("ended");
            row["code"].ShouldBeNull();
            row["git"].ShouldBeNull();
        }
        finally
        {
            Directory.Delete(stray, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_ClearedPolicy_GrantsNoAutonomyInsteadOfReSeedingTheDefault()
    {
        var tools = Tools(Git(), [DeadClaim()], ClaimSettings(null));

        var payload = await ScanAsync(tools);

        payload["policy"]!["text"].ShouldBeNull();
        payload["policy"]!["autonomy"]!.GetValue<string>().ShouldBe(RescueScanSource.NotSet);
        payload["policy"]!["guidance"]!.GetValue<string>().ShouldContain("deliberate clearing");
        Only(payload)["rescueAllowed"]!.GetValue<bool>().ShouldBe(false);
    }

    [Fact]
    public async Task Scan_UnreadablePolicy_IsUnknownAndGrantsNothing()
    {
        var tools = Tools(Git(), [DeadClaim()], claimSettingsStatus: 503);

        var payload = await ScanAsync(tools);

        payload["policy"]!["autonomy"]!.GetValue<string>().ShouldBe(RescueScanSource.Unreadable);
        payload["policy"]!["error"]!.GetValue<string>().ShouldContain("claim-settings did not answer");
        Only(payload)["rescueAllowed"]!.GetValue<bool>().ShouldBe(false);
    }

    [Fact]
    public async Task Scan_SlugFilter_NarrowsRowsAndAMissCarriesTheHint()
    {
        var tools = Tools(Git(), [
            Candidate("235-one", "stale", _worktree, "feat/235-one"),
            Candidate("235-two", "ended", _worktree, "feat/235-two")]);

        var filtered = await ScanAsync(tools, "235-two");
        Only(filtered)["slug"]!.GetValue<string>().ShouldBe("235-two");

        var miss = await ScanAsync(tools, "235-absent");
        miss["candidates"]!.AsArray().Count.ShouldBe(0);
        miss["hint"]!.GetValue<string>().ShouldContain("235-absent");
        miss["counts"]!["candidates"]!.GetValue<int>().ShouldBe(0);
    }

    [Fact]
    public async Task Scan_EmptyDoor_AnswersTheHintWithoutTouchingGit()
    {
        var tools = Tools(Substitute.For<IGitCli>(), []);

        var payload = await ScanAsync(tools);

        payload["candidates"]!.AsArray().Count.ShouldBe(0);
        payload["hint"]!.GetValue<string>().ShouldContain("nothing to rescue");
        payload["counts"]!.AsObject().Count.ShouldBe(5);
    }

    [Fact]
    public async Task Scan_UnknownProject_StopsOnTheDoorAnswer()
    {
        var tools = Tools(Git(), [DeadClaim()], candidatesStatus: 404);

        var failure = await Should.ThrowAsync<McpException>(() => tools.RescueScan(projectId: Canned.ProjectId));

        failure.Message.ShouldContain("rescue-candidates door");
    }

    [Fact]
    public async Task Scan_UnsafePinnedRef_RefusesTheQueryAndSaysWhy()
    {
        var tools = Tools(Git(), [Candidate("235-poisoned", "ended", _worktree, "--output=/tmp/evil")]);

        var row = Only(await ScanAsync(tools));

        row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.Unknown);
        Reasons(row).Single().ShouldContain("not a safe git ref name");
        NoGitCall("status");
        NoGitCall("rev-list");
    }

    private async Task<JsonObject> ScanAsync(RescueScanTools tools, string? slug = null) =>
        (JsonObject)JsonNode.Parse((await tools.RescueScan(slug, Canned.ProjectId)).GetRawText())!;

    private static JsonObject Only(JsonObject payload) =>
        payload["candidates"]!.AsArray().Single()!.AsObject();

    private static IReadOnlyList<string> Reasons(JsonObject row) =>
        row["reasons"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();

    private void NoGitCall(string prefix) =>
        _gitCalls.ShouldNotContain(call => call.Arguments.StartsWith(prefix, StringComparison.Ordinal));

    private JsonObject DeadClaim() => Candidate("235-dead-claim", "ended", _worktree, DeadBranch);

    private RescueScanTools Tools(
        IGitCli git,
        JsonObject[] rows,
        JsonObject? claimSettings = null,
        int claimSettingsStatus = 200,
        int candidatesStatus = 200,
        string? diskPath = null)
    {
        var door = new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["generatedAt"] = "2026-11-17T18:00:00Z",
            ["candidates"] = new JsonArray([.. rows.Select(row => (JsonNode)row)]),
        };

        var http = new HttpClient(new FakeHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/planning/rescue-candidates", StringComparison.Ordinal))
            {
                return candidatesStatus == 200
                    ? Json(200, door)
                    : Json(404, new JsonObject { ["title"] = "Not Found" });
            }

            if (path.EndsWith("/claim-settings", StringComparison.Ordinal))
            {
                return claimSettingsStatus == 200
                    ? Json(200, claimSettings ?? ClaimSettings(Project.DefaultRescuePolicy))
                    : Json(claimSettingsStatus, new JsonObject { ["title"] = "claim settings offline" });
            }

            if (path.EndsWith($"/projects/{Canned.ProjectId}", StringComparison.Ordinal))
            {
                return Json(200, new JsonObject
                {
                    ["id"] = Canned.ProjectId.ToString(),
                    ["name"] = "alpha",
                    ["diskPath"] = diskPath ?? _root,
                });
            }

            throw new InvalidOperationException($"unstubbed host call: {request.Method} {path}");
        }))
        {
            BaseAddress = new Uri("http://127.0.0.1:4761"),
        };

        var api = new OvermindApiClient(http);

        return new RescueScanTools(api, git, new ProjectResolver(api));
    }

    private static JsonObject Candidate(string slug, string claimState, string? worktreePath, string? branch) =>
        new()
        {
            ["slug"] = slug,
            ["title"] = slug,
            ["status"] = "inProgress",
            ["claimState"] = claimState,
            ["claimedByAlias"] = "mark-wolff",
            ["claimedByVendor"] = "claude",
            ["claimedAt"] = "2026-11-16T18:00:00Z",
            ["claimedByHuman"] = false,
            ["pinnedBranch"] = branch,
            ["pinnedWorktreePath"] = worktreePath,
            ["updatedAt"] = "2026-11-17T09:00:00Z",
        };

    private static JsonObject ClaimSettings(string? rescuePolicy) =>
        new()
        {
            ["claimStaleThresholdMinutes"] = 15,
            ["agentNamingRules"] = null,
            ["rescuePolicy"] = rescuePolicy,
        };

    private IGitCli Git(params (string Prefix, int Exit, string Output)[] rows) => GitStub(rows, [], null);

    private IGitCli GitStub(
        (string Prefix, int Exit, string Output)[] rows,
        string[] absentRefs,
        string? throwsIn)
    {
        (string Prefix, int Exit, string Output)[] listing =
        [
            ("worktree list --porcelain", 0, $"worktree {_root}\nHEAD {MainSha}\nbranch refs/heads/main\n\n"),
            ("for-each-ref", 0, $"main\n{DeadBranch}\nfeat/235-one\nfeat/235-two\n"),
            ("rev-parse --verify", 0, "ignored\n"),
        ];

        var stages = rows.Concat(listing).ToArray();
        var git = Substitute.For<IGitCli>();
        git.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var directory = call.ArgAt<string>(0);
                var arguments = call.ArgAt<string>(1);
                _gitCalls.Add((directory, arguments));

                if (throwsIn is not null && directory == throwsIn)
                {
                    throw new McpException($"git {arguments} timed out after 10s in {directory}.");
                }

                if (absentRefs.Length > 0
                    && arguments.StartsWith("rev-parse --verify", StringComparison.Ordinal)
                    && absentRefs.Contains(arguments[(arguments.LastIndexOf(' ') + 1)..], StringComparer.Ordinal))
                {
                    return new GitCommandResult(1, "stub says no such ref", "");
                }

                var match = stages.FirstOrDefault(stage =>
                    arguments.StartsWith(stage.Prefix, StringComparison.Ordinal));

                return match.Prefix is null
                    ? new GitCommandResult(128, "", $"unstubbed git call: {arguments}")
                    : new GitCommandResult(match.Exit, match.Output, match.Exit == 0 ? "" : $"stub refused: {arguments}");
            });

        return git;
    }

    private static HttpResponseMessage Json(int status, JsonNode body) =>
        new((System.Net.HttpStatusCode)status)
        {
            Content = new StringContent(body.ToJsonString(JsonOptions), Encoding.UTF8, "application/json"),
        };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
