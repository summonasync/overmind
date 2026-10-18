using System.Net;
using Overmind.Host;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PlanningCliTests
{
    private const string ProjectId = "ff313f29-8144-488c-94e6-5ea5dde57bfd";
    private const string SessionId = "6b8a2f1e-1c4d-4e5f-9a0b-1234567890ab";
    private const string ArtifactId = "8cd89e86-b88c-4e1f-b38d-f40007f493de";

    [Fact]
    public async Task Projects_List_RendersRowsAndNeverRegistersASession()
    {
        var stub = new StubOvermindApi().On("GET", "/api/projects/",
            """
            [{"id":"ff313f29-8144-488c-94e6-5ea5dde57bfd","name":"Overmind","diskPath":"/repo",
              "openFeatureCount":3}]
            """);
        var (output, error, _, _) = await RunAsync(["planning", "projects", "list"], stub);

        error.ToString().ShouldBeEmpty();
        output.ToString().ShouldContain("- Overmind [ff313f29-8144-488c-94e6-5ea5dde57bfd] · open features 3");
        stub.Requests.ShouldNotContain(r => r.Path == "/api/agent/sessions");
    }

    [Fact]
    public async Task Queue_WithJsonFlag_PassesHostBodyThroughUnchanged()
    {
        var payload = """{"items":[{"slug":"133-cli","priority":"p1","status":"inProgress","title":"CLI","taskDone":0,"taskTotal":1,"specStatus":"validated","planStatus":"validated","phase":"in progress"}]}""";
        var stub = new StubOvermindApi().On(
            "GET", $"/api/agent/projects/{ProjectId}/planning/task-queue", payload);
        var (output, error, code, _) = await RunAsync(
            ["planning", "queue", "--project", ProjectId, "--json"], stub);

        code.ShouldBe(0);
        error.ToString().ShouldBeEmpty();
        output.ToString().ShouldBe(payload + Environment.NewLine);
    }

    [Fact]
    public async Task Queue_WithLimit_SendsLimitQuery()
    {
        var stub = new StubOvermindApi().On(
            "GET", $"/api/agent/projects/{ProjectId}/planning/task-queue", """{"items":[]}""");
        var (output, error, code, _) = await RunAsync(
            ["planning", "queue", "--project", ProjectId, "--limit", "5"], stub);

        code.ShouldBe(0);
        output.ToString().ShouldBe("queue is empty\n");
        stub.Requests.Single().Query.ShouldBe("limit=5");
    }

    [Fact]
    public async Task Queue_WithoutProject_ReturnsUsageErrorWithExitCode2()
    {
        var stub = new StubOvermindApi();
        var (output, error, code, _) = await RunAsync(["planning", "queue"], stub);

        code.ShouldBe(2);
        output.ToString().ShouldBeEmpty();
        error.ToString().ShouldContain("--project");
    }

    [Fact]
    public async Task FeatureRead_RendersBodySpecTasksAndResult()
    {
        var stub = new StubOvermindApi().On(
            "GET", $"/api/agent/projects/{ProjectId}/planning/features/133-cli",
            $$"""
            {
              "slug": "133-cli",
              "title": "PlanningCli",
              "body": "CLI door.",
              "status": "inProgress",
              "artifacts": [
                {"kind":"spec","status":"validated","order":0,"body":"spec body"},
                {"kind":"task","status":"inProgress","order":0,"review":"hard","body":"# T0\n\nDo it."},
                {"kind":"result","status":"draft","order":0,"body":"result body"}
              ]
            }
            """);
        var (output, error, code, _) = await RunAsync(
            ["planning", "feature", "read", "133-cli", "--project", ProjectId], stub);

        code.ShouldBe(0);
        error.ToString().ShouldBeEmpty();
        var text = output.ToString();
        text.ShouldContain("# PlanningCli");
        text.ShouldContain("## Spec\n\nspec body");
        text.ShouldContain("### 00. T0");
        text.ShouldContain("Status: inProgress · Review: hard");
        text.ShouldContain("## Result\n\nresult body");
    }

    [Fact]
    public async Task Current_StopState_LeadsWithTheAuthoritativeStopLine()
    {
        var stub = new StubOvermindApi().On(
            "GET", $"/api/agent/projects/{ProjectId}/planning/features/133-cli/current",
            """{"state":"stop","reason":"STOP: spec is not validated — ask the user."}""");
        var (output, _, code, _) = await RunAsync(
            ["planning", "current", "133-cli", "--project", ProjectId], stub);

        code.ShouldBe(0);
        output.ToString().ShouldStartWith("STOP:");
    }

    [Fact]
    public async Task Current_HeldState_LeadsWithHeld()
    {
        var stub = new StubOvermindApi().On(
            "GET", $"/api/agent/projects/{ProjectId}/planning/features/133-cli/current",
            """{"state":"held","reason":"every remaining task sits in review"}""");
        var (output, _, _, _) = await RunAsync(
            ["planning", "current", "133-cli", "--project", ProjectId], stub);

        output.ToString().ShouldBe("HELD: every remaining task sits in review\n");
    }

    [Fact]
    public async Task ArtifactRead_ByAlias_ResolvesRosterThenReadsWithSections()
    {
        var stub = new StubOvermindApi()
            .On(
                "GET",
                $"/api/agent/projects/{ProjectId}/planning/features/133-cli/artifacts",
                $$"""{"artifacts":[{"kind":"task","order":0,"artifactId":"{{ArtifactId}}","status":"inProgress"}]}""")
            .On(
                "GET",
                $"/api/agent/planning/artifacts/{ArtifactId}",
                $$"""
                {"id":"{{ArtifactId}}","kind":"task","status":"inProgress","version":1,"order":0,
                 "brief":"PlanningCli: run-and-exit","body":"<goal>\nCLI door.\n</goal>"}
                """);
        var (output, error, code, _) = await RunAsync(
            ["planning", "artifact", "read", "133-cli/T0", "--sections", "goal", "--project", ProjectId], stub);

        code.ShouldBe(0);
        error.ToString().ShouldBeEmpty();
        output.ToString().ShouldContain("task #00 [inProgress] v1");
        output.ToString().ShouldContain("CLI door.");
        stub.Requests.Last().Query.ShouldBe("sections=goal");
    }

    [Fact]
    public async Task ArtifactRead_WithMalformedAlias_ReturnsUsageError()
    {
        var stub = new StubOvermindApi();
        var (_, error, code, _) = await RunAsync(
            ["planning", "artifact", "read", "133-cli/X9", "--project", ProjectId], stub);

        code.ShouldBe(2);
        error.ToString().ShouldContain("neither an artifact GUID nor an alias");
    }

    [Fact]
    public async Task Claim_AutoRegistersSessionAndStampsHeaderOnClaim()
    {
        var stub = new StubOvermindApi()
            .On("POST", "/api/agent/sessions", $$"""{"id":"{{SessionId}}","alias":"agent","vendor":"unknown"}""", HttpStatusCode.Created)
            .On(
                "POST",
                $"/api/agent/projects/{ProjectId}/planning/features/133-cli/claim",
                $$"""{"slug":"133-cli","claimedByAlias":"agent","claimedByVendor":"unknown","claimState":"alive"}""");
        var (_, error, code, _) = await RunAsync(
            ["planning", "claim", "133-cli", "--project", ProjectId], stub);

        code.ShouldBe(0);
        error.ToString().ShouldBeEmpty();
        stub.Requests.Count.ShouldBe(2);
        stub.Requests[0].Method.ShouldBe("POST");
        stub.Requests[0].Path.ShouldBe("/api/agent/sessions");
        var registration = stub.Requests[0].Body ?? string.Empty;
        registration.ShouldContain("\"alias\":\"agent\"");
        registration.ShouldContain("\"vendor\":\"unknown\"");
        stub.Requests[1].SessionHeader.ShouldBe(SessionId);
    }

    [Fact]
    public async Task Claim_WithEnvironmentSession_ReusesHeaderWithoutRegistration()
    {
        var stub = new StubOvermindApi().On(
            "POST",
            $"/api/agent/projects/{ProjectId}/planning/features/133-cli/claim",
            """{"slug":"133-cli","claimedByAlias":"billy","claimedByVendor":"claude","claimState":"alive"}""");
        var options = new PlanningCliOptions(SessionId, "billy", "claude");
        var (_, error, code, _) = await RunAsync(
            ["planning", "claim", "133-cli", "--project", ProjectId, "--override-lane-check"],
            stub,
            options);

        code.ShouldBe(0);
        error.ToString().ShouldBeEmpty();
        stub.Requests.ShouldContain(r => r.Path != "/api/agent/sessions");
        stub.Requests.Single().SessionHeader.ShouldBe(SessionId);
        var body = stub.Requests.Single().Body ?? string.Empty;
        body.ShouldContain("\"alias\":\"billy\"");
        body.ShouldContain("\"overrideLaneCheck\":true");
    }

    [Fact]
    public async Task Unclaim_SendsSessionHeaderAndRendersUnclaimed()
    {
        var stub = new StubOvermindApi().On(
            "POST",
            $"/api/agent/projects/{ProjectId}/planning/features/133-cli/unclaim",
            """{"slug":"133-cli","claimedByAlias":null}""");
        var options = new PlanningCliOptions(SessionId, "billy", "claude");
        var (output, _, code, _) = await RunAsync(
            ["planning", "unclaim", "133-cli", "--project", ProjectId], stub, options);

        code.ShouldBe(0);
        stub.Requests.Single().SessionHeader.ShouldBe(SessionId);
        output.ToString().ShouldContain("133-cli · unclaimed");
    }

    [Fact]
    public async Task ArtifactUpsert_ReadsBodyFileAndPostsPayload()
    {
        var bodyFile = Path.Combine(Path.GetTempPath(), $"planning-cli-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(bodyFile, "<goal>\nCLI door.\n</goal>");
        try
        {
            var stub = new StubOvermindApi().On(
                "POST",
                $"/api/agent/projects/{ProjectId}/planning/features/133-cli/artifacts",
                $$"""{"id":"{{ArtifactId}}","status":"draft","version":1,"lastLogLine":"created task #0"}""");
            var options = new PlanningCliOptions(SessionId, "billy", "claude");
            var (output, _, code, _) = await RunAsync(
                [
                    "planning", "artifact", "upsert", "133-cli", "--kind", "task", "--order", "0",
                    "--review", "hard", "--body-file", bodyFile, "--log-note", "created", "--project", ProjectId,
                ],
                stub,
                options);

            code.ShouldBe(0);
            var body = stub.Requests.Last().Body ?? string.Empty;
            body.ShouldContain("\"kind\":\"task\"");
            body.ShouldContain("\"body\":\"<goal>\\nCLI door.\\n</goal>\"");
            body.ShouldContain("\"order\":0");
            body.ShouldContain("\"review\":\"hard\"");
            body.ShouldContain("\"logNote\":\"created\"");
            output.ToString().ShouldContain("[draft] v1");
            output.ToString().ShouldContain("created task #0");
        }
        finally
        {
            File.Delete(bodyFile);
        }
    }

    [Fact]
    public async Task ArtifactUpsert_WithMissingBodyFile_ReturnsUsageError()
    {
        var stub = new StubOvermindApi();
        var (_, error, code, _) = await RunAsync(
            [
                "planning", "artifact", "upsert", "133-cli", "--kind", "task",
                "--body-file", "/no/such/file.md", "--project", ProjectId,
            ],
            stub);

        code.ShouldBe(2);
        error.ToString().ShouldContain("--body-file");
    }

    [Fact]
    public async Task ArtifactPatch_ByAcTextOnly_UnticksWithoutAcIndex()
    {
        var stub = new StubOvermindApi().On(
            "POST",
            $"/api/agent/planning/artifacts/{ArtifactId}/patch",
            $$"""
            {"id":"{{ArtifactId}}","kind":"task","section":"acceptance-criteria","status":"review","version":8,
             "tickedIndex":1,"tickedText":"Round done"}
            """);
        var options = new PlanningCliOptions(SessionId, "billy", "claude");
        var (output, _, code, _) = await RunAsync(
            ["planning", "artifact", "patch", ArtifactId, "--ac-text", "Round done", "--untick", "--project", ProjectId],
            stub,
            options);

        code.ShouldBe(0);
        var body = stub.Requests.Single().Body ?? string.Empty;
        body.ShouldContain("\"op\":\"untick_ac\"");
        body.ShouldContain("\"acText\":\"Round done\"");
        body.ShouldNotContain("acIndex");
        output.ToString().ShouldContain("acceptance item 1: Round done");
    }

    [Fact]
    public async Task ArtifactPatch_ByAcIndex_SendsParsedIndex()
    {
        var stub = new StubOvermindApi().On(
            "POST",
            $"/api/agent/planning/artifacts/{ArtifactId}/patch",
            $$"""{"id":"{{ArtifactId}}","kind":"task","section":"acceptance-criteria","status":"review","version":8,"tickedIndex":3}""");
        var options = new PlanningCliOptions(SessionId, "billy", "claude");
        var (_, _, code, _) = await RunAsync(
            ["planning", "artifact", "patch", ArtifactId, "--ac-index", "3", "--project", ProjectId],
            stub,
            options);

        code.ShouldBe(0);
        var body = stub.Requests.Single().Body ?? string.Empty;
        body.ShouldContain("\"op\":\"tick_ac\"");
        body.ShouldContain("\"acIndex\":3");
        body.ShouldNotContain("acText");
    }

    [Fact]
    public async Task ArtifactPatch_WithBothAcSelectors_ReturnsUsageError()
    {
        var stub = new StubOvermindApi();
        var (_, error, code, _) = await RunAsync(
            ["planning", "artifact", "patch", ArtifactId, "--ac-index", "2", "--ac-text", "Round done", "--project", ProjectId],
            stub);

        code.ShouldBe(2);
        stub.Requests.ShouldBeEmpty();
        error.ToString().ShouldContain("by acIndex or by exact acText, not both");
    }

    [Fact]
    public async Task ArtifactPatch_WithUnparsableAcIndex_ReturnsUsageError()
    {
        var stub = new StubOvermindApi();
        var (_, error, code, _) = await RunAsync(
            ["planning", "artifact", "patch", ArtifactId, "--ac-index", "two", "--project", ProjectId],
            stub);

        code.ShouldBe(2);
        error.ToString().ShouldContain("--ac-index expects an integer");
    }

    [Fact]
    public async Task ArtifactPatch_SectionWithContent_ReplacesSection()
    {
        var stub = new StubOvermindApi().On(
            "POST",
            $"/api/agent/planning/artifacts/{ArtifactId}/patch",
            $$"""{"id":"{{ArtifactId}}","kind":"task","section":"goal","status":"review","version":8}""");
        var options = new PlanningCliOptions(SessionId, "billy", "claude");
        var (output, _, code, _) = await RunAsync(
            ["planning", "artifact", "patch", ArtifactId, "--section", "goal", "--content", "<goal>\nCLI door.\n</goal>", "--project", ProjectId],
            stub,
            options);

        code.ShouldBe(0);
        var body = stub.Requests.Single().Body ?? string.Empty;
        body.ShouldContain("\"op\":\"replace_section\"");
        body.ShouldContain("\"section\":\"goal\"");
        body.ShouldContain("\"content\":\"<goal>\\nCLI door.\\n</goal>\"");
        output.ToString().ShouldContain("task · goal [review] v8");
    }

    [Fact]
    public async Task ArtifactPatch_SectionWithContentFile_ReadsTheFile()
    {
        var sectionFile = Path.Combine(Path.GetTempPath(), $"planning-cli-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(sectionFile, "<goal>\nrewritten.\n</goal>");
        try
        {
            var stub = new StubOvermindApi().On(
                "POST",
                $"/api/agent/planning/artifacts/{ArtifactId}/patch",
                $$"""{"id":"{{ArtifactId}}","kind":"task","section":"goal","status":"review","version":8}""");
            var options = new PlanningCliOptions(SessionId, "billy", "claude");
            var (_, _, code, _) = await RunAsync(
                ["planning", "artifact", "patch", ArtifactId, "--section", "goal", "--content-file", sectionFile, "--project", ProjectId],
                stub,
                options);

            code.ShouldBe(0);
            (stub.Requests.Single().Body ?? string.Empty)
                .ShouldContain("\"content\":\"<goal>\\nrewritten.\\n</goal>\"");
        }
        finally
        {
            File.Delete(sectionFile);
        }
    }

    [Fact]
    public async Task ArtifactPatch_ByBrief_SetsBrief()
    {
        var stub = new StubOvermindApi().On(
            "POST",
            $"/api/agent/planning/artifacts/{ArtifactId}/patch",
            $$"""{"id":"{{ArtifactId}}","kind":"task","status":"review","version":9}""");
        var options = new PlanningCliOptions(SessionId, "billy", "claude");
        var (_, _, code, _) = await RunAsync(
            ["planning", "artifact", "patch", ArtifactId, "--brief", "one-line summary", "--project", ProjectId],
            stub,
            options);

        code.ShouldBe(0);
        var body = stub.Requests.Single().Body ?? string.Empty;
        body.ShouldContain("\"op\":\"set_brief\"");
        body.ShouldContain("\"brief\":\"one-line summary\"");
    }

    [Fact]
    public async Task ArtifactPatch_WithContentButNoSection_ReturnsUsageError()
    {
        var stub = new StubOvermindApi();
        var (_, error, code, _) = await RunAsync(
            ["planning", "artifact", "patch", ArtifactId, "--content", "orphan text", "--project", ProjectId],
            stub);

        code.ShouldBe(2);
        stub.Requests.ShouldBeEmpty();
        error.ToString().ShouldContain("exactly one mode");
    }

    [Fact]
    public async Task ArtifactPatch_SectionWithoutContentSource_ReturnsUsageError()
    {
        var stub = new StubOvermindApi();
        var (_, error, code, _) = await RunAsync(
            ["planning", "artifact", "patch", ArtifactId, "--section", "goal", "--project", ProjectId],
            stub);

        code.ShouldBe(2);
        error.ToString().ShouldContain("exactly one content source");
    }

    [Fact]
    public async Task ArtifactPatch_WithBothContentSources_ReturnsUsageError()
    {
        var stub = new StubOvermindApi();
        var (_, error, code, _) = await RunAsync(
            [
                "planning", "artifact", "patch", ArtifactId, "--section", "goal",
                "--content", "inline", "--content-file", "/tmp/section.md", "--project", ProjectId,
            ],
            stub);

        code.ShouldBe(2);
        error.ToString().ShouldContain("exactly one content source");
    }

    [Fact]
    public async Task HostUnreachable_ExplainStartTheHostAndExitOne()
    {
        var failing = new FailingHandler(new HttpRequestException("connection refused"));
        using var http = new HttpClient(failing) { BaseAddress = new Uri("http://127.0.0.1:4761") };
        var client = new PlanningCliClient(http, "http://127.0.0.1:4761");
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await PlanningCli.ExecuteAsync(
            ["planning", "queue", "--project", ProjectId],
            client,
            new PlanningCliOptions(null, "agent", "unknown"),
            output,
            error,
            CancellationToken.None);

        code.ShouldBe(1);
        output.ToString().ShouldBeEmpty();
        error.ToString().ShouldContain("start the host");
    }

    [Fact]
    public async Task ApiError_SurfacesProblemDetailAndReturnsExitOne()
    {
        var stub = new StubOvermindApi().On(
            "POST",
            $"/api/agent/projects/{ProjectId}/planning/features/133-cli/claim",
            """{"detail":"STOP: the plan is not validated — approve it in the Overmind UI."}""",
            HttpStatusCode.Conflict);
        var options = new PlanningCliOptions(SessionId, "billy", "claude");
        var (output, error, code, _) = await RunAsync(
            ["planning", "claim", "133-cli", "--project", ProjectId], stub, options);

        code.ShouldBe(1);
        output.ToString().ShouldBeEmpty();
        error.ToString().ShouldStartWith("STOP:");
    }

    [Fact]
    public async Task UnexpectedFailure_ReportsOnStderrAndExitsOne()
    {
        var failing = new FailingHandler(new InvalidOperationException("collector exploded"));
        using var http = new HttpClient(failing) { BaseAddress = new Uri("http://127.0.0.1:4761") };
        var client = new PlanningCliClient(http, "http://127.0.0.1:4761");
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await PlanningCli.ExecuteAsync(
            ["planning", "queue", "--project", ProjectId],
            client,
            new PlanningCliOptions(null, "agent", "unknown"),
            output,
            error,
            CancellationToken.None);

        code.ShouldBe(1);
        output.ToString().ShouldBeEmpty();
        error.ToString().ShouldContain("collector exploded");
    }

    [Fact]
    public async Task UnknownCommand_PrintsUsageAndReturnsExitTwo()
    {
        var stub = new StubOvermindApi();
        var (output, error, code, _) = await RunAsync(["planning", "frobnicate"], stub);

        code.ShouldBe(2);
        output.ToString().ShouldBeEmpty();
        error.ToString().ShouldContain("Unknown planning command 'frobnicate'");
        error.ToString().ShouldContain("usage: overmind planning");
    }

    [Fact]
    public async Task MissingVerb_PrintsUsageAndReturnsExitTwo()
    {
        var stub = new StubOvermindApi();
        var (_, error, code, _) = await RunAsync(["planning"], stub);

        code.ShouldBe(2);
        error.ToString().ShouldContain("No planning command given.");
    }

    [Fact]
    public async Task TaskStatus_ByAlias_MovesTaskAndReportsWarnings()
    {
        var stub = new StubOvermindApi()
            .On(
                "GET",
                $"/api/agent/projects/{ProjectId}/planning/features/133-cli/artifacts",
                $$"""{"artifacts":[{"kind":"task","order":0,"artifactId":"{{ArtifactId}}"}]}""")
            .On(
                "POST",
                $"/api/agent/planning/artifacts/{ArtifactId}/status",
                $$"""
                {"id":"{{ArtifactId}}","status":"review","version":2,
                 "lastLogLine":"2026-10-18 [agent] — review","warnings":["1 unticked acceptance item"]}
                """);
        var options = new PlanningCliOptions(SessionId, "billy", "claude");
        var (output, _, code, _) = await RunAsync(
            [
                "planning", "task", "status", "133-cli/T0", "--status", "review",
                "--log-note", "review", "--project", ProjectId,
            ],
            stub,
            options);

        code.ShouldBe(0);
        var request = stub.Requests.Single(r => r.Path!.EndsWith("/status"));
        request.SessionHeader.ShouldBe(SessionId);
        (request.Body ?? string.Empty).ShouldContain("\"status\":\"review\"");
        output.ToString().ShouldContain("[review] v2");
        output.ToString().ShouldContain("warning: 1 unticked acceptance item");
    }

    [Fact]
    public async Task DirectiveAck_PostsToDirectiveRoute()
    {
        var stub = new StubOvermindApi().On(
            "POST",
            $"/api/agent/projects/{ProjectId}/planning/features/133-cli/directives/{SessionId}/ack",
            $$"""{"id":"{{SessionId}}","ack":true,"ackByAlias":"billy","ackVendor":"claude"}""");
        var options = new PlanningCliOptions(SessionId, "billy", "claude");
        var (output, _, code, _) = await RunAsync(
            ["planning", "directive", "ack", "133-cli", SessionId, "--project", ProjectId], stub, options);

        code.ShouldBe(0);
        output.ToString().ShouldContain("acknowledged by billy/claude");
    }

    private static async Task<(string Output, string Error, int Code, StubOvermindApi Stub)> RunAsync(
        string[] args, StubOvermindApi stub, PlanningCliOptions? options = null)
    {
        using var http = new HttpClient(stub) { BaseAddress = new Uri("http://127.0.0.1:4761") };
        var client = new PlanningCliClient(http, "http://127.0.0.1:4761");
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await PlanningCli.ExecuteAsync(
            args,
            client,
            options ?? new PlanningCliOptions(null, "agent", "unknown"),
            output,
            error,
            CancellationToken.None);
        return (output.ToString(), error.ToString(), code, stub);
    }

    private sealed class FailingHandler(Exception failure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(failure);
    }
}
