using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Overmind.Host;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

[Collection(nameof(ProcessEnvironment))]
public sealed class OnboardApiTests : IDisposable, IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OnboardApiTests()
    {
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var put = await _client.PutAsJsonAsync("/api/settings", new { PlanningMaterializationSync = "on" });
        put.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Onboard_Quickstart_TombstoneAwareness_PointsAtBriefing()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.Quickstart.ShouldContain("call overmind_briefing");
        onboard.Quickstart.ShouldContain("and the tombstone list");
        onboard.Quickstart.ShouldContain("Check tombstones before proposing anything that smells like a resurrect");
        onboard.Quickstart.ShouldContain("deleted entries are buried there with title, provenance and final content, not erased");
    }

    [Fact]
    public async Task Onboard_CwdMatchingDiskPath_ReturnsExistingProjectCreatedFalse()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var project = await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.ProjectId.ShouldBe(project.Id);
        onboard.ProjectName.ShouldBe(name);
        onboard.Created.ShouldBeFalse();
        onboard.Board.CountsByStatus.ShouldBeEmpty();
    }

    [Fact]
    public async Task Onboard_TrailingSlashCwd_MatchesExistingProject()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var project = await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) + "/" });

        onboard.ProjectId.ShouldBe(project.Id);
        onboard.Created.ShouldBeFalse();
    }

    [Fact]
    public async Task Onboard_NonNormalizedCwdWithDotSegment_MatchesExistingProject()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var project = await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) + "/./" });

        onboard.ProjectId.ShouldBe(project.Id);
        onboard.Created.ShouldBeFalse();
    }

    [Fact]
    public async Task Onboard_CwdThroughSymlink_MatchesResolvedDiskPath()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var realPath = Path.Combine(Path.GetTempPath(), $"overmind-onboard-real-{Guid.NewGuid():N}");
        var linkPath = Path.Combine(Path.GetTempPath(), $"overmind-onboard-link-{Guid.NewGuid():N}");
        Directory.CreateDirectory(realPath);
        Directory.CreateSymbolicLink(linkPath, realPath);

        try
        {
            var project = await CreateProjectAsync($"onboard-{Guid.NewGuid():N}", realPath);

            var onboard = await OnboardAsync(new { Cwd = linkPath });

            onboard.ProjectId.ShouldBe(project.Id);
            onboard.Created.ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(linkPath);
            Directory.Delete(realPath);
        }
    }

    [Fact]
    public async Task Onboard_UnknownCwd_CreatesProjectNamedAfterDirectory()
    {
        var name = $"onboard-{Guid.NewGuid():N}";

        var onboard = await OnboardAsync(new { Cwd = TestPaths.RegisteredDiskPath(name) });

        onboard.ProjectId.ShouldNotBe(Guid.Empty);
        onboard.ProjectName.ShouldBe(name);
        onboard.Created.ShouldBeTrue();
        onboard.Board.CountsByStatus.ShouldBeEmpty();
    }

    [Fact]
    public async Task Onboard_SecondCallWithSameCwd_ReturnsCreatedFalseWithSameProject()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var cwd = TestPaths.RegisteredDiskPath(name);
        var first = await OnboardAsync(new { Cwd = cwd });

        var second = await OnboardAsync(new { Cwd = cwd });

        second.ProjectId.ShouldBe(first.ProjectId);
        second.ProjectName.ShouldBe(name);
        second.Created.ShouldBeFalse();
    }

    [Fact]
    public async Task Onboard_NameCollisionWithDifferentPath_AppendsNumericSuffix()
    {
        var existingPath = Path.Combine(TestPaths.DiskPath($"parent-{Guid.NewGuid():N}"), "dup");
        await CreateProjectAsync("dup", existingPath);

        var onboard = await OnboardAsync(new
        {
            Cwd = Path.Combine(TestPaths.RegisteredDiskPath($"parent-{Guid.NewGuid():N}"), "dup"),
        });

        onboard.Created.ShouldBeTrue();
        onboard.ProjectName.ShouldBe("dup-2");
    }

    [Fact]
    public async Task Onboard_RepeatedNameCollisions_AppendIncrementingSuffix()
    {
        await CreateProjectAsync("dup", Path.Combine(TestPaths.DiskPath($"parent-{Guid.NewGuid():N}"), "dup"));
        await CreateProjectAsync("dup-2", Path.Combine(TestPaths.DiskPath($"parent-{Guid.NewGuid():N}"), "dup"));

        var onboard = await OnboardAsync(new
        {
            Cwd = Path.Combine(TestPaths.RegisteredDiskPath($"parent-{Guid.NewGuid():N}"), "dup"),
        });

        onboard.Created.ShouldBeTrue();
        onboard.ProjectName.ShouldBe("dup-3");
    }

    [Fact]
    public async Task Onboard_ExplicitProjectId_OverridesCwdMatch()
    {
        var firstName = $"onboard-{Guid.NewGuid():N}";
        var first = await CreateProjectAsync(firstName, TestPaths.DiskPath(firstName));
        var secondName = $"onboard-{Guid.NewGuid():N}";
        await CreateProjectAsync(secondName, TestPaths.DiskPath(secondName));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(secondName), ProjectId = first.Id });

        onboard.ProjectId.ShouldBe(first.Id);
        onboard.ProjectName.ShouldBe(firstName);
        onboard.Created.ShouldBeFalse();
    }

    [Fact]
    public async Task Onboard_ExplicitProjectIdForUnknownGuid_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/onboard",
            new { Cwd = TestPaths.DiskPath($"onboard-{Guid.NewGuid():N}"), ProjectId = Guid.NewGuid() });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Status.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Onboard_CreateFalseWithUnregisteredCwd_Returns404GuidanceAndInsertsNothing()
    {
        var before = await _client.GetFromJsonAsync<ProjectJson[]>("/api/projects", JsonOptions);
        var cwd = TestPaths.DiskPath($"onboard-{Guid.NewGuid():N}");

        var response = await _client.PostAsJsonAsync("/api/onboard", new { Cwd = cwd, Create = false });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("overmind_onboard");
        problem.Detail.ShouldContain("projectId");
        problem.Detail.ShouldContain(cwd);
        var after = await _client.GetFromJsonAsync<ProjectJson[]>("/api/projects", JsonOptions);
        after!.Length.ShouldBe(before!.Length);
    }

    [Fact]
    public async Task Onboard_CreateFalseWithRegisteredCwd_ReturnsExistingProjectCreatedFalse()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var project = await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name), Create = false });

        onboard.ProjectId.ShouldBe(project.Id);
        onboard.ProjectName.ShouldBe(name);
        onboard.Created.ShouldBeFalse();
    }

    [Theory]
    [InlineData("/tmp")]
    [InlineData("/var/tmp")]
    public async Task Onboard_CreateFromTransientSystemTempCwd_ReturnsGuidanceAndLeavesListUnchanged(string root)
    {
        var before = await _client.GetFromJsonAsync<ProjectJson[]>("/api/projects", JsonOptions);
        var cwd = Path.Combine(root, $"onboard-{Guid.NewGuid():N}");

        var response = await _client.PostAsJsonAsync("/api/onboard", new { Cwd = cwd });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain(cwd);
        problem.Detail.ShouldContain("project root");
        problem.Detail.ShouldContain("OVERMIND_PROJECT_ID");
        var after = await _client.GetFromJsonAsync<ProjectJson[]>("/api/projects", JsonOptions);
        after!.Length.ShouldBe(before!.Length);
    }

    [Fact]
    public async Task Onboard_CreateFromTmpDirEnvCwd_ReturnsGuidanceAndLeavesListUnchanged()
    {
        var previous = Environment.GetEnvironmentVariable("TMPDIR");
        var tmpRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            $"overmind-tmpdir-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable("TMPDIR", tmpRoot);
        try
        {
            var before = await _client.GetFromJsonAsync<ProjectJson[]>("/api/projects", JsonOptions);
            var cwd = Path.Combine(tmpRoot, $"onboard-{Guid.NewGuid():N}");

            var response = await _client.PostAsJsonAsync("/api/onboard", new { Cwd = cwd });

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
            problem!.Detail.ShouldNotBeNull();
            problem.Detail.ShouldContain(cwd);
            var after = await _client.GetFromJsonAsync<ProjectJson[]>("/api/projects", JsonOptions);
            after!.Length.ShouldBe(before!.Length);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TMPDIR", previous);
        }
    }

    [Fact]
    public async Task Onboard_CreateFromTempCwdWithExplicitProjectId_ResolvesWithoutCreating()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var project = await CreateProjectAsync(name, TestPaths.RegisteredDiskPath(name));
        var tempCwd = Path.Combine(Path.GetTempPath(), $"onboard-{Guid.NewGuid():N}");

        var onboard = await OnboardAsync(new { Cwd = tempCwd, ProjectId = project.Id });

        onboard.ProjectId.ShouldBe(project.Id);
        onboard.Created.ShouldBeFalse();
    }

    [Fact]
    public async Task Onboard_CreateOmittedWithUnregisteredCwd_StillCreatesProject()
    {
        var name = $"onboard-{Guid.NewGuid():N}";

        var onboard = await OnboardAsync(new { Cwd = TestPaths.RegisteredDiskPath(name) });

        onboard.Created.ShouldBeTrue();
        onboard.ProjectName.ShouldBe(name);
    }

    [Fact]
    public async Task Onboard_BoardSummary_CountsByStatus()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var project = await CreateProjectAsync(name, TestPaths.DiskPath(name));
        await CreateFeatureAsync(project.Id, "feat-a", "Feature A");
        await CreateFeatureAsync(project.Id, "feat-b", "Feature B");
        await UpdateFeatureStatusAsync(project.Id, "feat-b", "InProgress");

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.Board.CountsByStatus.Count.ShouldBe(2);
        onboard.Board.CountsByStatus["draft"].ShouldBe(1);
        onboard.Board.CountsByStatus["inProgress"].ShouldBe(1);
    }

    [Fact]
    public async Task Onboard_RescueBlock_SurfacesOwnerDirectiveForFreshProject()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.Rescue.PolicyText.ShouldBe(
            "everything unpushed may be pushed, everything unmerged I allow merging");
    }

    [Fact]
    public async Task Onboard_RescueBlock_SurfacesEditedPolicy()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var project = await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var put = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/claim-settings",
            new
            {
                ClaimStaleThresholdMinutes = 15,
                AgentNamingRules = (string?)null,
                RescuePolicy = "rescue only after review passes",
            });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.Rescue.PolicyText.ShouldBe("rescue only after review passes");
    }

    [Fact]
    public async Task Onboard_Quickstart_MentionsToolOrderAndEscapeHatch()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.Quickstart.ShouldContain("onboard");
        onboard.Quickstart.ShouldContain("list_features");
        onboard.Quickstart.ShouldContain("current_task");
        onboard.Quickstart.ShouldContain("upsert_artifact");
        onboard.Quickstart.ShouldContain("set_status");
        onboard.Quickstart.ShouldContain("OVERMIND_PROJECT_ID");
        onboard.Quickstart.IndexOf("list_features", StringComparison.Ordinal)
            .ShouldBeLessThan(onboard.Quickstart.IndexOf("current_task", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Onboard_Quickstart_CoversMaterializationAndValidationGate()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.Quickstart.ShouldContain("the BACKLOG/ tree under the project root catches up shortly after each planning mutation");
        onboard.Quickstart.ShouldContain("the mirror is eventually consistent");
        onboard.Quickstart.ShouldContain("lastSuccess (null = never materialized) and lastAttempt (null = never attempted)");
        onboard.Quickstart.ShouldContain("humanGate is on, planning_validate returns STOP");
    }

    [Fact]
    public async Task Onboard_Quickstart_CoversTheClaimsWorkflow()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.Quickstart.ShouldContain("planning_claim_feature the moment you take it into active work");
        onboard.Quickstart.ShouldContain("before the first feature mutation (artifact upsert, status move, priority move)");
        onboard.Quickstart.ShouldContain("sub-agents inherit the lock through the shared adapter session");
        onboard.Quickstart.ShouldContain("must not claim or unclaim themselves");
        onboard.Quickstart.ShouldContain("the error names the holder");
        onboard.Quickstart.ShouldContain("ask the user before taking over");
        onboard.Quickstart.ShouldContain("stale or ended → take-over is legitimate");
        onboard.Quickstart.ShouldContain("Claims release themselves — a dead adapter process");
        onboard.Quickstart.ShouldContain("unclaim when you hand off or close the wave");
    }

    [Fact]
    public async Task Onboard_ValidationBlock_OffState_SaysLiveApprovalProofIsRequired()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.Validation.HumanGate.ShouldBeFalse();
        onboard.Validation.Guidance.ShouldContain("agent validate requires a live approval proof");
        onboard.Validation.Guidance.ShouldContain("overmind_notify(kind=approval)");
        onboard.Validation.Guidance.ShouldContain("a human reply newer than the artifact's last update");
        onboard.Validation.Guidance.ShouldContain("409 ValidationProofRequired");
        onboard.Validation.Guidance.ShouldContain("humanGate=on would STOP agents at the UI door instead");
    }

    [Fact]
    public async Task Onboard_ValidationBlock_OnState_SaysAgentsStopUntilTheHumanValidates()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var project = await CreateProjectAsync(name, TestPaths.DiskPath(name));
        await SetWorkflowAsync(project.Id, validation: "on");

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.Validation.HumanGate.ShouldBeTrue();
        onboard.Validation.Guidance.ShouldBe("agents STOP until a human validates the artifact in the UI.");
    }

    [Fact]
    public async Task Onboard_Quickstart_DocumentsTheValidationGateModel()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.Quickstart.ShouldContain("Validation gates: three doors");
        onboard.Quickstart.ShouldContain(
            "agent validate requires a live approval proof: overmind_notify(kind=approval) with a human reply " +
            "newer than the artifact's last update, without it planning_validate returns 409 ValidationProofRequired");
        onboard.Quickstart.ShouldContain("the UI validate door takes no proof, the human validates the artifact directly");
        onboard.Quickstart.ShouldContain(
            "the UI done tap needs a result artifact plus a fresh approval proof newer than the result's last update");
        onboard.Quickstart.ShouldContain("Any body mutation of the artifact voids a standing approval proof");
        onboard.Quickstart.ShouldContain("asMigrated=true is the escape hatch for artifacts already validated on disk before an import");
    }

    [Fact]
    public async Task Onboard_MaterializationBlock_OnStrategy_ResolvesWithTargetDirAndNeverSplit()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var diskPath = TestPaths.DiskPath(name);
        var project = await CreateProjectAsync(name, diskPath);
        await SetWorkflowAsync(project.Id, materialization: "on", validation: "on");
        await SetMaterializationSyncAsync("off");

        var onboard = await OnboardAsync(new { Cwd = diskPath });

        onboard.Materialization.ShouldNotBeNull();
        onboard.Materialization.Strategy.ShouldBe("on");
        onboard.Materialization.TargetDir.ShouldBe(Path.Combine(diskPath, "BACKLOG"));
        onboard.Materialization.Trigger.ShouldContain("after every planning mutation");
        onboard.Materialization.LastSuccess.ShouldBeNull();
        onboard.Materialization.LastAttempt.ShouldBeNull();
        onboard.Validation.HumanGate.ShouldBeTrue();
    }

    [Fact]
    public async Task Onboard_MaterializationBlock_ArchiveStrategy_TargetDirIsArchiveRootNotRepoBacklog()
    {
        var name = $"onboard-archive-{Guid.NewGuid():N}";
        var diskPath = TestPaths.DiskPath(name);
        var project = await CreateProjectAsync(name, diskPath);
        await SetWorkflowAsync(project.Id, materialization: "archive");
        await SetMaterializationSyncAsync("off");

        var onboard = await OnboardAsync(new { Cwd = diskPath });

        onboard.Materialization.Strategy.ShouldBe("archive");
        onboard.Materialization.TargetDir.ShouldBe(Path.Combine(OvermindPaths.DataDirectory, "projects", name, "backlog"));
        onboard.Materialization.TargetDir.ShouldNotBe(Path.Combine(diskPath, "BACKLOG"));
        onboard.Materialization.Trigger.ShouldContain("after every planning mutation");
        onboard.Materialization.LastSuccess.ShouldBeNull();
        onboard.Materialization.LastAttempt.ShouldBeNull();
    }

    [Fact]
    public async Task Onboard_MaterializationBlock_OffStrategy_StillEmittedWithWillNotRunTrigger()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var diskPath = TestPaths.DiskPath(name);
        var project = await CreateProjectAsync(name, diskPath);
        await SetWorkflowAsync(project.Id, materialization: "off");

        var onboard = await OnboardAsync(new { Cwd = diskPath });

        onboard.Materialization.ShouldNotBeNull();
        onboard.Materialization.Strategy.ShouldBe("off");
        onboard.Materialization.TargetDir.ShouldBe(Path.Combine(diskPath, "BACKLOG"));
        onboard.Materialization.Trigger.ShouldContain("will not run");
        onboard.Materialization.Trigger.ShouldContain("Enable it via the planning.materializationDefault setting");
        onboard.Materialization.LastSuccess.ShouldBeNull();
        onboard.Materialization.LastAttempt.ShouldBeNull();
        onboard.Validation.HumanGate.ShouldBeFalse();
    }

    [Fact]
    public async Task Onboard_MaterializationBlock_Inherit_ResolvesGlobalDefaultOn()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var diskPath = TestPaths.DiskPath(name);
        await CreateProjectAsync(name, diskPath);
        await SetMaterializationSyncAsync("off");

        var onboard = await OnboardAsync(new { Cwd = diskPath });

        onboard.Materialization.Strategy.ShouldBe("on");
        onboard.Materialization.Trigger.ShouldContain("enqueued after every planning mutation, runs shortly after in the background");
        onboard.Validation.HumanGate.ShouldBeFalse();
    }

    [Fact]
    public async Task Onboard_MaterializationBlock_SyncFlagOn_TriggerSaysInline()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        var diskPath = TestPaths.DiskPath(name);
        await CreateProjectAsync(name, diskPath);
        await SetMaterializationSyncAsync("on");

        var onboard = await OnboardAsync(new { Cwd = diskPath });

        onboard.Materialization.Strategy.ShouldBe("on");
        onboard.Materialization.Trigger.ShouldContain("runs the mirror pass inline before returning");
        onboard.Materialization.Trigger.ShouldContain("planning.materializationSync=on");
        onboard.Validation.HumanGate.ShouldBeFalse();
    }

    [Fact]
    public async Task Onboard_MaterializationSplit_NullBeforePass_SetAfterMutationTriggeredPass()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), $"overmind-onboard-mat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sandbox);
        try
        {
            var name = $"mat-{Guid.NewGuid():N}";
            var project = await CreateProjectAsync(name, sandbox);
            await SetWorkflowAsync(project.Id, materialization: "on");

            var before = await OnboardAsync(new { Cwd = sandbox });
            before.Materialization.LastSuccess.ShouldBeNull();
            before.Materialization.LastAttempt.ShouldBeNull();

            var create = await _client.PostAsJsonAsync(
                $"/api/projects/{project.Id}/planning/features",
                new { Slug = "feat", Title = "Feat", Body = string.Empty });
            create.StatusCode.ShouldBe(HttpStatusCode.Created);

            File.Exists(Path.Combine(sandbox, "BACKLOG", "feat", "feature.md")).ShouldBeTrue();

            var after = await OnboardAsync(new { Cwd = sandbox });
            after.Materialization.LastSuccess.ShouldNotBeNull();
            after.Materialization.LastAttempt.ShouldNotBeNull();
            after.Materialization.Strategy.ShouldBe("on");
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    [Fact]
    public async Task Onboard_MaterializationSplit_FailedAttemptAfterSuccess_KeepsLastSuccessAndAdvancesAttempt()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), $"overmind-onboard-split-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sandbox);
        try
        {
            var name = $"split-{Guid.NewGuid():N}";
            var project = await CreateProjectAsync(name, sandbox);
            await SetWorkflowAsync(project.Id, materialization: "on");
            await CreateFeatureAsync(project.Id, "feat", "Feat");

            var clean = await OnboardAsync(new { Cwd = sandbox });
            clean.Materialization.LastSuccess.ShouldNotBeNull();
            clean.Materialization.LastAttempt.ShouldNotBeNull();
            clean.Materialization.Error.ShouldBeNull();

            var featureDoc = Path.Combine(sandbox, "BACKLOG", "feat", "feature.md");
            await File.AppendAllTextAsync(featureDoc, "\nhand edit\n");
            await CreateFeatureAsync(project.Id, "second", "Second");

            var refused = await OnboardAsync(new { Cwd = sandbox });
            refused.Materialization.LastSuccess.ShouldBe(clean.Materialization.LastSuccess);
            refused.Materialization.LastAttempt.ShouldNotBeNull();
            refused.Materialization.LastAttempt!.Value
                .ShouldBeGreaterThan(clean.Materialization.LastAttempt!.Value);
            refused.Materialization.Error.ShouldNotBeNull();
            refused.Materialization.FailedAt.ShouldNotBeNull();
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    [Fact]
    public async Task Onboard_MaterializationBlock_FailedSync_CarriesErrorAndClearsAfterRecovery()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), $"overmind-onboard-fail-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sandbox);
        try
        {
            var name = $"fail-{Guid.NewGuid():N}";
            var project = await CreateProjectAsync(name, sandbox);
            await SetWorkflowAsync(project.Id, materialization: "on");
            await CreateFeatureAsync(project.Id, "feat", "Feat");

            var featureDoc = Path.Combine(sandbox, "BACKLOG", "feat", "feature.md");
            var pristine = await File.ReadAllTextAsync(featureDoc);
            await File.AppendAllTextAsync(featureDoc, "\nhand edit\n");

            var squatter = Path.Combine(sandbox, "BACKLOG", "second", "feature.md");
            Directory.CreateDirectory(Path.GetDirectoryName(squatter)!);
            await File.WriteAllTextAsync(squatter, "foreign squatter");

            await CreateFeatureAsync(project.Id, "second", "Second");

            var failed = await OnboardAsync(new { Cwd = sandbox });
            failed.Materialization.Error.ShouldNotBeNull();
            failed.Materialization.Error.ShouldContain("nothing was written");
            failed.Materialization.FailedAt.ShouldNotBeNull();
            failed.Materialization.LastAttempt.ShouldNotBeNull();
            failed.Materialization.LastSuccess.ShouldNotBeNull();

            await File.WriteAllTextAsync(featureDoc, pristine);
            File.Delete(squatter);

            await CreateFeatureAsync(project.Id, "third", "Third");

            var recovered = await OnboardAsync(new { Cwd = sandbox });
            recovered.Materialization.Error.ShouldBeNull();
            recovered.Materialization.FailedAt.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    [Fact]
    public async Task Onboard_MaterializationBlock_SeenFailure_DemotesToOneLinerUntilNewFailure()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), $"overmind-onboard-demote-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sandbox);
        try
        {
            var name = $"demote-{Guid.NewGuid():N}";
            var project = await CreateProjectAsync(name, sandbox);
            await SetWorkflowAsync(project.Id, materialization: "on");
            await CreateFeatureAsync(project.Id, "feat", "Feat");

            var featureDoc = Path.Combine(sandbox, "BACKLOG", "feat", "feature.md");
            var pristine = await File.ReadAllTextAsync(featureDoc);
            await File.AppendAllTextAsync(featureDoc, "\nhand edit\n");

            await CreateFeatureAsync(project.Id, "second", "Second");

            var first = await OnboardAsync(new { Cwd = sandbox });
            var firstError = first.Materialization.Error;
            firstError.ShouldNotBeNull();
            first.Materialization.FailedAt.ShouldNotBeNull();
            first.Materialization.DemotedNotice.ShouldBeNull();

            var second = await OnboardAsync(new { Cwd = sandbox });
            second.Materialization.Error.ShouldBeNull();
            second.Materialization.DemotedNotice.ShouldNotBeNull();
            second.Materialization.DemotedNotice.ShouldContain("already reported");
            second.Materialization.DemotedNotice.ShouldContain("planning_materialize_status");
            second.Materialization.FailedAt.ShouldBe(first.Materialization.FailedAt);

            await CreateFeatureAsync(project.Id, "third", "Third");

            var third = await OnboardAsync(new { Cwd = sandbox });
            third.Materialization.Error.ShouldNotBeNull();
            third.Materialization.DemotedNotice.ShouldBeNull();

            await File.WriteAllTextAsync(featureDoc, pristine);
            await CreateFeatureAsync(project.Id, "fourth", "Fourth");

            var clean = await OnboardAsync(new { Cwd = sandbox });
            clean.Materialization.Error.ShouldBeNull();
            clean.Materialization.DemotedNotice.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    [Fact]
    public async Task Onboard_MaterializationBlock_RefusedSync_CarriesErrorAndClearsOnCleanPass()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), $"overmind-onboard-skip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sandbox);
        try
        {
            var name = $"skip-{Guid.NewGuid():N}";
            var project = await CreateProjectAsync(name, sandbox);
            await SetWorkflowAsync(project.Id, materialization: "on");
            await CreateFeatureAsync(project.Id, "feat", "Feat");

            var featureDoc = Path.Combine(sandbox, "BACKLOG", "feat", "feature.md");
            var pristine = await File.ReadAllTextAsync(featureDoc);
            await File.AppendAllTextAsync(featureDoc, "\nhand edit\n");

            await CreateFeatureAsync(project.Id, "second", "Second");

            var refused = await OnboardAsync(new { Cwd = sandbox });
            refused.Materialization.LastAttempt.ShouldNotBeNull();
            refused.Materialization.LastSuccess.ShouldNotBeNull();
            refused.Materialization.Error.ShouldNotBeNull();
            refused.Materialization.Error!.ShouldContain("local edits");
            refused.Materialization.FailedAt.ShouldNotBeNull();
            File.Exists(Path.Combine(sandbox, "BACKLOG", "second", "feature.md")).ShouldBeFalse();

            await File.WriteAllTextAsync(featureDoc, pristine);

            await CreateFeatureAsync(project.Id, "third", "Third");

            var clean = await OnboardAsync(new { Cwd = sandbox });
            clean.Materialization.Error.ShouldBeNull();
            clean.Materialization.FailedAt.ShouldBeNull();
            clean.Materialization.SkippedCount.ShouldBe(1);
            clean.Materialization.Skipped.ShouldNotBeNull();
            clean.Materialization.Skipped!.ShouldHaveSingleItem().ShouldBe(
                "BACKLOG/feat/feature.md: content already matches the board");
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    [Fact]
    public async Task Onboard_EmptyCwdWithoutProjectId_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/onboard", new { Cwd = string.Empty });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Onboard_AdapterVersionMatchesHost_ReportsVersionsWithoutWarning()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) }, HostInformationalVersion());

        onboard.Adapter.ShouldNotBeNull();
        onboard.Adapter.AdapterVersion.ShouldBe(HostInformationalVersion());
        onboard.Adapter.HostVersion.ShouldBe(HostInformationalVersion());
        onboard.Adapter.Warning.ShouldBeNull();
    }

    [Fact]
    public async Task Onboard_AdapterVersionDiffers_WarnsRestartPinned()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) }, "0.0.0+stale-adapter");

        onboard.Adapter.ShouldNotBeNull();
        onboard.Adapter.AdapterVersion.ShouldBe("0.0.0+stale-adapter");
        onboard.Adapter.HostVersion.ShouldBe(HostInformationalVersion());
        onboard.Adapter.Warning.ShouldBe("tools may be missing from your toolset; restart your MCP session");
    }

    [Fact]
    public async Task Onboard_AdapterVersionHeaderMissing_WarnsPredatesPinned()
    {
        var name = $"onboard-{Guid.NewGuid():N}";
        await CreateProjectAsync(name, TestPaths.DiskPath(name));

        var onboard = await OnboardAsync(new { Cwd = TestPaths.DiskPath(name) });

        onboard.Adapter.ShouldNotBeNull();
        onboard.Adapter.AdapterVersion.ShouldBeNull();
        onboard.Adapter.HostVersion.ShouldBe(HostInformationalVersion());
        onboard.Adapter.Warning.ShouldBe("adapter predates version reporting; restart recommended");
    }

    private static string HostInformationalVersion() =>
        typeof(OnboardApi).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    private async Task<OnboardJson> OnboardAsync(object request, string? adapterVersion = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/onboard")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        if (adapterVersion is not null)
        {
            message.Headers.Add(AdapterVersionHeader.Name, adapterVersion);
        }

        var response = await _client.SendAsync(message);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var onboard = await response.Content.ReadFromJsonAsync<OnboardJson>(JsonOptions);
        onboard.ShouldNotBeNull();
        return onboard!;
    }

    private async Task<ProjectJson> CreateProjectAsync(string name, string diskPath)
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = diskPath,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task CreateFeatureAsync(Guid projectId, string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = string.Empty });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task UpdateFeatureStatusAsync(Guid projectId, string slug, string status)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}",
            new { Status = status });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task SetWorkflowAsync(Guid projectId, string? materialization = null, string? validation = null)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/workflow",
            new { Materialization = materialization, RequireHumanValidation = validation });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task SetMaterializationSyncAsync(string value)
    {
        var response = await _client.PutAsJsonAsync("/api/settings", new { PlanningMaterializationSync = value });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record OnboardJson(
        Guid ProjectId, string ProjectName, bool Created, OnboardBoardJson Board,
        OnboardMaterializationJson Materialization, OnboardValidationJson Validation,
        OnboardAdapterJson Adapter, OnboardRescueJson Rescue, string Quickstart);

    public sealed record OnboardAdapterJson(string? AdapterVersion, string HostVersion, string? Warning);

    public sealed record OnboardRescueJson(string? PolicyText);

    public sealed record OnboardBoardJson(Dictionary<string, int> CountsByStatus);

    public sealed record OnboardMaterializationJson(
        string Strategy, string TargetDir, string Trigger, DateTime? LastSuccess, DateTime? LastAttempt,
        string? Error, DateTime? FailedAt, int? SkippedCount, IReadOnlyList<string>? Skipped,
        string? DemotedNotice = null);

    public sealed record OnboardValidationJson(bool HumanGate, string Guidance);

    public sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
