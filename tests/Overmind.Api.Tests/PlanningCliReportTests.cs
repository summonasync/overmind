using System.Globalization;
using Overmind.Host;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PlanningCliReportTests
{
    private const string ProjectId = "ff313f29-8144-488c-94e6-5ea5dde57bfd";
    private const string EntriesPath = $"/api/projects/{ProjectId}/entries";

    [Fact]
    public async Task Report_WithoutDate_ReturnsUsageErrorAndCallsNothing()
    {
        var stub = new StubOvermindApi();
        var (_, error, code, _) = await RunAsync(["planning", "report", "--project", ProjectId], stub);

        code.ShouldBe(2);
        stub.Requests.ShouldBeEmpty();
        error.ToString().ShouldContain("--date");
    }

    [Fact]
    public async Task Report_WithBadDateFormat_ReturnsUsageError()
    {
        var stub = new StubOvermindApi();
        var (_, error, code, _) = await RunAsync(
            ["planning", "report", "--date", "12/11/2026", "--project", ProjectId], stub);

        code.ShouldBe(2);
        stub.Requests.ShouldBeEmpty();
        error.ToString().ShouldContain("--date expects yyyy-MM-dd");
    }

    [Fact]
    public async Task Report_WithoutProject_ReturnsUsageError()
    {
        var stub = new StubOvermindApi();
        var (_, error, code, _) = await RunAsync(["planning", "report", "--date", "2026-11-12"], stub);

        code.ShouldBe(2);
        error.ToString().ShouldContain("--project");
    }

    [Fact]
    public async Task Report_SendsLocalMidnightWindowAndDefaultTag()
    {
        var stub = new StubOvermindApi().On("GET", EntriesPath, "[]");
        var (_, error, code, _) = await RunAsync(
            ["planning", "report", "--date", "2026-11-12", "--project", ProjectId], stub);

        code.ShouldBe(0);
        error.ToString().ShouldBeEmpty();
        var query = stub.Requests.Single().Query.ShouldNotBeNull();
        query.ShouldContain($"since={Escape(UtcMidnight(new DateOnly(2026, 11, 12)))}");
        query.ShouldContain($"until={Escape(UtcMidnight(new DateOnly(2026, 11, 13)))}");
        query.ShouldContain("tag=daily-notes");
    }

    [Fact]
    public async Task Report_WithTagSendsTagQuery()
    {
        var stub = new StubOvermindApi().On("GET", EntriesPath, "[]");
        var (_, _, code, _) = await RunAsync(
            ["planning", "report", "--date", "2026-11-12", "--tag", "sprint-notes", "--project", ProjectId],
            stub);

        code.ShouldBe(0);
        stub.Requests.Single().Query.ShouldNotBeNull().ShouldContain("tag=sprint-notes");
    }

    [Fact]
    public async Task Report_WithJsonFlag_PassesHostBodyThroughUnchanged()
    {
        var payload = """
            [{"id":"8cd89e86-b88c-4e1f-b38d-f40007f493de","title":"One","content":"body","createdAt":"2026-11-12T10:00:00Z","authorAlias":"billy"}]
            """;
        var stub = new StubOvermindApi().On("GET", EntriesPath, payload);
        var (output, error, code, _) = await RunAsync(
            ["planning", "report", "--date", "2026-11-12", "--project", ProjectId, "--json"], stub);

        code.ShouldBe(0);
        error.ToString().ShouldBeEmpty();
        output.ToString().ShouldBe(payload + Environment.NewLine);
    }

    [Fact]
    public async Task Report_GroupsByAuthorAlphabeticallyWithUnattributedBucket()
    {
        var stub = new StubOvermindApi().On("GET", EntriesPath, """
            [
              {"title":"Zed late note","content":"from zed","createdAt":"2026-11-12T15:00:00Z","authorAlias":"zed"},
              {"title":"Billy wave sync","content":"synced lanes","createdAt":"2026-11-12T15:30:00Z","authorAlias":"billy"},
              {"title":"Orphan memory","content":"nobody signed","createdAt":"2026-11-12T11:00:00Z","authorAlias":null},
              {"title":"Billy morning notes","content":"first line\nsecond line","createdAt":"2026-11-12T09:00:00Z","authorAlias":"billy"}
            ]
            """);
        var (output, error, code, _) = await RunAsync(
            ["planning", "report", "--date", "2026-11-12", "--project", ProjectId], stub);

        code.ShouldBe(0);
        error.ToString().ShouldBeEmpty();
        var text = output.ToString();
        text.ShouldContain("# Report 2026-11-12");
        text.IndexOf("## billy", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("## unattributed", StringComparison.Ordinal));
        text.IndexOf("## unattributed", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("## zed", StringComparison.Ordinal));
        text.IndexOf("### Billy morning notes", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("### Billy wave sync", StringComparison.Ordinal));
        text.ShouldContain("first line\nsecond line\n");
        text.ShouldContain("### Orphan memory");
        text.ShouldContain("### Zed late note");
    }

    [Fact]
    public async Task Report_EmptyDay_RendersNoEntriesLineAndExitsZero()
    {
        var stub = new StubOvermindApi().On("GET", EntriesPath, "[]");
        var (output, error, code, _) = await RunAsync(
            ["planning", "report", "--date", "2026-11-12", "--project", ProjectId], stub);

        code.ShouldBe(0);
        error.ToString().ShouldBeEmpty();
        output.ToString().ShouldBe("No entries on 2026-11-12\n");
    }

    private static string UtcMidnight(DateOnly day) =>
        DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local)
            .ToUniversalTime()
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string value) => Uri.EscapeDataString(value);

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
}
