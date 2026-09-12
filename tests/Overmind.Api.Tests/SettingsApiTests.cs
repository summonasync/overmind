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
        document.RootElement.GetProperty("planningMaterializationDefault").GetString().ShouldBe("off");
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
}
