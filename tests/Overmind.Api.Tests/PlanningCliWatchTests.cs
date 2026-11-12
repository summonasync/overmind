using System.Globalization;
using System.Net;
using Overmind.Host;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PlanningCliWatchTests
{
    private const string SessionId = "6b8a2f1e-1c4d-4e5f-9a0b-1234567890ab";
    private const string FreshAt = "2027-01-01T00:00:00.0000000+00:00";

    [Fact]
    public async Task Watch_RegistersSessionAndPollsWithBaselineSince()
    {
        var stub = new StubOvermindApi()
            .On("POST", "/api/agent/sessions", $$"""{"id":"{{SessionId}}","alias":"agent","vendor":"unknown"}""", HttpStatusCode.Created)
            .On(
                "GET",
                "/api/agent/wake",
                Page(SignalJson(PlanningCliWatchOptions.MentionKind, "van", "lane-a")));
        var (output, error, code, _) = await RunAsync(["planning", "watch"], stub);

        code.ShouldBe(0);
        error.ToString().ShouldBeEmpty();
        stub.Requests.Count.ShouldBe(2);
        stub.Requests[0].Method.ShouldBe("POST");
        stub.Requests[0].Path.ShouldBe("/api/agent/sessions");
        (stub.Requests[0].Body ?? string.Empty).ShouldContain("\"alias\":\"agent\"");
        stub.Requests[1].SessionHeader.ShouldBe(SessionId);
        var query = Uri.UnescapeDataString(stub.Requests[1].Query ?? string.Empty);
        query.ShouldStartWith("since=");
        var since = DateTimeOffset.Parse(query["since=".Length..], CultureInfo.InvariantCulture);
        (DateTimeOffset.UtcNow - since).ShouldBeGreaterThan(TimeSpan.Zero);
        (DateTimeOffset.UtcNow - since).ShouldBeLessThan(TimeSpan.FromMinutes(1));
        output.ToString().ShouldBe($"[wave-mention] from van in lane-a at {FreshAt}: wake up\n");
    }

    [Fact]
    public async Task Watch_WithAlias_RegistersUnderAliasAndFiltersKind()
    {
        var stub = new StubOvermindApi()
            .On("POST", "/api/agent/sessions", $$"""{"id":"{{SessionId}}","alias":"billy","vendor":"unknown"}""", HttpStatusCode.Created)
            .On(
                "GET",
                "/api/agent/wake",
                Page(
                    SignalJson(PlanningCliWatchOptions.MentionKind, "kerrigan", "all"),
                    SignalJson(PlanningCliWatchOptions.ReplyKind, "human", null)));
        var (output, error, code, _) = await RunAsync(
            ["planning", "watch", "--alias", "billy", "--replies"], stub);

        code.ShouldBe(0);
        error.ToString().ShouldBeEmpty();
        (stub.Requests[0].Body ?? string.Empty).ShouldContain("\"alias\":\"billy\"");
        output.ToString().ShouldBe($"[notification-reply] from human in - at {FreshAt}: wake up\n");
        output.ToString().ShouldNotContain("wave-mention");
    }

    [Fact]
    public async Task Watch_WithZeroTimeout_NamesThePositiveContract()
    {
        var stub = new StubOvermindApi()
            .On("GET", "/api/agent/wake", Page());
        var (output, error, code, _) = await RunAsync(
            ["planning", "watch", "--timeout", "0s"], stub);

        code.ShouldBe(2);
        output.ToString().ShouldBeEmpty();
        error.ToString().ShouldContain("--timeout expects a duration above zero");
        stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Watch_WithBothKindFlags_ReturnsUsageErrorWithoutHttpCalls()
    {
        var stub = new StubOvermindApi();
        var (output, error, code, _) = await RunAsync(
            ["planning", "watch", "--mentions", "--replies"], stub);

        code.ShouldBe(2);
        output.ToString().ShouldBeEmpty();
        error.ToString().ShouldContain("--mentions and --replies are exclusive");
        stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Watch_WithUnparsableTimeout_ReturnsUsageErrorWithoutHttpCalls()
    {
        var stub = new StubOvermindApi();
        var (_, error, code, _) = await RunAsync(
            ["planning", "watch", "--timeout", "abc"], stub);

        code.ShouldBe(2);
        error.ToString().ShouldContain("--timeout expects a duration above zero");
        stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Watch_UnknownCommand_StillReportsUsage()
    {
        var stub = new StubOvermindApi();
        var (_, error, code, _) = await RunAsync(["planning", "watchx"], stub);

        code.ShouldBe(2);
        error.ToString().ShouldContain("Unknown planning command 'watchx'");
    }

    private static async Task<(string Output, string Error, int Code, StubOvermindApi Stub)> RunAsync(
        string[] args, StubOvermindApi stub)
    {
        using var http = new HttpClient(stub) { BaseAddress = new Uri("http://127.0.0.1:4761") };
        var client = new PlanningCliClient(http, "http://127.0.0.1:4761");
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await PlanningCli.ExecuteAsync(
            args,
            client,
            new PlanningCliOptions(null, "agent", "unknown"),
            output,
            error,
            CancellationToken.None);
        return (output.ToString(), error.ToString(), code, stub);
    }

    private static string Page(params string[] signals) =>
        $$"""{"signals":[{{string.Join(",", signals)}}],"cursor":"{{FreshAt}}"}""";

    private static string SignalJson(string kind, string from, string? room) =>
        $$"""
        {"kind":"{{kind}}","refId":"{{Guid.NewGuid()}}","roomId":{{(room is null ? "null" : $"\"{room}\"")}},"from":"{{from}}","createdAt":"{{FreshAt}}","excerpt":"wake up"}
        """;
}
