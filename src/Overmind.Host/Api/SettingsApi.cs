using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Configuration;

namespace Overmind.Host.Api;

public static class SettingsApi
{
    public static RouteGroupBuilder MapSettingsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapGet("/settings", GetSettings);
        group.MapPut("/settings", UpdateSettings);
        return group;
    }

    private static async Task<IResult> GetSettings(AppSettingsService settings, CancellationToken ct)
    {
        var provider = await settings.GetOrDefaultAsync(
            SettingKeys.EmbeddingProvider, SettingDefaults.EmbeddingProvider, ct);
        var ollamaBaseUrl = await settings.GetOrDefaultAsync(
            SettingKeys.EmbeddingOllamaBaseUrl, SettingDefaults.EmbeddingOllamaBaseUrl, ct);
        var ollamaModel = await settings.GetOrDefaultAsync(
            SettingKeys.EmbeddingOllamaModel, SettingDefaults.EmbeddingOllamaModel, ct);
        var openAiBaseUrl = await settings.GetOrDefaultAsync(
            SettingKeys.EmbeddingOpenAiBaseUrl, SettingDefaults.EmbeddingOpenAiBaseUrl, ct);
        var apiKeyConfigured = await settings.HasAsync(SettingKeys.EmbeddingOpenAiApiKey, ct);
        var planningMaterializationDefault = await settings.GetOrDefaultAsync(
            SettingKeys.PlanningMaterializationDefault, SettingDefaults.PlanningMaterializationDefault, ct);
        var planningRequireHumanValidationDefault = await settings.GetOrDefaultAsync(
            SettingKeys.PlanningRequireHumanValidationDefault, SettingDefaults.PlanningRequireHumanValidationDefault, ct);

        return Results.Ok(new SettingsResponse(
            provider, ollamaBaseUrl, ollamaModel, openAiBaseUrl, apiKeyConfigured,
            planningMaterializationDefault, planningRequireHumanValidationDefault));
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

        return await GetSettings(settings, ct);
    }

    private static IResult InvalidSetting(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static bool IsValidHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
