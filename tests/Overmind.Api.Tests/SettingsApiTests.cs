using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class SettingsApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SettingsApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Get_ReturnsDefaults_AndNeverExposesApiKey()
    {
        using var response = await _client.GetAsync("/api/settings");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.ShouldNotContain("secret");
        var document = JsonDocument.Parse(json);

        document.RootElement.GetProperty("embeddingProvider").GetString().ShouldBe("onnx");
        document.RootElement.GetProperty("ollamaBaseUrl").GetString().ShouldBe("http://127.0.0.1:11434");
        document.RootElement.GetProperty("openAiBaseUrl").GetString().ShouldBe("https://api.openai.com");
        document.RootElement.GetProperty("openAiApiKeyConfigured").GetBoolean().ShouldBeFalse();
        document.RootElement.EnumerateObject().Any(p => p.Name.Equals("apiKey", StringComparison.OrdinalIgnoreCase))
            .ShouldBeFalse();
        document.RootElement.EnumerateObject()
            .Any(p => p.Name.Equals("openAiApiKey", StringComparison.OrdinalIgnoreCase))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task Put_PlanningMaterializationSync_PersistsOnAndRejectsUnknownValues()
    {
        using var bad = await _client.PutAsJsonAsync("/api/settings", new { PlanningMaterializationSync = "banana" });
        bad.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var put = await _client.PutAsJsonAsync("/api/settings", new { PlanningMaterializationSync = "on" });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var get = await _client.GetAsync("/api/settings");
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("planningMaterializationSync").GetString().ShouldBe("on");
    }

    [Fact]
    public async Task Put_PersistsValues_AndApiKeyStaysWriteOnly()
    {
        using var put = await _client.PutAsJsonAsync("/api/settings", new
        {
            EmbeddingProvider = "openai",
            OllamaBaseUrl = "http://127.0.0.1:9999",
            OpenAiBaseUrl = "http://127.0.0.1:9998/v1",
            OpenAiApiKey = "super-secret-key-123",
        });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var putJson = await put.Content.ReadAsStringAsync();
        putJson.ShouldNotContain("super-secret-key-123");

        using var get = await _client.GetAsync("/api/settings");
        var json = await get.Content.ReadAsStringAsync();
        json.ShouldNotContain("super-secret-key-123");

        var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("embeddingProvider").GetString().ShouldBe("openai");
        document.RootElement.GetProperty("ollamaBaseUrl").GetString().ShouldBe("http://127.0.0.1:9999");
        document.RootElement.GetProperty("ollamaModel").GetString().ShouldBe("all-minilm-l6-v2");
        document.RootElement.GetProperty("openAiBaseUrl").GetString().ShouldBe("http://127.0.0.1:9998/v1");
        document.RootElement.GetProperty("openAiApiKeyConfigured").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Put_RejectsUnknownProvider()
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new
        {
            EmbeddingProvider = "chromadb",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_RejectsMalformedBaseUrl()
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new
        {
            OllamaBaseUrl = "not a url",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_IncludesOllamaModelDefault()
    {
        using var response = await _client.GetAsync("/api/settings");

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("ollamaModel").GetString().ShouldBe("all-minilm-l6-v2");
    }

    [Fact]
    public async Task Put_OllamaModel_RoundTrips_AndSurvivesPartialUpdates()
    {
        using var put = await _client.PutAsJsonAsync("/api/settings", new { OllamaModel = "  nomic-embed-text  " });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var putDocument = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
        putDocument.RootElement.GetProperty("ollamaModel").GetString().ShouldBe("nomic-embed-text");

        await _client.PutAsJsonAsync("/api/settings", new { EmbeddingProvider = "ollama" });

        using var get = await _client.GetAsync("/api/settings");
        var document = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("ollamaModel").GetString().ShouldBe("nomic-embed-text");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Put_RejectsEmptyOllamaModel(string model)
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { OllamaModel = model });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_RejectsOllamaModelOver100Chars()
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { OllamaModel = new string('m', 101) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_AcceptsHundredCharacterOllamaModel()
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { OllamaModel = new string('m', 100) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("ollamaModel").GetString().ShouldBe(new string('m', 100));
    }

    [Fact]
    public async Task Put_PartialUpdate_KeepsOtherSettings()
    {
        await _client.PutAsJsonAsync("/api/settings", new { EmbeddingProvider = "ollama" });

        using var response = await _client.PutAsJsonAsync("/api/settings", new { OpenAiBaseUrl = "http://127.0.0.1:4321" });

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("embeddingProvider").GetString().ShouldBe("ollama");
        document.RootElement.GetProperty("openAiBaseUrl").GetString().ShouldBe("http://127.0.0.1:4321");
    }

    [Fact]
    public async Task Get_IncludesPlanningDefaults()
    {
        using var response = await _client.GetAsync("/api/settings");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("planningMaterializationDefault").GetString().ShouldBe("on");
        document.RootElement.GetProperty("planningRequireHumanValidationDefault").GetString().ShouldBe("off");
    }

    [Fact]
    public async Task Put_PlanningDefaults_PersistAcrossSecondGet()
    {
        using var put = await _client.PutAsJsonAsync("/api/settings", new
        {
            PlanningMaterializationDefault = "ARCHIVE",
            PlanningRequireHumanValidationDefault = "on",
        });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var putDocument = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
        putDocument.RootElement.GetProperty("planningMaterializationDefault").GetString().ShouldBe("archive");
        putDocument.RootElement.GetProperty("planningRequireHumanValidationDefault").GetString().ShouldBe("on");

        using var get = await _client.GetAsync("/api/settings");
        var document = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("planningMaterializationDefault").GetString().ShouldBe("archive");
        document.RootElement.GetProperty("planningRequireHumanValidationDefault").GetString().ShouldBe("on");
    }

    [Fact]
    public async Task Put_RejectsInvalidPlanningDefaults()
    {
        using var materialization = await _client.PutAsJsonAsync("/api/settings", new
        {
            PlanningMaterializationDefault = "inherit",
        });
        materialization.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var validation = await _client.PutAsJsonAsync("/api/settings", new
        {
            PlanningRequireHumanValidationDefault = "maybe",
        });
        validation.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_IncludesAgentNamingRulesDefault_EmptyByDefault()
    {
        using var response = await _client.GetAsync("/api/settings");

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("planningAgentNamingRulesDefault").GetString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_AgentNamingRulesDefault_RoundTrips_Trimmed_AndSurvivesPartialUpdates()
    {
        using var put = await _client.PutAsJsonAsync(
            "/api/settings", new { PlanningAgentNamingRulesDefault = "  aliases are crew names  " });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var putDocument = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
        putDocument.RootElement.GetProperty("planningAgentNamingRulesDefault").GetString()
            .ShouldBe("aliases are crew names");

        await _client.PutAsJsonAsync("/api/settings", new { EmbeddingProvider = "ollama" });

        using var get = await _client.GetAsync("/api/settings");
        var document = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("planningAgentNamingRulesDefault").GetString()
            .ShouldBe("aliases are crew names");
    }

    [Fact]
    public async Task Put_AgentNamingRulesDefault_EmptyValue_ClearsBackToDefault()
    {
        await _client.PutAsJsonAsync("/api/settings", new { PlanningAgentNamingRulesDefault = "crew names" });

        using var clear = await _client.PutAsJsonAsync("/api/settings", new { PlanningAgentNamingRulesDefault = "" });

        clear.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = JsonDocument.Parse(await clear.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("planningAgentNamingRulesDefault").GetString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_AgentNamingRulesDefault_WhitespaceOnly_CollapsesToEmpty()
    {
        using var response = await _client.PutAsJsonAsync(
            "/api/settings", new { PlanningAgentNamingRulesDefault = "   " });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("planningAgentNamingRulesDefault").GetString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_AgentNamingRulesDefault_RejectsOverProjectLimit()
    {
        using var response = await _client.PutAsJsonAsync(
            "/api/settings", new { PlanningAgentNamingRulesDefault = new string('r', 2001) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_AgentNamingRulesDefault_AcceptsAtProjectLimit()
    {
        using var response = await _client.PutAsJsonAsync(
            "/api/settings", new { PlanningAgentNamingRulesDefault = new string('r', 2000) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("planningAgentNamingRulesDefault").GetString().ShouldBe(new string('r', 2000));
    }

    [Fact]
    public async Task Get_IncludesQdrantEndpointDefault()
    {
        using var response = await _client.GetAsync("/api/settings");

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("qdrantEndpoint").GetString().ShouldBe("http://127.0.0.1:6334");
    }

    [Fact]
    public async Task Put_QdrantEndpoint_RoundTrips_AndSurvivesPartialUpdates()
    {
        using var put = await _client.PutAsJsonAsync(
            "/api/settings", new { QdrantEndpoint = "  http://127.0.0.1:6335  " });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var putDocument = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
        putDocument.RootElement.GetProperty("qdrantEndpoint").GetString().ShouldBe("http://127.0.0.1:6335");

        await _client.PutAsJsonAsync("/api/settings", new { EmbeddingProvider = "ollama" });

        using var get = await _client.GetAsync("/api/settings");
        var document = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("qdrantEndpoint").GetString().ShouldBe("http://127.0.0.1:6335");
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://127.0.0.1:6334")]
    public async Task Put_RejectsInvalidQdrantEndpoint(string endpoint)
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { QdrantEndpoint = endpoint });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_ReindexRecommended_True_WhenProviderChanges()
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { EmbeddingProvider = "ollama" });

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("reindexRecommended").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Put_ReindexRecommended_True_WhenOllamaModelChanges()
    {
        using var response = await _client.PutAsJsonAsync(
            "/api/settings", new { OllamaModel = "nomic-embed-text" });

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("reindexRecommended").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Put_ReindexRecommended_True_WhenQdrantEndpointChanges()
    {
        using var response = await _client.PutAsJsonAsync(
            "/api/settings", new { QdrantEndpoint = "http://127.0.0.1:6335" });

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("reindexRecommended").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Put_ReindexRecommended_False_WhenEmbeddingInputsUnchanged()
    {
        await _client.PutAsJsonAsync("/api/settings", new { EmbeddingProvider = "ollama" });

        using var response = await _client.PutAsJsonAsync("/api/settings", new { EmbeddingProvider = "OLLAMA" });

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("reindexRecommended").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Put_ReindexRecommended_False_WhenOnlyPlanningDefaultsChange()
    {
        using var response = await _client.PutAsJsonAsync(
            "/api/settings", new { PlanningMaterializationDefault = "on" });

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("reindexRecommended").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Get_IncludesNudgeDefaults()
    {
        using var response = await _client.GetAsync("/api/settings");

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("notificationNudgeMode").GetString().ShouldBe("quiet");
        document.RootElement.GetProperty("notificationNudgeIntervalMinutes").GetInt32().ShouldBe(10);
    }

    [Theory]
    [InlineData("quiet")]
    [InlineData("NAG")]
    [InlineData("mini")]
    public async Task Put_NudgeMode_RoundTrips_AndSurvivesPartialUpdates(string mode)
    {
        using var put = await _client.PutAsJsonAsync("/api/settings", new { NotificationNudgeMode = mode });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var putDocument = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
        putDocument.RootElement.GetProperty("notificationNudgeMode").GetString().ShouldBe(mode.ToLowerInvariant());

        await _client.PutAsJsonAsync("/api/settings", new { EmbeddingProvider = "ollama" });

        using var get = await _client.GetAsync("/api/settings");
        var document = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("notificationNudgeMode").GetString().ShouldBe(mode.ToLowerInvariant());
    }

    [Fact]
    public async Task Put_NudgeInterval_RoundTrips_AndSurvivesPartialUpdates()
    {
        using var put = await _client.PutAsJsonAsync("/api/settings", new { NotificationNudgeIntervalMinutes = 25 });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var putDocument = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
        putDocument.RootElement.GetProperty("notificationNudgeIntervalMinutes").GetInt32().ShouldBe(25);

        await _client.PutAsJsonAsync("/api/settings", new { NotificationNudgeMode = "nag" });

        using var get = await _client.GetAsync("/api/settings");
        var document = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("notificationNudgeIntervalMinutes").GetInt32().ShouldBe(25);
        document.RootElement.GetProperty("notificationNudgeMode").GetString().ShouldBe("nag");
    }

    [Theory]
    [InlineData("loud")]
    [InlineData("")]
    public async Task Put_RejectsInvalidNudgeMode(string mode)
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { NotificationNudgeMode = mode });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Put_RejectsNonPositiveNudgeInterval(int minutes)
    {
        using var response = await _client.PutAsJsonAsync(
            "/api/settings", new { NotificationNudgeIntervalMinutes = minutes });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_RejectsNonIntegerNudgeInterval()
    {
        using var response = await _client.PutAsJsonAsync(
            "/api/settings", new { NotificationNudgeIntervalMinutes = "soon" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_IncludesAgentDefaults_AndNeverExposesApiKey()
    {
        using var response = await _client.GetAsync("/api/settings");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var document = JsonDocument.Parse(json);

        document.RootElement.GetProperty("agentBaseUrl").GetString()
            .ShouldBe("https://api.z.ai/api/paas/v4");
        document.RootElement.GetProperty("agentModel").GetString().ShouldBe("glm-5.3-flash");
        document.RootElement.GetProperty("agentApiKeyConfigured").GetBoolean().ShouldBeFalse();
        document.RootElement.GetProperty("agentPerTurnMaxTokens").GetInt32().ShouldBe(8192);
        document.RootElement.GetProperty("agentDailyBudget").GetInt32().ShouldBe(0);
        document.RootElement.EnumerateObject()
            .Any(p => p.Name.Equals("agentApiKey", StringComparison.OrdinalIgnoreCase))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task Put_AgentSettings_Persist_AndKeyStaysWriteOnly()
    {
        using var put = await _client.PutAsJsonAsync("/api/settings", new
        {
            AgentBaseUrl = "http://127.0.0.1:19101/v4",
            AgentModel = "  glm-4.6-air  ",
            AgentApiKey = "agent-secret-42",
            AgentPerTurnMaxTokens = 4096,
            AgentDailyBudget = 2000000,
        });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var putJson = await put.Content.ReadAsStringAsync();
        putJson.ShouldNotContain("agent-secret-42");
        var putDocument = JsonDocument.Parse(putJson);
        putDocument.RootElement.GetProperty("agentBaseUrl").GetString().ShouldBe("http://127.0.0.1:19101/v4");
        putDocument.RootElement.GetProperty("agentModel").GetString().ShouldBe("glm-4.6-air");
        putDocument.RootElement.GetProperty("agentApiKeyConfigured").GetBoolean().ShouldBeTrue();
        putDocument.RootElement.GetProperty("agentPerTurnMaxTokens").GetInt32().ShouldBe(4096);
        putDocument.RootElement.GetProperty("agentDailyBudget").GetInt32().ShouldBe(2000000);

        using var get = await _client.GetAsync("/api/settings");
        var json = await get.Content.ReadAsStringAsync();
        json.ShouldNotContain("agent-secret-42");

        var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("agentBaseUrl").GetString().ShouldBe("http://127.0.0.1:19101/v4");
        document.RootElement.GetProperty("agentModel").GetString().ShouldBe("glm-4.6-air");
        document.RootElement.GetProperty("agentApiKeyConfigured").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Put_AgentPartialUpdate_KeepsOtherAgentSettings()
    {
        await _client.PutAsJsonAsync("/api/settings", new { AgentModel = "glm-4.6-air", AgentPerTurnMaxTokens = 2048 });

        using var response = await _client.PutAsJsonAsync("/api/settings", new { AgentBaseUrl = "http://127.0.0.1:19102/v4" });

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("agentBaseUrl").GetString().ShouldBe("http://127.0.0.1:19102/v4");
        document.RootElement.GetProperty("agentModel").GetString().ShouldBe("glm-4.6-air");
        document.RootElement.GetProperty("agentPerTurnMaxTokens").GetInt32().ShouldBe(2048);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://127.0.0.1:19101")]
    public async Task Put_RejectsInvalidAgentBaseUrl(string baseUrl)
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { AgentBaseUrl = baseUrl });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Put_RejectsEmptyAgentModel(string model)
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { AgentModel = model });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task Put_RejectsNonPositiveAgentPerTurnMaxTokens(int maxTokens)
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { AgentPerTurnMaxTokens = maxTokens });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_RejectsNegativeAgentDailyBudget()
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { AgentDailyBudget = -1 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_AgentRouteMatchThreshold_RoundTrips_AndSurvivesPartialUpdates()
    {
        var put = await _client.PutAsJsonAsync("/api/settings", new { AgentRouteMatchThreshold = 0.643 });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var putDocument = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
        putDocument.RootElement.GetProperty("agentRouteMatchThreshold").GetDouble().ShouldBe(0.643);

        await _client.PutAsJsonAsync("/api/settings", new { AgentModel = "glm-4.6-air" });
        using var get = await _client.GetAsync("/api/settings");
        var document = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("agentRouteMatchThreshold").GetDouble().ShouldBe(0.643);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(-0.2)]
    public async Task Put_RejectsOutOfRangeAgentRouteMatchThreshold(double threshold)
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { AgentRouteMatchThreshold = threshold });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_ZeroAgentDailyBudget_IsAcceptedAsDisabled()
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { AgentDailyBudget = 0 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("agentDailyBudget").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Get_GrimoireMemoryDefaults_AutoDetectCrewAndDailyCompaction()
    {
        using var response = await _client.GetAsync("/api/settings");
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetProperty("agentGrimoireBaseUrl").GetString().ShouldBe("");
        document.RootElement.GetProperty("agentGrimoireTome").GetString().ShouldBe("crew");
        document.RootElement.GetProperty("agentGrimoireCompactionHours").GetInt32().ShouldBe(24);
    }

    [Fact]
    public async Task Put_GrimoireMemorySettings_Persist()
    {
        using var put = await _client.PutAsJsonAsync("/api/settings", new
        {
            AgentGrimoireBaseUrl = "http://127.0.0.1:4770",
            AgentGrimoireTome = "  whispers  ",
            AgentGrimoireCompactionHours = 12,
        });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var putDocument = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
        putDocument.RootElement.GetProperty("agentGrimoireBaseUrl").GetString().ShouldBe("http://127.0.0.1:4770");
        putDocument.RootElement.GetProperty("agentGrimoireTome").GetString().ShouldBe("whispers");
        putDocument.RootElement.GetProperty("agentGrimoireCompactionHours").GetInt32().ShouldBe(12);

        using var get = await _client.GetAsync("/api/settings");
        var document = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("agentGrimoireBaseUrl").GetString().ShouldBe("http://127.0.0.1:4770");
        document.RootElement.GetProperty("agentGrimoireTome").GetString().ShouldBe("whispers");
        document.RootElement.GetProperty("agentGrimoireCompactionHours").GetInt32().ShouldBe(12);
    }

    [Fact]
    public async Task Put_EmptyGrimoireBaseUrl_IsAcceptedAsAutoDetect()
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { AgentGrimoireBaseUrl = "   " });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("agentGrimoireBaseUrl").GetString().ShouldBe("");
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://127.0.0.1:4770")]
    public async Task Put_RejectsInvalidGrimoireBaseUrl(string baseUrl)
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { AgentGrimoireBaseUrl = baseUrl });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Put_RejectsEmptyGrimoireTome(string tome)
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { AgentGrimoireTome = tome });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_RejectsNegativeGrimoireCompactionHours()
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { AgentGrimoireCompactionHours = -1 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_ZeroGrimoireCompactionHours_IsAcceptedAsDisabled()
    {
        using var response = await _client.PutAsJsonAsync("/api/settings", new { AgentGrimoireCompactionHours = 0 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("agentGrimoireCompactionHours").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Get_IncludesAgentInstructions_EmptyByDefault()
    {
        using var response = await _client.GetAsync("/api/settings");

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("agentInstructions").GetString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_AgentInstructions_RoundTrips_AndSurvivesPartialUpdates()
    {
        using var put = await _client.PutAsJsonAsync(
            "/api/settings", new { AgentInstructions = "Be terse. Answer in Russian." });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var putDocument = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
        putDocument.RootElement.GetProperty("agentInstructions").GetString().ShouldBe("Be terse. Answer in Russian.");

        await _client.PutAsJsonAsync("/api/settings", new { AgentModel = "glm-4.6-air" });

        using var get = await _client.GetAsync("/api/settings");
        var document = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("agentInstructions").GetString().ShouldBe("Be terse. Answer in Russian.");
    }

    [Fact]
    public async Task Put_EmptyAgentInstructions_ClearsBackToDefault()
    {
        await _client.PutAsJsonAsync("/api/settings", new { AgentInstructions = "be terse" });

        using var clear = await _client.PutAsJsonAsync("/api/settings", new { AgentInstructions = "" });

        clear.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = JsonDocument.Parse(await clear.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("agentInstructions").GetString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_AgentInstructions_AcceptsAtCap()
    {
        using var response = await _client.PutAsJsonAsync(
            "/api/settings", new { AgentInstructions = new string('x', 4000) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("agentInstructions").GetString().ShouldBe(new string('x', 4000));
    }

    [Fact]
    public async Task Put_RejectsAgentInstructionsOverCap()
    {
        using var response = await _client.PutAsJsonAsync(
            "/api/settings", new { AgentInstructions = new string('x', 4001) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_FreshInstance_ReportsHumanDigestDisabled()
    {
        using var response = await _client.GetAsync("/api/settings");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("humanDigestEnabled").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Put_HumanDigestEnabled_RoundTrips_AndSurvivesPartialUpdates()
    {
        using var put = await _client.PutAsJsonAsync("/api/settings", new { HumanDigestEnabled = true });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var putDocument = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
        putDocument.RootElement.GetProperty("humanDigestEnabled").GetBoolean().ShouldBeTrue();

        await _client.PutAsJsonAsync("/api/settings", new { EmbeddingProvider = "ollama" });

        using var get = await _client.GetAsync("/api/settings");
        var document = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("humanDigestEnabled").GetBoolean().ShouldBeTrue();

        await _client.PutAsJsonAsync("/api/settings", new { HumanDigestEnabled = false });
        using var afterDisable = await _client.GetAsync("/api/settings");
        var disabled = JsonDocument.Parse(await afterDisable.Content.ReadAsStringAsync());
        disabled.RootElement.GetProperty("humanDigestEnabled").GetBoolean().ShouldBeFalse();
    }
}
