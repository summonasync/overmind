using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class OnboardAgentNamingRulesApiTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OnboardAgentNamingRulesApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-naming-{Guid.NewGuid():N}.db");
        _factory = new ApiFactory(databasePath);
        _client = _factory.CreateClient();
        _db = new SqliteConnection($"Data Source={databasePath};Pooling=false");
        _db.Open();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Onboard_BothEmpty_ReturnsGenericHint()
    {
        var project = await CreateProjectAsync();

        var body = await OnboardAsync(project.DiskPath);

        body.GetProperty("agentIdentity").GetProperty("namingRules").GetString()!
            .ShouldContain("invent an alias");
    }

    [Fact]
    public async Task Onboard_ProjectEmpty_GlobalDefaultSet_ReturnsGlobalDefault()
    {
        var project = await CreateProjectAsync();
        await PutGlobalRulesAsync("global: aliases are crew names");

        var body = await OnboardAsync(project.DiskPath);

        body.GetProperty("agentIdentity").GetProperty("namingRules").GetString()!
            .ShouldBe("global: aliases are crew names");
    }

    [Fact]
    public async Task Onboard_ProjectRulesSet_WinOverGlobalDefault()
    {
        var project = await CreateProjectAsync();
        await PutGlobalRulesAsync("global: aliases are crew names");
        await ExecuteAsync(
            "UPDATE Projects SET AgentNamingRules = 'project: aliases follow the team roster' " +
            $"WHERE Id = '{project.Id.ToString().ToUpperInvariant()}'");

        var body = await OnboardAsync(project.DiskPath);

        body.GetProperty("agentIdentity").GetProperty("namingRules").GetString()!
            .ShouldBe("project: aliases follow the team roster");
    }

    [Fact]
    public async Task Onboard_ProjectEmpty_GlobalWhitespaceInRawStore_ReturnsGenericHint()
    {
        var project = await CreateProjectAsync();
        await ExecuteAsync(
            "INSERT INTO AppSettings (Key, Value) VALUES ('planning.agentNamingRulesDefault', '   ')");

        var body = await OnboardAsync(project.DiskPath);

        body.GetProperty("agentIdentity").GetProperty("namingRules").GetString()!
            .ShouldContain("invent an alias");
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var name = $"naming-{Guid.NewGuid():N}";
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

    private async Task PutGlobalRulesAsync(string rules)
    {
        using var response = await _client.PutAsJsonAsync(
            "/api/settings", new { PlanningAgentNamingRulesDefault = rules }, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<JsonElement> OnboardAsync(string cwd)
    {
        using var response = await _client.PostAsJsonAsync("/api/onboard", new { Cwd = cwd }, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body;
    }

    private async Task ExecuteAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);
}
