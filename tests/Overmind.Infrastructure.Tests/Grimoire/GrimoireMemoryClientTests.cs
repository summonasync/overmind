using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Grimoire;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Grimoire;

public sealed class GrimoireMemoryClientTests : IDisposable
{
    private static readonly Uri StubBaseUrl = new("http://127.0.0.1:19998");

    private readonly TempDatabase _temp = new();
    private readonly FakeGrimoireServer _server = new();
    private readonly IGrimoireBinaryLocator _locator = Substitute.For<IGrimoireBinaryLocator>();
    private readonly IGrimoireCli _cli = Substitute.For<IGrimoireCli>();
    private readonly IGrimoireHealthClient _health = Substitute.For<IGrimoireHealthClient>();
    private readonly string _configDirectory;
    private readonly List<ServiceProvider> _providers = [];

    public GrimoireMemoryClientTests()
    {
        _configDirectory = Path.Combine(Path.GetTempPath(), $"overmind-grimoire-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_configDirectory);
    }

    [Fact]
    public async Task ResolveEndpointAsync_OverrideUrl_PassesHealthProbeAndWins()
    {
        var client = CreateClient();
        await ConfigureAsync(overrideUrl: StubBaseUrl.ToString());

        var endpoint = await client.ResolveEndpointAsync(CancellationToken.None);

        endpoint.BaseUrl.ShouldBe(StubBaseUrl.ToString().TrimEnd('/'));
        endpoint.Tome.ShouldBe("crew");
        endpoint.ActorAlias.ShouldBe("overmind-agent");
    }

    [Fact]
    public async Task ResolveEndpointAsync_OverrideUnhealthy_ThrowsUnavailable()
    {
        var client = CreateClient();
        _server.HealthStatus = HttpStatusCode.ServiceUnavailable;
        await ConfigureAsync(overrideUrl: StubBaseUrl.ToString());

        var failure = await Should.ThrowAsync<GrimoireMemoryException>(
            () => client.ResolveEndpointAsync(CancellationToken.None));

        failure.Message.ShouldContain("failed its health probe");
    }

    [Fact]
    public async Task ResolveEndpointAsync_DetectionRunning_UsesDetectedPort()
    {
        var client = CreateClient();
        ConfigureDetection(binaryPath: "/usr/local/bin/grimoire", configPort: 51234);

        var endpoint = await client.ResolveEndpointAsync(CancellationToken.None);

        endpoint.BaseUrl.ShouldBe("http://127.0.0.1:51234");
    }

    [Fact]
    public async Task ResolveEndpointAsync_NoDetectionNoOverride_ThrowsUnavailable()
    {
        var client = CreateClient();
        _locator.Locate(Arg.Any<string?>()).Returns((string?)null);
        _cli.ProbeVersionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GrimoireVersionProbe(false, null));

        var failure = await Should.ThrowAsync<GrimoireMemoryException>(
            () => client.ResolveEndpointAsync(CancellationToken.None));

        failure.Message.ShouldContain("no running grimoire instance was detected");
    }

    [Fact]
    public async Task ResolveEndpointAsync_ConfiguredTomeAndAlias_FlowIntoEndpoint()
    {
        var client = CreateClient();
        await ConfigureAsync(overrideUrl: StubBaseUrl.ToString(), tome: "whispers", alias: "billy-jr");

        var endpoint = await client.ResolveEndpointAsync(CancellationToken.None);

        endpoint.Tome.ShouldBe("whispers");
        endpoint.ActorAlias.ShouldBe("billy-jr");
    }

    [Fact]
    public async Task ResolveEndpointAsync_BlankTomeFallsBackToCrew()
    {
        var client = CreateClient();
        await ConfigureAsync(overrideUrl: StubBaseUrl.ToString(), tome: "   ");

        var endpoint = await client.ResolveEndpointAsync(CancellationToken.None);

        endpoint.Tome.ShouldBe("crew");
    }

    [Fact]
    public async Task RememberAsync_LandsInConfiguredTomeWithProvenanceTags()
    {
        var client = CreateClient();
        var endpoint = new GrimoireMemoryEndpoint(StubBaseUrl.ToString().TrimEnd('/'), "crew", "overmind-agent");

        var result = await client.RememberAsync(
            endpoint,
            new GrimoireMemoryWriteRequest("owner prefers feature branches", "global", "fact", ["pref"], 0.9, null),
            CancellationToken.None);

        result.Id.ShouldBe(_server.LastRememberId);
        result.Tome.ShouldBe("crew");
        var body = JsonDocument.Parse(_server.Single(HttpMethod.Post, "/api/memories").Body).RootElement;
        body.GetProperty("tome").GetString()!.ShouldBe("crew");
        body.GetProperty("scope").GetString()!.ShouldBe("global");
        body.GetProperty("kind").GetString()!.ShouldBe("fact");
        body.GetProperty("importance").GetDouble().ShouldBe(0.9);
        var tags = body.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).ToList();
        tags.ShouldContain("overmind://actor/overmind-agent");
        tags.ShouldContain("overmind://vendor/overmind-host");
        tags.ShouldContain("pref");
        body.TryGetProperty("supersedes", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task RememberAsync_WithSupersedes_SendsTheTargetId()
    {
        var client = CreateClient();
        var endpoint = new GrimoireMemoryEndpoint(StubBaseUrl.ToString().TrimEnd('/'), "crew", "overmind-agent");
        var superseded = Guid.NewGuid();

        var result = await client.RememberAsync(
            endpoint,
            new GrimoireMemoryWriteRequest("restate", "global", "fact", [], 0.5, superseded),
            CancellationToken.None);

        var body = JsonDocument.Parse(_server.Single(HttpMethod.Post, "/api/memories").Body).RootElement;
        body.GetProperty("supersedes").GetString()!.ShouldBe(superseded.ToString());
        result.SupersededId.ShouldBeNull();
    }

    [Fact]
    public async Task RememberAsync_ErrorStatus_ThrowsWithApiDetail()
    {
        var client = CreateClient();
        _server.RememberStatus = HttpStatusCode.BadRequest;
        var endpoint = new GrimoireMemoryEndpoint(StubBaseUrl.ToString().TrimEnd('/'), "crew", "overmind-agent");

        var failure = await Should.ThrowAsync<GrimoireMemoryException>(() => client.RememberAsync(
            endpoint,
            new GrimoireMemoryWriteRequest("x", "global", "fact", [], 0.5, null),
            CancellationToken.None));

        failure.Message.ShouldContain("400");
        failure.Message.ShouldContain("POST /api/memories");
    }

    [Fact]
    public async Task RecallAsync_SendsQueryAndParsesItems()
    {
        var client = CreateClient();
        var endpoint = new GrimoireMemoryEndpoint(StubBaseUrl.ToString().TrimEnd('/'), "crew", "overmind-agent");

        var records = await client.RecallAsync(
            endpoint, "branch policy q&a", "fact", "policy", 7, CancellationToken.None);

        var captured = _server.Single(HttpMethod.Get, "/api/memories");
        captured.Query.ShouldContain("query=branch%20policy%20q%26a");
        captured.Query.ShouldContain("kind=fact");
        captured.Query.ShouldContain("tag=policy");
        captured.Query.ShouldContain("limit=7");
        captured.Query.ShouldContain("tome=crew");
        records.ShouldHaveSingleItem();
        records[0].Id.ShouldBe(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        records[0].Content.ShouldBe("crew ships on branches");
        records[0].Kind.ShouldBe("fact");
        records[0].ActorAlias.ShouldBe("billy-herrington");
    }

    [Fact]
    public async Task ListTopFactsAsync_RequestsKindFactOnly()
    {
        var client = CreateClient();
        var endpoint = new GrimoireMemoryEndpoint(StubBaseUrl.ToString().TrimEnd('/'), "crew", "overmind-agent");

        var facts = await client.ListTopFactsAsync(endpoint, 5, CancellationToken.None);

        var captured = _server.Single(HttpMethod.Get, "/api/memories");
        captured.Query.ShouldContain("kind=fact");
        captured.Query.ShouldContain("limit=5");
        captured.Query.ShouldNotContain("query=");
        facts.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetCompactionCandidatesAsync_ParsesGroups()
    {
        var client = CreateClient();
        _server.CandidateGroupsJson =
            """{"memoryIds":["22222222-2222-2222-2222-222222222222","33333333-3333-3333-3333-333333333333"],"similarity":0.81,"entries":[""" +
            """{"id":"22222222-2222-2222-2222-222222222222","content":"noted build quirk","kind":"journal","tags":[],"createdAt":"2026-10-20T10:00:00Z","lastAccessedAt":"2026-10-21T10:00:00Z","actor":{"alias":"claude-core","vendor":null}}]}""";
        var endpoint = new GrimoireMemoryEndpoint(StubBaseUrl.ToString().TrimEnd('/'), "crew", "overmind-agent");

        var groups = await client.GetCompactionCandidatesAsync(endpoint, CancellationToken.None);

        groups.ShouldHaveSingleItem();
        groups[0].Similarity.ShouldBe(0.81);
        groups[0].MemoryIds.Count.ShouldBe(2);
        groups[0].Entries[0].Content.ShouldBe("noted build quirk");
    }

    [Fact]
    public async Task GetCompactionCandidatesAsync_MissingEndpoint_ThrowsHonest()
    {
        var client = CreateClient();
        _server.CandidatesEndpointExists = false;
        var endpoint = new GrimoireMemoryEndpoint(StubBaseUrl.ToString().TrimEnd('/'), "crew", "overmind-agent");

        var failure = await Should.ThrowAsync<GrimoireMemoryException>(
            () => client.GetCompactionCandidatesAsync(endpoint, CancellationToken.None));

        failure.Message.ShouldContain("404");
        failure.Message.ShouldContain("/api/compaction/candidates");
    }

    [Fact]
    public async Task ForgetAsync_SendsReasonAndTome()
    {
        var client = CreateClient();
        var endpoint = new GrimoireMemoryEndpoint(StubBaseUrl.ToString().TrimEnd('/'), "crew", "overmind-agent");
        var memoryId = Guid.NewGuid();

        await client.ForgetAsync(
            memoryId, "compacted into 44444444-4444-4444-4444-444444444444", endpoint, CancellationToken.None);

        var captured = _server.Single(HttpMethod.Post, $"/api/memories/{memoryId}/forget");
        var body = JsonDocument.Parse(captured.Body).RootElement;
        body.GetProperty("reason").GetString()!.ShouldBe("compacted into 44444444-4444-4444-4444-444444444444");
        body.GetProperty("tome").GetString()!.ShouldBe("crew");
    }

    private GrimoireMemoryClient CreateClient()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new AppSettingsService(_temp.CreateContext()));
        services.AddScoped<GrimoireDetectionService>(provider => new GrimoireDetectionService(
            provider.GetRequiredService<AppSettingsService>(),
            _locator,
            _cli,
            _health,
            new GrimoireConfigReader(_configDirectory)));
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return new GrimoireMemoryClient(
            provider.GetRequiredService<IServiceScopeFactory>(), new HttpClient(_server));
    }

    private async Task ConfigureAsync(string? overrideUrl = null, string? tome = null, string? alias = null)
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);
        if (overrideUrl is not null)
        {
            await settings.SetAsync(SettingKeys.AgentGrimoireBaseUrl, overrideUrl, CancellationToken.None);
        }

        if (tome is not null)
        {
            await settings.SetAsync(SettingKeys.AgentGrimoireTome, tome, CancellationToken.None);
        }

        if (alias is not null)
        {
            await settings.SetAsync(SettingKeys.AgentAlias, alias, CancellationToken.None);
        }
    }

    private void ConfigureDetection(string? binaryPath, int? configPort)
    {
        _locator.Locate(Arg.Any<string?>()).Returns(binaryPath);
        _cli.ProbeVersionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GrimoireVersionProbe(binaryPath is not null, binaryPath is not null ? "0.5.0" : null));
        _health.IsHealthyAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(binaryPath is not null);
        if (configPort is { } port)
        {
            File.WriteAllText(
                Path.Combine(_configDirectory, "config.json"),
                JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["serve"] = new Dictionary<string, object?> { ["port"] = port },
                }));
        }
    }

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }

        _server.Dispose();
        _temp.Dispose();
        Directory.Delete(_configDirectory, recursive: true);
    }
}
