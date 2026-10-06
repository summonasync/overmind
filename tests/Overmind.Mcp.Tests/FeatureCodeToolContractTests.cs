using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class FeatureCodeToolContractTests : IAsyncLifetime
{
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
    public async Task MultipleMatches_ListsWorktreeAndBranch_WithCommitAndFeatureRideAlong()
    {
        var (worktreeDir, tempRoot) = CreateTempWorktree();
        try
        {
            var git = new StubGitCli();
            git.Stage("worktree list --porcelain",
                """
                worktree /repo/main
                HEAD f67669fabc1234567890abcdef1234567890abcd
                branch refs/heads/main

                worktree {{PATH}}
                HEAD 0123456789abcdef0123456789abcdef01234567
                branch refs/heads/feat/063-worktree-feature-binding

                """.Replace("{{PATH}}", worktreeDir));
            git.Stage("for-each-ref",
                "main\tf67669f\t2026-10-05T10:15:00+03:00\n" +
                "feat/063-worktree-feature-binding\tcceb4fe\t2026-10-04T18:40:00Z\n");
            _stub.RouteGet(
                $"/api/projects/{Canned.ProjectId}",
                Canned.ProjectJson.Replace("E:\\\\work\\\\alpha", Directory.GetCurrentDirectory().Replace("\\", "\\\\")));
            _stub.RouteGet(
                $"/api/agent/projects/{Canned.ProjectId}/planning/features/063-worktree-feature-binding",
                Canned.PlanningFeatureJson);

            var result = await Tool(git).FeatureCode("063-worktree-feature-binding", Canned.ProjectId);

            result.GetProperty("hint").ValueKind.ShouldBe(JsonValueKind.Null);
            result.GetProperty("workspaceMatchesProject").GetBoolean().ShouldBeTrue();
            var matches = result.GetProperty("matches");
            matches.GetArrayLength().ShouldBe(2);

            var worktree = matches[0];
            worktree.GetProperty("kind").GetString().ShouldBe("worktree");
            worktree.GetProperty("name").GetString().ShouldBe("feat/063-worktree-feature-binding");
            worktree.GetProperty("path").GetString().ShouldBe(worktreeDir);
            worktree.GetProperty("stale").GetBoolean().ShouldBeFalse();
            worktree.GetProperty("lastCommit").GetProperty("hash").GetString().ShouldBe("cceb4fe");
            worktree.GetProperty("lastCommit").GetProperty("date").GetString().ShouldBe("2026-10-04T18:40:00Z");

            var branch = matches[1];
            branch.GetProperty("kind").GetString().ShouldBe("branch");
            branch.GetProperty("path").ValueKind.ShouldBe(JsonValueKind.Null);
            branch.GetProperty("lastCommit").GetProperty("hash").GetString().ShouldBe("cceb4fe");

            var feature = result.GetProperty("feature");
            feature.GetProperty("status").GetString().ShouldBe("inProgress");
            feature.GetProperty("priority").GetString().ShouldBe("p1");
            result.GetProperty("featureError").ValueKind.ShouldBe(JsonValueKind.Null);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task StaleWorktree_IsReportedNotDropped()
    {
        var git = new StubGitCli();
        git.Stage("worktree list --porcelain",
            """
            worktree /repo/main
            HEAD f67669fabc1234567890abcdef1234567890abcd
            branch refs/heads/main

            worktree /repo/.claude/worktrees/063-swept-away
            HEAD 0123456789abcdef0123456789abcdef01234567
            branch refs/heads/feat/063-worktree-feature-binding

            """);
        git.Stage("for-each-ref", "main\tf67669f\t2026-10-05T10:15:00+03:00\n");
        git.Stage("log -1", "cceb4fe\t2026-10-04T18:40:00Z\n");

        var result = await Tool(git).FeatureCode("063-worktree-feature-binding", Canned.ProjectId);

        var matches = result.GetProperty("matches");
        matches.GetArrayLength().ShouldBe(1);
        var stale = matches[0];
        stale.GetProperty("kind").GetString().ShouldBe("worktree");
        stale.GetProperty("path").GetString().ShouldBe("/repo/.claude/worktrees/063-swept-away");
        stale.GetProperty("stale").GetBoolean().ShouldBeTrue();
        stale.GetProperty("lastCommit").GetProperty("hash").GetString().ShouldBe("cceb4fe");
    }

    [Fact]
    public async Task NoMatch_ReturnsHonestEmpty_WithConventionHint()
    {
        var git = new StubGitCli();
        git.Stage("worktree list --porcelain",
            """
            worktree /repo/main
            HEAD f67669fabc1234567890abcdef1234567890abcd
            branch refs/heads/main

            """);
        git.Stage("for-each-ref", "main\tf67669f\t2026-10-05T10:15:00+03:00\n");

        var result = await Tool(git).FeatureCode("063-worktree-feature-binding", Canned.ProjectId);

        result.GetProperty("matches").GetArrayLength().ShouldBe(0);
        result.GetProperty("hint").GetString().ShouldNotBeNull().ShouldContain("feat/NNN-slug");
    }

    [Fact]
    public async Task HostFeatureReadFails_DiscoveryHalfStillAnswered()
    {
        var (worktreeDir, tempRoot) = CreateTempWorktree();
        try
        {
            var git = new StubGitCli();
            git.Stage("worktree list --porcelain",
                """
                worktree {{PATH}}
                HEAD 0123456789abcdef0123456789abcdef01234567
                branch refs/heads/feat/063-worktree-feature-binding

                """.Replace("{{PATH}}", worktreeDir));
            git.Stage("for-each-ref",
                "feat/063-worktree-feature-binding\tcceb4fe\t2026-10-04T18:40:00Z\n");

            var result = await Tool(git).FeatureCode("063-worktree-feature-binding", Canned.ProjectId);

            result.GetProperty("matches").GetArrayLength().ShouldBe(2);
            result.GetProperty("feature").ValueKind.ShouldBe(JsonValueKind.Null);
            result.GetProperty("featureError").GetString().ShouldNotBeNull().ShouldContain("feature read failed");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DetachedWorktree_CommitEnrichedThroughLogCall()
    {
        var (worktreeDir, tempRoot) = CreateTempWorktree("063-worktree-feature-binding");
        try
        {
            var git = new StubGitCli();
            git.Stage("worktree list --porcelain",
                """
                worktree {{PATH}}
                HEAD deadbeefdeadbeefdeadbeefdeadbeefdeadbeef
                detached

                """.Replace("{{PATH}}", worktreeDir));
            git.Stage("for-each-ref", "main\tf67669f\t2026-10-05T10:15:00+03:00\n");
            git.Stage("log -1", "8d89544\t2026-10-05T09:12:00+03:00\n");

            var result = await Tool(git).FeatureCode("063-worktree-feature-binding", Canned.ProjectId);

            var matches = result.GetProperty("matches");
            matches.GetArrayLength().ShouldBe(1);
            var detached = matches[0];
            detached.GetProperty("kind").GetString().ShouldBe("worktree");
            detached.GetProperty("name").GetString().ShouldBe("063-worktree-feature-binding");
            detached.GetProperty("lastCommit").GetProperty("hash").GetString().ShouldBe("8d89544");
            detached.GetProperty("lastCommit").GetProperty("date").GetString().ShouldBe("2026-10-05T09:12:00+03:00");
            git.Calls.ShouldContain(call => call.Arguments.StartsWith("log -1", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GitFailure_SurfacesHonestErrorWithoutStack()
    {
        var git = new StubGitCli { ExitCode = 128, Error = "fatal: not a git repository (or any of the parent directories): .git" };

        var failure = await Should.ThrowAsync<McpException>(() =>
            Tool(git).FeatureCode("063-worktree-feature-binding", Canned.ProjectId));

        failure.Message.ShouldContain("not a git repository");
        failure.Message.ShouldContain("worktree list --porcelain");
    }

    [Fact]
    public async Task CwdResolution_PostsCreateFalseBody()
    {
        _stub.Route("POST", "/api/onboard", 200, Canned.OnboardJson);
        var git = new StubGitCli();

        await Tool(git).FeatureCode("063-worktree-feature-binding");

        var onboard = _stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/onboard");
        var body = JsonNode.Parse(onboard.Body)!;
        body["cwd"]!.GetValue<string>().ShouldBe(Environment.CurrentDirectory);
        body["create"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task UnregisteredCwd_ThrowsHonestGuidanceWithSingleResolveCall()
    {
        _stub.Route("POST", "/api/onboard", 404, Canned.OnboardUnregisteredCwdJson);
        var git = new StubGitCli();

        var failure = await Should.ThrowAsync<McpException>(() =>
            Tool(git).FeatureCode("063-worktree-feature-binding"));

        failure.Message.ShouldContain("No project is registered for the working directory");
        failure.Message.ShouldContain("overmind_onboard");
        _stub.Requests.Count(r => r.Method == "POST" && r.Path == "/api/onboard").ShouldBe(1);
        git.Calls.ShouldBeEmpty();
    }

    private FeatureCodeTools Tool(StubGitCli git) => new(_api, git, new ProjectResolver(_api));

    private static (string WorktreeDir, string Root) CreateTempWorktree(string leaf = "063-bound")
    {
        var root = Path.Combine(Path.GetTempPath(), "overmind-feature-code", Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, leaf);
        Directory.CreateDirectory(dir);
        return (dir, root);
    }

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
