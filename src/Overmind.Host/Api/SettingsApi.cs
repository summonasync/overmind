using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.VectorSearch;

namespace Overmind.Host.Api;

public static class SettingsApi
{
    public static RouteGroupBuilder MapSettingsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapGet("/settings", GetSettings);
        group.MapPut("/settings", UpdateSettings);
        group.MapPost("/settings/test", TestSettings);
        return group;
    }

    private static async Task<IResult> GetSettings(AppSettingsService settings, CancellationToken ct)
    {
        var current = await ReadSettingsAsync(settings, ct);
        return Results.Ok(current);
    }

    private static async Task<IResult> UpdateSettings(
        UpdateSettingsRequest request,
        AppSettingsService settings,
        CancellationToken ct)
    {
        if (request.EmbeddingProvider is not null &&
            !SettingDefaults.EmbeddingProviders.Contains(request.EmbeddingProvider, StringComparer.OrdinalIgnoreCase))
        {
            return InvalidSetting(
                nameof(request.EmbeddingProvider),
                $"Provider must be one of: {string.Join(", ", SettingDefaults.EmbeddingProviders)}.");
        }

        if (request.OllamaBaseUrl is not null && !IsValidHttpUrl(request.OllamaBaseUrl))
        {
            return InvalidSetting(nameof(request.OllamaBaseUrl), "Must be a valid http(s) URL.");
        }

        if (request.OllamaModel is not null)
        {
            var trimmedModel = request.OllamaModel.Trim();
            if (trimmedModel.Length == 0 || trimmedModel.Length > 100)
            {
                return InvalidSetting(nameof(request.OllamaModel), "Must be between 1 and 100 characters.");
            }
        }

        if (request.OpenAiBaseUrl is not null && !IsValidHttpUrl(request.OpenAiBaseUrl))
        {
            return InvalidSetting(nameof(request.OpenAiBaseUrl), "Must be a valid http(s) URL.");
        }

        if (request.QdrantEndpoint is not null && !IsValidHttpUrl(request.QdrantEndpoint))
        {
            return InvalidSetting(nameof(request.QdrantEndpoint), "Must be a valid http(s) URL.");
        }

        if (request.PlanningMaterializationDefault is not null &&
            !SettingDefaults.PlanningMaterializationDefaults.Contains(
                request.PlanningMaterializationDefault, StringComparer.OrdinalIgnoreCase))
        {
            return InvalidSetting(
                nameof(request.PlanningMaterializationDefault),
                $"Must be one of: {string.Join(", ", SettingDefaults.PlanningMaterializationDefaults)}.");
        }

        if (request.PlanningRequireHumanValidationDefault is not null &&
            !SettingDefaults.PlanningRequireHumanValidationDefaults.Contains(
                request.PlanningRequireHumanValidationDefault, StringComparer.OrdinalIgnoreCase))
        {
            return InvalidSetting(
                nameof(request.PlanningRequireHumanValidationDefault),
                $"Must be one of: {string.Join(", ", SettingDefaults.PlanningRequireHumanValidationDefaults)}.");
        }

        var reindexRecommended = await ChangesEmbeddingInputsAsync(settings, request, ct);

        if (request.EmbeddingProvider is not null)
        {
            await settings.SetAsync(
                SettingKeys.EmbeddingProvider, request.EmbeddingProvider.ToLowerInvariant(), ct);
        }

        if (request.OllamaBaseUrl is not null)
        {
            await settings.SetAsync(SettingKeys.EmbeddingOllamaBaseUrl, request.OllamaBaseUrl.Trim(), ct);
        }

        if (request.OllamaModel is not null)
        {
            await settings.SetAsync(SettingKeys.EmbeddingOllamaModel, request.OllamaModel.Trim(), ct);
        }

        if (request.OpenAiBaseUrl is not null)
        {
            await settings.SetAsync(SettingKeys.EmbeddingOpenAiBaseUrl, request.OpenAiBaseUrl.Trim(), ct);
        }

        if (!string.IsNullOrEmpty(request.OpenAiApiKey))
        {
            await settings.SetAsync(SettingKeys.EmbeddingOpenAiApiKey, request.OpenAiApiKey, ct);
        }

        if (request.QdrantEndpoint is not null)
        {
            await settings.SetAsync(SettingKeys.QdrantEndpoint, request.QdrantEndpoint.Trim(), ct);
        }

        if (request.PlanningMaterializationDefault is not null)
        {
            await settings.SetAsync(
                SettingKeys.PlanningMaterializationDefault,
                request.PlanningMaterializationDefault.ToLowerInvariant(), ct);
        }

        if (request.PlanningRequireHumanValidationDefault is not null)
        {
            await settings.SetAsync(
                SettingKeys.PlanningRequireHumanValidationDefault,
                request.PlanningRequireHumanValidationDefault.ToLowerInvariant(), ct);
        }

        var updated = await ReadSettingsAsync(settings, ct);
        return Results.Ok(new UpdateSettingsResponse(
            updated.EmbeddingProvider,
            updated.OllamaBaseUrl,
            updated.OllamaModel,
            updated.OpenAiBaseUrl,
            updated.QdrantEndpoint,
            updated.OpenAiApiKeyConfigured,
            updated.PlanningMaterializationDefault,
            updated.PlanningRequireHumanValidationDefault,
            reindexRecommended));
    }

    private static async Task<IResult> TestSettings(
        TestSettingsRequest request,
        VectorSearchTester tester,
        CancellationToken ct)
    {
        if (request.Provider is not null &&
            !SettingDefaults.EmbeddingProviders.Contains(request.Provider, StringComparer.OrdinalIgnoreCase))
        {
            return InvalidSetting(
                nameof(request.Provider),
                $"Provider must be one of: {string.Join(", ", SettingDefaults.EmbeddingProviders)}.");
        }

        if (request.OllamaBaseUrl is not null && !IsValidHttpUrl(request.OllamaBaseUrl))
        {
            return InvalidSetting(nameof(request.OllamaBaseUrl), "Must be a valid http(s) URL.");
        }

        if (request.OllamaModel is not null)
        {
            var trimmedModel = request.OllamaModel.Trim();
            if (trimmedModel.Length == 0 || trimmedModel.Length > 100)
            {
                return InvalidSetting(nameof(request.OllamaModel), "Must be between 1 and 100 characters.");
            }
        }

        if (request.OpenAiBaseUrl is not null && !IsValidHttpUrl(request.OpenAiBaseUrl))
        {
            return InvalidSetting(nameof(request.OpenAiBaseUrl), "Must be a valid http(s) URL.");
        }

        if (request.QdrantEndpoint is not null && !IsValidHttpUrl(request.QdrantEndpoint))
        {
            return InvalidSetting(nameof(request.QdrantEndpoint), "Must be a valid http(s) URL.");
        }

        var report = await tester.TestAsync(
            new VectorSearchCandidate(
                request.Provider?.Trim(),
                request.OllamaBaseUrl?.Trim(),
                request.OllamaModel?.Trim(),
                request.OpenAiBaseUrl?.Trim(),
                request.QdrantEndpoint?.Trim()),
            ct);

        return Results.Ok(report);
    }

    private static async Task<SettingsResponse> ReadSettingsAsync(
        AppSettingsService settings, CancellationToken ct)
    {
        var provider = await settings.GetOrDefaultAsync(
            SettingKeys.EmbeddingProvider, SettingDefaults.EmbeddingProvider, ct);
        var ollamaBaseUrl = await settings.GetOrDefaultAsync(
            SettingKeys.EmbeddingOllamaBaseUrl, SettingDefaults.EmbeddingOllamaBaseUrl, ct);
        var ollamaModel = await settings.GetOrDefaultAsync(
            SettingKeys.EmbeddingOllamaModel, SettingDefaults.EmbeddingOllamaModel, ct);
        var openAiBaseUrl = await settings.GetOrDefaultAsync(
            SettingKeys.EmbeddingOpenAiBaseUrl, SettingDefaults.EmbeddingOpenAiBaseUrl, ct);
        var qdrantEndpoint = await settings.GetOrDefaultAsync(
            SettingKeys.QdrantEndpoint, SettingDefaults.QdrantEndpoint, ct);
        var apiKeyConfigured = await settings.HasAsync(SettingKeys.EmbeddingOpenAiApiKey, ct);
        var planningMaterializationDefault = await settings.GetOrDefaultAsync(
            SettingKeys.PlanningMaterializationDefault, SettingDefaults.PlanningMaterializationDefault, ct);
        var planningRequireHumanValidationDefault = await settings.GetOrDefaultAsync(
            SettingKeys.PlanningRequireHumanValidationDefault, SettingDefaults.PlanningRequireHumanValidationDefault, ct);

        return new SettingsResponse(
            provider, ollamaBaseUrl, ollamaModel, openAiBaseUrl, qdrantEndpoint, apiKeyConfigured,
            planningMaterializationDefault, planningRequireHumanValidationDefault);
    }

    private static async Task<bool> ChangesEmbeddingInputsAsync(
        AppSettingsService settings, UpdateSettingsRequest request, CancellationToken ct)
    {
        if (request.EmbeddingProvider is not null && await HasChangedAsync(
                settings, SettingKeys.EmbeddingProvider, SettingDefaults.EmbeddingProvider,
                request.EmbeddingProvider.ToLowerInvariant(), ct))
        {
            return true;
        }

        if (request.OllamaBaseUrl is not null && await HasChangedAsync(
                settings, SettingKeys.EmbeddingOllamaBaseUrl, SettingDefaults.EmbeddingOllamaBaseUrl,
                request.OllamaBaseUrl.Trim(), ct))
        {
            return true;
        }

        if (request.OllamaModel is not null && await HasChangedAsync(
                settings, SettingKeys.EmbeddingOllamaModel, SettingDefaults.EmbeddingOllamaModel,
                request.OllamaModel.Trim(), ct))
        {
            return true;
        }

        if (request.OpenAiBaseUrl is not null && await HasChangedAsync(
                settings, SettingKeys.EmbeddingOpenAiBaseUrl, SettingDefaults.EmbeddingOpenAiBaseUrl,
                request.OpenAiBaseUrl.Trim(), ct))
        {
            return true;
        }

        return request.QdrantEndpoint is not null && await HasChangedAsync(
            settings, SettingKeys.QdrantEndpoint, SettingDefaults.QdrantEndpoint,
            request.QdrantEndpoint.Trim(), ct);
    }

    private static async Task<bool> HasChangedAsync(
        AppSettingsService settings, string key, string defaultValue, string requestedValue, CancellationToken ct) =>
        !string.Equals(
            await settings.GetOrDefaultAsync(key, defaultValue, ct),
            requestedValue,
            StringComparison.Ordinal);

    private static IResult InvalidSetting(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static bool IsValidHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
