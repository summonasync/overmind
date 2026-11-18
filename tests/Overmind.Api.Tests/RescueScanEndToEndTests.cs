using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Overmind.Core;
using Overmind.Mcp;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class RescueScanEndToEndTests : IAsyncLifetime, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _sandbox;
    private readonly string _repo;
    private readonly string _worktree;
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public RescueScanEndToEndTests()
    {
        _sandbox = Path.Combine(Path.GetTempPath(), $"overmind-rescue-e2e-{Guid.NewGuid():N}");
        _repo = Path.Combine(_sandbox, "repo");
        _worktree = Path.Combine(_sandbox, "lanes", "240-e2e-rescue");
        Directory.CreateDirectory(_repo);
        Directory.CreateDirectory(Path.Combine(_sandbox, "origin"));
        BootstrapRepository();

        _factory = new ApiFactory();
        _client = _factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
        _client.Dispose();
        _factory.Dispose();
        try
        {
            Directory.Delete(_sandbox, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    public void Dispose()
    {
    }

    [Fact]
    public async Task AbandonedWorktree_IsFoundAndTakenOverAndPushesCleanPerPolicy()
    {
        var project = await CreateProjectAsync(_repo);
        await CreateFeatureAsync(project.Id);
        var dead = await RegisterSessionAsync("mark-wolff", "claude");
        (await ClaimAsync(project.Id, dead, "mark-wolff", "claude")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await PinCodeAsync(project.Id, "feat/240-e2e-rescue", _worktree);

        File.WriteAllText(Path.Combine(_worktree, "feature.cs"), "// work the dead session committed\n");
        Git(_worktree, "add", "-A");
        Git(_worktree, "commit", "-q", "-m", "rescue work in progress");
        File.WriteAllText(Path.Combine(_worktree, "scratch-notes.txt"), "// half-finished edit nobody committed\n");
        await EndSessionAsync(dead);

        var found = await ScanAsync(project.Id);
        var row = Only(found);

        row["slug"]!.GetValue<string>().ShouldBe("240-e2e-rescue");
        row["claimState"]!.GetValue<string>().ShouldBe("ended");
        row["claimedByAlias"]!.GetValue<string>().ShouldBe("mark-wolff");
        row["claimedByVendor"]!.GetValue<string>().ShouldBe("claude");
        row["code"]!["source"]!.GetValue<string>().ShouldBe(RescueCodeLocation.SourcePin);
        row["code"]!["worktreeExists"]!.GetValue<bool>().ShouldBe(true);
        row["git"]!["dirtyFiles"]!.GetValue<int>().ShouldBe(1);
        row["git"]!["untrackedFiles"]!.GetValue<int>().ShouldBe(1);
        row["git"]!["unpushedCommits"]!.GetValue<int>().ShouldBe(1);
        row["git"]!["unpushedBase"]!.GetValue<string>().ShouldBe("origin/main");
        row["git"]!["unmerged"]!.GetValue<bool>().ShouldBe(true);
        row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.RescueWorthy);
        found["policy"]!["text"]!.GetValue<string>().ShouldBe(Project.DefaultRescuePolicy);
        found["policy"]!["autonomy"]!.GetValue<string>().ShouldBe(RescueScanSource.OwnerSet);

        var rescuer = await RegisterSessionAsync("Dan", "qwen");
        (await ClaimAsync(project.Id, rescuer, "Dan", "qwen")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await FeatureLogAsync(project.Id)).ShouldContain(
            "Dan by qwen took over ended claim of mark-wolff by claude");
        var info = await TakeoverInfoAsync();
        info.ShouldContain("Dan by qwen");
        info.ShouldContain("mark-wolff by claude");

        Git(_worktree, "add", "-A");
        Git(_worktree, "commit", "-q", "-m", "finish what the dead session started");
        Git(_worktree, "push", "-q", "-u", "origin", "feat/240-e2e-rescue");
        Git(_repo, "merge", "-q", "--no-ff", "feat/240-e2e-rescue", "-m", "rescue: merge the abandoned branch");
        Git(_repo, "push", "-q", "origin", "main");
        await EndSessionAsync(rescuer);

        var rescued = Only(await ScanAsync(project.Id));

        rescued["git"]!["dirtyFiles"]!.GetValue<int>().ShouldBe(0);
        rescued["git"]!["unpushedCommits"]!.GetValue<int>().ShouldBe(0);
        rescued["git"]!["unpushedBase"]!.GetValue<string>().ShouldBe("origin/feat/240-e2e-rescue");
        rescued["git"]!["unmerged"]!.GetValue<bool>().ShouldBe(false);
        rescued["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.Clean);

        Directory.Delete(_worktree, recursive: true);
        var swept = Only(await ScanAsync(project.Id));

        swept["code"]!["worktreeExists"]!.GetValue<bool>().ShouldBe(false);
        swept["code"]!["stale"]!.GetValue<bool>().ShouldBe(true);
        swept["code"]!["branchExists"]!.GetValue<bool>().ShouldBe(true);
        swept["git"]!["dirtyFiles"].ShouldBeNull();
        swept["git"]!["unpushedCommits"]!.GetValue<int>().ShouldBe(0);
        swept["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.Clean);
        swept["reasons"]!.AsArray()
            .Select(node => node!.GetValue<string>())
            .ShouldContain(reason => reason.Contains("worktree path", StringComparison.Ordinal) && reason.Contains("is gone", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ClearedPolicy_ReportsNoAutonomy_WhileTheFactsStayMeasured()
    {
        var project = await CreateProjectAsync(_repo);
        await CreateFeatureAsync(project.Id);
        var dead = await RegisterSessionAsync("mark-wolff", "claude");
        (await ClaimAsync(project.Id, dead, "mark-wolff", "claude")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await PinCodeAsync(project.Id, "feat/240-e2e-rescue", _worktree);
        await ClearRescuePolicyAsync(project.Id);

        File.WriteAllText(Path.Combine(_worktree, "feature.cs"), "// unpushed work\n");
        Git(_worktree, "add", "-A");
        Git(_worktree, "commit", "-q", "-m", "work nobody pushed");
        await EndSessionAsync(dead);

        var payload = await ScanAsync(project.Id);

        payload["policy"]!["text"].ShouldBeNull();
        payload["policy"]!["autonomy"]!.GetValue<string>().ShouldBe(RescueScanSource.NotSet);
        payload["policy"]!["guidance"]!.GetValue<string>().ShouldContain("ask the owner");
        var row = Only(payload);
        row["rescueAllowed"]!.GetValue<bool>().ShouldBe(false);
        row["git"]!["unpushedCommits"]!.GetValue<int>().ShouldBe(1);
        row["verdict"]!.GetValue<string>().ShouldBe(RescueVerdict.RescueWorthy);
    }

    private static JsonObject Only(JsonObject payload) =>
        payload["candidates"]!.AsArray().Single()!.AsObject();

    private async Task<JsonObject> ScanAsync(Guid projectId)
    {
        var api = new OvermindApiClient(_client);
        var tools = new RescueScanTools(api, new GitCli(), new ProjectResolver(api));

        return (JsonObject)JsonNode.Parse((await tools.RescueScan(projectId: projectId)).GetRawText())!;
    }

    private void BootstrapRepository()
    {
        Git(Path.Combine(_sandbox, "origin"), "init", "-q", "--bare", "-b", "main");
        Git(_repo, "init", "-q", "-b", "main");
        Git(_repo, "config", "user.name", "e2e-rescuer");
        Git(_repo, "config", "user.email", "e2e-rescuer@example.test");
        Git(_repo, "config", "commit.gpgsign", "false");
        File.WriteAllText(Path.Combine(_repo, "Program.cs"), "// the shipped mainline\n");
        Git(_repo, "add", "-A");
        Git(_repo, "commit", "-q", "-m", "mainline");
        Git(_repo, "remote", "add", "origin", Path.Combine(_sandbox, "origin"));
        Git(_repo, "push", "-q", "-u", "origin", "main");
        Git(_repo, "worktree", "add", "-q", "-b", "feat/240-e2e-rescue", _worktree, "main");
    }

    private static void Git(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        process.ExitCode.ShouldBe(0, $"git {string.Join(' ', arguments)} failed in {workingDirectory}: {error}{output}");
    }

    private async Task<ProjectJson> CreateProjectAsync(string diskPath)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/projects",
            new { Name = $"rescue-e2e-{Guid.NewGuid():N}", DiskPath = diskPath, Description = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions))!;
    }

    private async Task CreateFeatureAsync(Guid projectId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = "240-e2e-rescue", Title = "Rescue the abandoned worktree", Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var status = await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/240-e2e-rescue", new { Status = "inProgress" });

        status.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<Guid> RegisterSessionAsync(string alias, string vendor)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/agent/sessions", new { Alias = alias, Vendor = vendor });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var session = await response.Content.ReadFromJsonAsync<SessionJson>(JsonOptions);

        return session!.Id;
    }

    private async Task<HttpResponseMessage> ClaimAsync(Guid projectId, Guid sessionId, string alias, string vendor)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/agent/projects/{projectId}/planning/features/240-e2e-rescue/claim")
        {
            Content = JsonContent.Create(new { Alias = alias, Vendor = vendor }),
        };
        request.Headers.Add("X-Overmind-Session", sessionId.ToString());

        return await _client.SendAsync(request);
    }

    private async Task PinCodeAsync(Guid projectId, string branch, string worktreePath)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/240-e2e-rescue/pin-code",
            new { Branch = branch, WorktreePath = worktreePath });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task ClearRescuePolicyAsync(Guid projectId)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/claim-settings",
            new { ClaimStaleThresholdMinutes = 15, AgentNamingRules = (string?)null, RescuePolicy = "   " });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task EndSessionAsync(Guid sessionId)
    {
        var response = await _client.PostAsync($"/api/agent/sessions/{sessionId}/end", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<string> FeatureLogAsync(Guid projectId)
    {
        var response = await _client.GetAsync($"/api/projects/{projectId}/planning/features/240-e2e-rescue");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["body"]!.GetValue<string>();
    }

    private async Task<string> TakeoverInfoAsync()
    {
        var response = await _client.GetAsync("/api/notifications");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["items"]!.AsArray()
            .Where(item => item["kind"]!.GetValue<string>() == "info"
                           && item["featureSlug"]?.GetValue<string>() == "240-e2e-rescue")
            .ToList();

        items.ShouldHaveSingleItem();

        return items[0]!["text"]!.GetValue<string>();
    }

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath, string? Description);

    private sealed record SessionJson(Guid Id, string Alias, string Vendor);
}
