using System.Text.Json.Nodes;
using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class InstallCommandOnboardTests : IDisposable
{
    private const string CeremonyFixture =
        """
        # Alpha

        Process:
        - Pick work from BACKLOG.md.
        - Feature folders live under BACKLOG/<slug>/.
        - Tasks use tasks/03-slug naming.
        """;

    private readonly string _home = Path.Combine(Path.GetTempPath(), $"overmind-onboard-{Guid.NewGuid():N}");
    private readonly string _cwd = Path.Combine(Path.GetTempPath(), $"overmind-onboard-cwd-{Guid.NewGuid():N}");
    private readonly string _binary = Path.Combine(Path.GetTempPath(), $"overmind-onboard-bin-{Guid.NewGuid():N}", "overmind-mcp");

    public InstallCommandOnboardTests()
    {
        Directory.CreateDirectory(_home);
        Directory.CreateDirectory(_cwd);
    }

    public void Dispose()
    {
        Directory.Delete(_home, recursive: true);
        Directory.Delete(_cwd, recursive: true);
    }

    private AgentPathEnvironment Env()
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["PATH"] = _home };
        return new AgentPathEnvironment { HomeDirectory = _home, Variables = variables };
    }

    private async Task<(int Exit, string Out, string Err)> RunAsync(
        InstallOptions options,
        OnboardProjectResolver resolver,
        string? workingDirectory = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var command = new InstallCommand(Env(), workingDirectory ?? _cwd, _binary, output, error, resolver);
        var exit = await command.RunAsync(options, CancellationToken.None);
        return (exit, output.ToString(), error.ToString());
    }

    private static OnboardProjectResolver ResolverFor(StubOvermindApi stub) =>
        new(new OvermindApiClient(OvermindApiClient.CreateHttpClient(stub.BaseUrl)));

    private static OnboardProjectResolver UnreachableResolver() =>
        new(new OvermindApiClient(OvermindApiClient.CreateHttpClient("http://127.0.0.1:1")));

    private static string[] Lines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [Fact]
    public async Task OnboardNoAgentFile_DefaultSelection_CreatesFileWithManagedBlockOnly()
    {
        Directory.CreateDirectory(Path.Combine(_cwd, ".git"));
        Directory.CreateDirectory(Path.Combine(_home, ".claude"));
        await using var stub = StubOvermindApi.Start();
        stub.Route("POST", "/api/onboard", 200, Canned.OnboardJson);

        var (exit, output, error) = await RunAsync(new InstallOptions { Onboard = true }, ResolverFor(stub));

        exit.ShouldBe(0);
        error.ShouldBeEmpty();
        var agentFile = Path.Combine(_cwd, "CLAUDE.md");
        File.Exists(agentFile).ShouldBeTrue();

        var content = await File.ReadAllTextAsync(agentFile);
        content.ShouldStartWith(OnboardManagedBlock.StartMarker + "\n");
        content.ShouldEndWith(OnboardManagedBlock.EndMarker + "\n");
        content.ShouldContain("This repository is Overmind project 'alpha'");
        content.ShouldContain(Canned.ProjectId.ToString());
        content.ShouldContain("call `overmind_onboard` first");
        content.ShouldContain("This block overrides any file-based ceremony instructions in this file.");
        content.ShouldContain("OVERMIND_PROJECT_ID");

        var lines = Lines(output);
        lines.ShouldContain($"claude: onboard block created — {agentFile}");
        lines.ShouldContain(line => line.StartsWith("summary: 6 processed,", StringComparison.Ordinal) &&
                                    line.Contains("onboard: 1 created, 0 updated, 0 unchanged, 0 failed, 0 conflict warning(s)", StringComparison.Ordinal));

        var request = stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/onboard");
        var body = JsonNode.Parse(request.Body)!;
        body["cwd"]!.GetValue<string>().ShouldBe(_cwd);
        body["projectId"].ShouldBeNull();
    }

    [Fact]
    public async Task OnboardSecondRun_ReplacesOnlyBlock_HumanContentByteIdentical()
    {
        Directory.CreateDirectory(Path.Combine(_cwd, ".git"));
        var agentFile = Path.Combine(_cwd, "CLAUDE.md");
        var humanContent = "# Alpha\n\nHuman instructions stay.\n\n## Workflow\n\nKeep the changelog.\n";
        await File.WriteAllTextAsync(agentFile, humanContent);
        await using var stub = StubOvermindApi.Start();
        stub.Route("POST", "/api/onboard", 200, Canned.OnboardJson);
        var resolver = ResolverFor(stub);

        await RunAsync(new InstallOptions { Agents = ["claude"], Onboard = true }, resolver);
        var snapshot = await File.ReadAllTextAsync(agentFile);
        snapshot.ShouldContain(humanContent);

        var tampered = snapshot.Replace("call `overmind_onboard` first", "tampered instruction", StringComparison.Ordinal);
        tampered.ShouldNotBe(snapshot);
        await File.WriteAllTextAsync(agentFile, tampered);

        var (exit2, output2, _) = await RunAsync(new InstallOptions { Agents = ["claude"], Onboard = true }, resolver);
        exit2.ShouldBe(0);
        Lines(output2).ShouldContain($"claude: onboard block updated — {agentFile}");
        (await File.ReadAllTextAsync(agentFile)).ShouldBe(snapshot);

        var (exit3, output3, _) = await RunAsync(new InstallOptions { Agents = ["claude"], Onboard = true }, resolver);
        exit3.ShouldBe(0);
        Lines(output3).ShouldContain($"claude: onboard block unchanged — {agentFile}");
        (await File.ReadAllTextAsync(agentFile)).ShouldBe(snapshot);
    }

    [Fact]
    public async Task OnboardCeremonyFile_WarnsWithLines_ContentOutsideMarkersUntouched()
    {
        Directory.CreateDirectory(Path.Combine(_cwd, ".git"));
        var agentFile = Path.Combine(_cwd, "CLAUDE.md");
        var fixtureWithNewline = CeremonyFixture + "\n";
        await File.WriteAllTextAsync(agentFile, fixtureWithNewline);
        await using var stub = StubOvermindApi.Start();
        stub.Route("POST", "/api/onboard", 200, Canned.OnboardJson);

        var (exit, output, error) = await RunAsync(new InstallOptions { Agents = ["claude"], Onboard = true }, ResolverFor(stub));

        exit.ShouldBe(0);
        error.ShouldContain("warning: claude:");
        error.ShouldContain("prescribes file-based ceremony outside the managed block (left untouched):");
        error.ShouldContain("line 4: - Pick work from BACKLOG.md.");
        error.ShouldContain("line 5: - Feature folders live under BACKLOG/<slug>/.");
        error.ShouldContain("line 6: - Tasks use tasks/03-slug naming.");

        var content = await File.ReadAllTextAsync(agentFile);
        content.ShouldContain("<!-- overmind:begin -->");
        OnboardManagedBlock.Remove(content).ShouldBe(fixtureWithNewline);

        var lines = Lines(output);
        lines.ShouldContain($"claude: onboard block updated — {agentFile}");
        lines.ShouldContain(line => line.Contains("onboard: 0 created, 1 updated, 0 unchanged, 0 failed, 3 conflict warning(s)", StringComparison.Ordinal));

        var snapshot = await File.ReadAllTextAsync(agentFile);
        var (exit2, _, error2) = await RunAsync(new InstallOptions { Agents = ["claude"], Onboard = true }, ResolverFor(stub));
        exit2.ShouldBe(0);
        error2.ShouldContain("line 4: - Pick work from BACKLOG.md.");
        (await File.ReadAllTextAsync(agentFile)).ShouldBe(snapshot);
    }

    [Fact]
    public async Task OnboardDryRun_PrintsPlan_WritesNothing()
    {
        Directory.CreateDirectory(Path.Combine(_cwd, ".git"));
        await using var stub = StubOvermindApi.Start();
        stub.Route("POST", "/api/onboard", 200, Canned.OnboardJson);

        var (exit, output, error) = await RunAsync(
            new InstallOptions { Agents = ["claude"], Onboard = true, DryRun = true },
            ResolverFor(stub));

        exit.ShouldBe(0);
        error.ShouldBeEmpty();
        var agentFile = Path.Combine(_cwd, "CLAUDE.md");
        File.Exists(agentFile).ShouldBeFalse();
        Lines(output).ShouldContain($"claude: would create onboard block — {agentFile}");
        Lines(output).ShouldContain(
            "dry run: project id resolution is skipped — the onboard block preview uses placeholder text.");
        stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path == "/api/onboard");
    }

    [Fact]
    public async Task OnboardDryRun_ResolverThrowsIfCalled_ExitZeroNoError()
    {
        Directory.CreateDirectory(Path.Combine(_cwd, ".git"));
        var throwingResolver = new OnboardProjectResolver(
            new OvermindApiClient(new HttpClient(new ThrowingHandler())));

        var (exit, output, error) = await RunAsync(
            new InstallOptions { Agents = ["claude"], Onboard = true, DryRun = true },
            throwingResolver);

        exit.ShouldBe(0);
        error.ShouldBeEmpty();
        File.Exists(Path.Combine(_cwd, "CLAUDE.md")).ShouldBeFalse();
        Lines(output).ShouldContain(line => line.Contains("would create onboard block", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnboardHostDown_WarnsAndStillExitsZero_WithPlaceholderBlock()
    {
        Directory.CreateDirectory(Path.Combine(_cwd, ".git"));

        var (exit, output, error) = await RunAsync(
            new InstallOptions { Agents = ["claude"], Onboard = true },
            UnreachableResolver());

        exit.ShouldBe(0);
        error.ShouldContain("warning: overmind host is unreachable");

        var agentFile = Path.Combine(_cwd, "CLAUDE.md");
        var content = await File.ReadAllTextAsync(agentFile);
        content.ShouldContain("Overmind project 'unknown'");
        content.ShouldContain("(id: resolved on first overmind_onboard call)");
        content.ShouldContain("OVERMIND_PROJECT_ID");
        Lines(output).ShouldContain($"claude: onboard block created — {agentFile}");
    }

    [Fact]
    public async Task OnboardFromSubdirectory_ResolvesGitRoot_WritesAtRepoRoot()
    {
        Directory.CreateDirectory(Path.Combine(_cwd, ".git"));
        var subDirectory = Path.Combine(_cwd, "src", "deep");
        Directory.CreateDirectory(subDirectory);
        await using var stub = StubOvermindApi.Start();
        stub.Route("POST", "/api/onboard", 200, Canned.OnboardJson);

        var (exit, output, _) = await RunAsync(
            new InstallOptions { Agents = ["claude"], Onboard = true },
            ResolverFor(stub),
            workingDirectory: subDirectory);

        exit.ShouldBe(0);
        File.Exists(Path.Combine(_cwd, "CLAUDE.md")).ShouldBeTrue();
        Directory.GetFiles(subDirectory, "*.md", SearchOption.AllDirectories).ShouldBeEmpty();
        Lines(output).ShouldContain($"claude: onboard block created — {Path.Combine(_cwd, "CLAUDE.md")}");

        var request = stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/onboard");
        JsonNode.Parse(request.Body)!["cwd"]!.GetValue<string>().ShouldBe(_cwd);
    }

    [Fact]
    public async Task OnboardWithoutGitRoot_FallsBackToWorkingDirectory()
    {
        await using var stub = StubOvermindApi.Start();
        stub.Route("POST", "/api/onboard", 200, Canned.OnboardJson);

        var (exit, _, _) = await RunAsync(new InstallOptions { Agents = ["claude"], Onboard = true }, ResolverFor(stub));

        exit.ShouldBe(0);
        File.Exists(Path.Combine(_cwd, "CLAUDE.md")).ShouldBeTrue();
    }

    [Fact]
    public async Task OnboardAgentFileBlockedByDirectory_ReportsFailure_InstallStillExitsZero()
    {
        Directory.CreateDirectory(Path.Combine(_cwd, ".git"));
        Directory.CreateDirectory(Path.Combine(_cwd, "CLAUDE.md"));

        var (exit, output, _) = await RunAsync(
            new InstallOptions { Agents = ["claude"], Onboard = true },
            UnreachableResolver());

        exit.ShouldBe(0);
        var lines = Lines(output);
        lines.ShouldContain(line => line.StartsWith("claude: onboard failed — ", StringComparison.Ordinal));
        lines.ShouldContain(line => line.Contains("onboard: 0 created, 0 updated, 0 unchanged, 1 failed,", StringComparison.Ordinal));
        lines.ShouldContain(line => line.StartsWith("claude: added — ", StringComparison.Ordinal));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("onboard resolver must not be called in dry-run.");
    }
}
