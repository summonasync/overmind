using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Http;

namespace Overmind.Infrastructure.Llm;

public sealed class LlmChatClientFactory : ILlmChatClientFactory
{
    public static readonly TimeSpan ChatTimeout = TimeSpan.FromSeconds(120);

    private readonly AppSettingsService _settings;
    private readonly LlmChatClientCache _cache;

    public LlmChatClientFactory(AppSettingsService settings, LlmChatClientCache cache)
    {
        _settings = settings;
        _cache = cache;
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken ct) =>
        !string.IsNullOrWhiteSpace(await _settings.GetAsync(SettingKeys.AgentApiKey, ct));

    public async Task<IChatClient?> CreateAsync(CancellationToken ct)
    {
        var apiKey = await _settings.GetAsync(SettingKeys.AgentApiKey, ct);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var baseUrl = await _settings.GetOrDefaultAsync(SettingKeys.AgentBaseUrl, SettingDefaults.AgentBaseUrl, ct);
        var model = await _settings.GetOrDefaultAsync(SettingKeys.AgentModel, SettingDefaults.AgentModel, ct);

        return await _cache.GetOrCreateAsync(baseUrl, apiKey, model, ct);
    }

    internal static IChatClient CreateChatClient(HttpClient http, string baseUrl, string apiKey, string model) =>
        new ChatClient(model, new ApiKeyCredential(apiKey), new OpenAIClientOptions
        {
            Endpoint = new Uri($"{baseUrl.TrimEnd('/')}/"),
            Transport = new HttpClientPipelineTransport(http),
        }).AsIChatClient();

    internal static HttpMessageHandler CreateChatHandler(string baseUrl) => LoopbackHttp.CreateHandler(baseUrl);
}
