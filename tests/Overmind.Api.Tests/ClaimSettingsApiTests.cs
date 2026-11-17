using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class ClaimSettingsApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ClaimSettingsApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Get_FreshProject_ReturnsDefaultThresholdAndNullRules()
    {
        var project = await CreateProjectAsync();

        var settings = await _client.GetFromJsonAsync<ClaimSettingsJson>(Url(project.Id), JsonOptions);

        settings!.ClaimStaleThresholdMinutes.ShouldBe(Project.DefaultClaimStaleThresholdMinutes);
        settings.AgentNamingRules.ShouldBeNull();
    }

    [Fact]
    public async Task Put_ThenGet_RoundTripsThresholdAndRules()
    {
        var project = await CreateProjectAsync();

        var put = await _client.PutAsJsonAsync(Url(project.Id), new
        {
            ClaimStaleThresholdMinutes = 45,
            AgentNamingRules = "aliases are crew names, vendor is the runtime",
        });

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stored = await put.Content.ReadFromJsonAsync<ClaimSettingsJson>(JsonOptions);
        stored!.ClaimStaleThresholdMinutes.ShouldBe(45);
        stored.AgentNamingRules.ShouldBe("aliases are crew names, vendor is the runtime");

        var fetched = await _client.GetFromJsonAsync<ClaimSettingsJson>(Url(project.Id), JsonOptions);
        fetched!.ClaimStaleThresholdMinutes.ShouldBe(45);
        fetched.AgentNamingRules.ShouldBe("aliases are crew names, vendor is the runtime");
    }

    [Fact]
    public async Task Put_WhitespaceOnlyRules_PersistsNull()
    {
        var project = await CreateProjectAsync();

        var put = await _client.PutAsJsonAsync(Url(project.Id), new
        {
            ClaimStaleThresholdMinutes = 20,
            AgentNamingRules = "   ",
        });

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await _client.GetFromJsonAsync<ClaimSettingsJson>(Url(project.Id), JsonOptions);
        fetched!.AgentNamingRules.ShouldBeNull();
        fetched.ClaimStaleThresholdMinutes.ShouldBe(20);
    }

    [Fact]
    public async Task Put_ZeroThresholdAndNullRules_IsStored()
    {
        var project = await CreateProjectAsync();

        var put = await _client.PutAsJsonAsync(Url(project.Id), new
        {
            ClaimStaleThresholdMinutes = 0,
            AgentNamingRules = (string?)null,
        });

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await _client.GetFromJsonAsync<ClaimSettingsJson>(Url(project.Id), JsonOptions);
        fetched!.ClaimStaleThresholdMinutes.ShouldBe(0);
        fetched.AgentNamingRules.ShouldBeNull();
    }

    [Fact]
    public async Task Put_NegativeThreshold_Returns400WithoutStoring()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PutAsJsonAsync(Url(project.Id), new
        {
            ClaimStaleThresholdMinutes = -1,
            AgentNamingRules = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem!.Errors.ShouldContainKey("ClaimStaleThresholdMinutes");
        problem.Errors["ClaimStaleThresholdMinutes"][0].ShouldContain("0 disables stale detection");

        var untouched = await _client.GetFromJsonAsync<ClaimSettingsJson>(Url(project.Id), JsonOptions);
        untouched!.ClaimStaleThresholdMinutes.ShouldBe(Project.DefaultClaimStaleThresholdMinutes);
    }

    [Fact]
    public async Task Put_RulesOverLimit_Returns400()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PutAsJsonAsync(Url(project.Id), new
        {
            ClaimStaleThresholdMinutes = 15,
            AgentNamingRules = new string('x', Project.MaxNamingRulesLength + 1),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem!.Errors.ShouldContainKey("AgentNamingRules");
        problem.Errors["AgentNamingRules"][0].ShouldContain("2000");
    }

    [Fact]
    public async Task Put_RulesAtLimit_IsStored()
    {
        var project = await CreateProjectAsync();
        var rules = new string('x', Project.MaxNamingRulesLength);

        var put = await _client.PutAsJsonAsync(Url(project.Id), new
        {
            ClaimStaleThresholdMinutes = 15,
            AgentNamingRules = rules,
        });

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await _client.GetFromJsonAsync<ClaimSettingsJson>(Url(project.Id), JsonOptions);
        fetched!.AgentNamingRules.ShouldBe(rules);
    }

    [Fact]
    public async Task Get_UnknownProject_Returns404()
    {
        var response = await _client.GetAsync(Url(Guid.NewGuid()));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_FreshProject_RescuePolicyIsOwnerDirectiveVerbatim()
    {
        var project = await CreateProjectAsync();

        var settings = await _client.GetFromJsonAsync<ClaimSettingsJson>(Url(project.Id), JsonOptions);

        settings!.RescuePolicy.ShouldBe(
            "everything unpushed may be pushed, everything unmerged I allow merging");
    }

    [Fact]
    public async Task Put_ThenGet_RoundTripsTrimmedRescuePolicy()
    {
        var project = await CreateProjectAsync();

        var put = await _client.PutAsJsonAsync(Url(project.Id), new
        {
            ClaimStaleThresholdMinutes = 45,
            AgentNamingRules = (string?)null,
            RescuePolicy = "  push anything, merge only through review  ",
        });

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stored = await put.Content.ReadFromJsonAsync<ClaimSettingsJson>(JsonOptions);
        stored!.RescuePolicy.ShouldBe("push anything, merge only through review");

        var fetched = await _client.GetFromJsonAsync<ClaimSettingsJson>(Url(project.Id), JsonOptions);
        fetched!.RescuePolicy.ShouldBe("push anything, merge only through review");
    }

    [Fact]
    public async Task Put_LegacyBodyWithoutRescuePolicy_KeepsStoredText()
    {
        var project = await CreateProjectAsync();
        await _client.PutAsJsonAsync(Url(project.Id), new
        {
            ClaimStaleThresholdMinutes = 45,
            AgentNamingRules = (string?)null,
            RescuePolicy = "kept across legacy saves",
        });

        var put = await _client.PutAsJsonAsync(Url(project.Id), new
        {
            ClaimStaleThresholdMinutes = 10,
            AgentNamingRules = "new rules",
        });

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await _client.GetFromJsonAsync<ClaimSettingsJson>(Url(project.Id), JsonOptions);
        fetched!.RescuePolicy.ShouldBe("kept across legacy saves");
        fetched.ClaimStaleThresholdMinutes.ShouldBe(10);
        fetched.AgentNamingRules.ShouldBe("new rules");
    }

    [Fact]
    public async Task Put_WhitespaceOnlyRescuePolicy_PersistsNull()
    {
        var project = await CreateProjectAsync();

        var put = await _client.PutAsJsonAsync(Url(project.Id), new
        {
            ClaimStaleThresholdMinutes = 20,
            AgentNamingRules = (string?)null,
            RescuePolicy = "   ",
        });

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await _client.GetFromJsonAsync<ClaimSettingsJson>(Url(project.Id), JsonOptions);
        fetched!.RescuePolicy.ShouldBeNull();
        fetched.ClaimStaleThresholdMinutes.ShouldBe(20);
    }

    [Fact]
    public async Task Put_RescuePolicyOverLimit_Returns400WithoutStoring()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PutAsJsonAsync(Url(project.Id), new
        {
            ClaimStaleThresholdMinutes = 15,
            AgentNamingRules = (string?)null,
            RescuePolicy = new string('x', Project.MaxRescuePolicyLength + 1),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem!.Errors.ShouldContainKey("RescuePolicy");
        problem.Errors["RescuePolicy"][0].ShouldContain("2000");

        var untouched = await _client.GetFromJsonAsync<ClaimSettingsJson>(Url(project.Id), JsonOptions);
        untouched!.RescuePolicy.ShouldBe(Project.DefaultRescuePolicy);
    }

    [Fact]
    public async Task Put_UnknownProject_Returns404()
    {
        var response = await _client.PutAsJsonAsync(Url(Guid.NewGuid()), new
        {
            ClaimStaleThresholdMinutes = 15,
            AgentNamingRules = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static string Url(Guid projectId) =>
        $"/api/projects/{projectId}/claim-settings";

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var name = $"claim-settings-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record ClaimSettingsJson(
        int ClaimStaleThresholdMinutes, string? AgentNamingRules, string? RescuePolicy);

    public sealed record ValidationProblemJson(Dictionary<string, string[]> Errors);
}
