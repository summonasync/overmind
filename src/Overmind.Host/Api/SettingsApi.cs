using System.Globalization;
using System.Text.Json;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Telegram;
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
        group.MapGet("/settings/usage-pricing", GetUsagePricing);
        group.MapPut("/settings/usage-pricing", UpdateUsagePricing);
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

        if (request.PlanningAgentNamingRulesDefault is not null &&
            request.PlanningAgentNamingRulesDefault.Trim().Length > Project.MaxNamingRulesLength)
        {
            return InvalidSetting(
                nameof(request.PlanningAgentNamingRulesDefault),
                $"Must be at most {Project.MaxNamingRulesLength} characters.");
        }

        if (request.PlanningMaterializationSync is not null &&
            !SettingDefaults.PlanningMaterializationSyncModes.Contains(
                request.PlanningMaterializationSync, StringComparer.OrdinalIgnoreCase))
        {
            return InvalidSetting(
                nameof(request.PlanningMaterializationSync),
                $"Must be one of: {string.Join(", ", SettingDefaults.PlanningMaterializationSyncModes)}.");
        }

        if (request.NotificationNudgeMode is not null &&
            !SettingDefaults.NotificationNudgeModes.Contains(
                request.NotificationNudgeMode, StringComparer.OrdinalIgnoreCase))
        {
            return InvalidSetting(
                nameof(request.NotificationNudgeMode),
                $"Must be one of: {string.Join(", ", SettingDefaults.NotificationNudgeModes)}.");
        }

        if (request.NotificationNudgeIntervalMinutes is < 1)
        {
            return InvalidSetting(
                nameof(request.NotificationNudgeIntervalMinutes),
                "Must be greater than 0.");
        }

        if (request.AgentBaseUrl is not null && !IsValidHttpUrl(request.AgentBaseUrl))
        {
            return InvalidSetting(nameof(request.AgentBaseUrl), "Must be a valid http(s) URL.");
        }

        if (request.AgentModel is not null)
        {
            var trimmedAgentModel = request.AgentModel.Trim();
            if (trimmedAgentModel.Length == 0 || trimmedAgentModel.Length > 100)
            {
                return InvalidSetting(nameof(request.AgentModel), "Must be between 1 and 100 characters.");
            }
        }

        if (request.AgentPerTurnMaxTokens is < 1)
        {
            return InvalidSetting(nameof(request.AgentPerTurnMaxTokens), "Must be greater than 0.");
        }

        if (request.AgentRouteMatchThreshold is <= 0 or >= 1)
        {
            return InvalidSetting(
                nameof(request.AgentRouteMatchThreshold), "Must be between 0 and 1 (exclusive).");
        }

        if (request.AgentDailyBudget is < 0)
        {
            return InvalidSetting(nameof(request.AgentDailyBudget), "Must be 0 or greater.");
        }

        if (request.AgentGrimoireBaseUrl is not null &&
            request.AgentGrimoireBaseUrl.Trim().Length > 0 &&
            !IsValidHttpUrl(request.AgentGrimoireBaseUrl))
        {
            return InvalidSetting(
                nameof(request.AgentGrimoireBaseUrl),
                "Must be empty (auto-detect via the grimoire integration) or a valid http(s) URL.");
        }

        if (request.AgentGrimoireTome is not null)
        {
            var trimmedTome = request.AgentGrimoireTome.Trim();
            if (trimmedTome.Length == 0 || trimmedTome.Length > 64)
            {
                return InvalidSetting(nameof(request.AgentGrimoireTome), "Must be between 1 and 64 characters.");
            }
        }

        if (request.AgentGrimoireCompactionHours is < 0)
        {
            return InvalidSetting(nameof(request.AgentGrimoireCompactionHours), "Must be 0 or greater.");
        }

        if (request.AgentInstructions is not null &&
            request.AgentInstructions.Length > SettingDefaults.AgentInstructionsMaxLength)
        {
            return InvalidSetting(
                nameof(request.AgentInstructions),
                $"Must be at most {SettingDefaults.AgentInstructionsMaxLength} characters.");
        }

        if (request.AgentDelegateApprovalsDefault is not null &&
            !SettingDefaults.AgentDelegateApprovalsDefaults.Contains(
                request.AgentDelegateApprovalsDefault, StringComparer.OrdinalIgnoreCase))
        {
            return InvalidSetting(
                nameof(request.AgentDelegateApprovalsDefault),
                $"Must be one of: {string.Join(", ", SettingDefaults.AgentDelegateApprovalsDefaults)}.");
        }

        if (request.TelegramAllowedChatIds is { Length: > 100 })
        {
            return InvalidSetting(
                nameof(request.TelegramAllowedChatIds),
                "Must contain at most 100 chat ids.");
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

        if (request.PlanningAgentNamingRulesDefault is not null)
        {
            await settings.SetAsync(
                SettingKeys.PlanningAgentNamingRulesDefault,
                request.PlanningAgentNamingRulesDefault.Trim(), ct);
        }

        if (request.PlanningMaterializationSync is not null)
        {
            await settings.SetAsync(
                SettingKeys.PlanningMaterializationSync,
                request.PlanningMaterializationSync.ToLowerInvariant(), ct);
        }

        if (request.NotificationNudgeMode is not null)
        {
            await settings.SetAsync(
                SettingKeys.NotificationNudgeMode,
                request.NotificationNudgeMode.ToLowerInvariant(), ct);
        }

        if (request.NotificationNudgeIntervalMinutes is not null)
        {
            await settings.SetAsync(
                SettingKeys.NotificationNudgeIntervalMinutes,
                request.NotificationNudgeIntervalMinutes.Value.ToString(CultureInfo.InvariantCulture), ct);
        }

        if (request.AgentBaseUrl is not null)
        {
            await settings.SetAsync(SettingKeys.AgentBaseUrl, request.AgentBaseUrl.Trim(), ct);
        }

        if (request.AgentModel is not null)
        {
            await settings.SetAsync(SettingKeys.AgentModel, request.AgentModel.Trim(), ct);
        }

        if (!string.IsNullOrEmpty(request.AgentApiKey))
        {
            await settings.SetAsync(SettingKeys.AgentApiKey, request.AgentApiKey, ct);
        }

        if (request.AgentAlias is not null)
        {
            var trimmedAlias = request.AgentAlias.Trim();
            if (trimmedAlias.Length == 0 || trimmedAlias.Length > AgentSession.MaxIdentityLength)
            {
                return InvalidSetting(
                    nameof(request.AgentAlias),
                    $"Must be between 1 and {AgentSession.MaxIdentityLength} characters.");
            }

            await settings.SetAsync(SettingKeys.AgentAlias, trimmedAlias, ct);
        }

        if (request.AgentPerTurnMaxTokens is not null)
        {
            await settings.SetAsync(
                SettingKeys.AgentPerTurnMaxTokens,
                request.AgentPerTurnMaxTokens.Value.ToString(CultureInfo.InvariantCulture), ct);
        }

        if (request.AgentRouteMatchThreshold is not null)
        {
            await settings.SetAsync(
                SettingKeys.AgentRouteMatchThreshold,
                request.AgentRouteMatchThreshold.Value.ToString(CultureInfo.InvariantCulture), ct);
        }

        if (request.AgentDailyBudget is not null)
        {
            await settings.SetAsync(
                SettingKeys.AgentDailyBudget,
                request.AgentDailyBudget.Value.ToString(CultureInfo.InvariantCulture), ct);
        }

        if (request.AgentGrimoireBaseUrl is not null)
        {
            await settings.SetAsync(SettingKeys.AgentGrimoireBaseUrl, request.AgentGrimoireBaseUrl.Trim(), ct);
        }

        if (request.AgentGrimoireTome is not null)
        {
            await settings.SetAsync(SettingKeys.AgentGrimoireTome, request.AgentGrimoireTome.Trim(), ct);
        }

        if (request.AgentGrimoireCompactionHours is not null)
        {
            await settings.SetAsync(
                SettingKeys.AgentGrimoireCompactionHours,
                request.AgentGrimoireCompactionHours.Value.ToString(CultureInfo.InvariantCulture), ct);
        }

        if (request.AgentInstructions is not null)
        {
            await settings.SetAsync(SettingKeys.AgentInstructions, request.AgentInstructions, ct);
        }

        if (request.AgentDelegateApprovalsDefault is not null)
        {
            await settings.SetAsync(
                SettingKeys.AgentDelegateApprovalsDefault,
                request.AgentDelegateApprovalsDefault.ToLowerInvariant(), ct);
        }

        if (request.TelegramEnabled is not null)
        {
            await settings.SetAsync(
                SettingKeys.TelegramEnabled,
                request.TelegramEnabled.Value ? "true" : "false", ct);
        }

        if (!string.IsNullOrEmpty(request.TelegramBotToken))
        {
            await settings.SetAsync(SettingKeys.TelegramBotToken, request.TelegramBotToken, ct);
        }

        if (request.TelegramAllowedChatIds is not null)
        {
            await settings.SetAsync(
                SettingKeys.TelegramAllowedChatIds,
                string.Join(",", request.TelegramAllowedChatIds), ct);
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
            updated.PlanningAgentNamingRulesDefault,
            updated.PlanningMaterializationSync,
            updated.NotificationNudgeMode,
            updated.NotificationNudgeIntervalMinutes,
            updated.AgentBaseUrl,
            updated.AgentModel,
            updated.AgentApiKeyConfigured,
            updated.AgentAlias,
            updated.AgentPerTurnMaxTokens,
            updated.AgentDailyBudget,
            updated.AgentRouteMatchThreshold,
            updated.AgentGrimoireBaseUrl,
            updated.AgentGrimoireTome,
            updated.AgentGrimoireCompactionHours,
            updated.AgentInstructions,
            updated.AgentDelegateApprovalsDefault,
            reindexRecommended,
            updated.TelegramEnabled,
            updated.TelegramBotTokenConfigured,
            updated.TelegramAllowedChatIds));
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

    private static async Task<IResult> GetUsagePricing(
        UsagePricingCatalog catalog, CancellationToken ct)
    {
        var pricing = await catalog.LoadAsync(ct);
        return Results.Ok(new UsagePricingResponse(ToPricingEntries(pricing)));
    }

    private static async Task<IResult> UpdateUsagePricing(
        UpdateUsagePricingRequest request,
        UsagePricingCatalog catalog,
        AppSettingsService settings,
        CancellationToken ct)
    {
        foreach (var (model, price) in request.Pricing)
        {
            var name = model.Trim();
            if (name.Length == 0 || name.Length > UsageRecord.MaxModelLength)
            {
                return InvalidSetting(
                    nameof(request.Pricing),
                    $"Model names must be between 1 and {UsageRecord.MaxModelLength} characters; got '{model}'.");
            }

            if (price is null)
            {
                return InvalidSetting(nameof(request.Pricing), $"Pricing for '{model}' is required.");
            }

            if (price.Input < 0 || price.Output < 0 || price.CacheRead < 0 || price.CacheWrite < 0)
            {
                return InvalidSetting(
                    nameof(request.Pricing), $"Prices for '{model}' must not be negative.");
            }
        }

        var payload = request.Pricing.ToDictionary(
            pair => pair.Key.Trim(), pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        await settings.SetAsync(
            SettingKeys.UsagePricing,
            JsonSerializer.Serialize(payload, UsagePricingCatalog.SerializerOptions), ct);

        var stored = await catalog.LoadAsync(ct);
        return Results.Ok(new UsagePricingResponse(ToPricingEntries(stored)));
    }

    private static IReadOnlyDictionary<string, UsagePricingEntry> ToPricingEntries(
        IReadOnlyDictionary<string, UsagePricing> pricing) => pricing.ToDictionary(
        pair => pair.Key,
        pair => new UsagePricingEntry(
            pair.Value.Input, pair.Value.Output, pair.Value.CacheRead, pair.Value.CacheWrite));

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
        var planningAgentNamingRulesDefault = await settings.GetOrDefaultAsync(
            SettingKeys.PlanningAgentNamingRulesDefault, SettingDefaults.PlanningAgentNamingRulesDefault, ct);
        var planningMaterializationSync = await settings.GetOrDefaultAsync(
            SettingKeys.PlanningMaterializationSync, SettingDefaults.PlanningMaterializationSync, ct);
        var notificationNudgeMode = await settings.GetOrDefaultAsync(
            SettingKeys.NotificationNudgeMode, SettingDefaults.NotificationNudgeMode, ct);
        var notificationNudgeIntervalRaw = await settings.GetOrDefaultAsync(
            SettingKeys.NotificationNudgeIntervalMinutes,
            SettingDefaults.NotificationNudgeIntervalMinutes.ToString(CultureInfo.InvariantCulture), ct);
        var notificationNudgeIntervalMinutes =
            int.TryParse(notificationNudgeIntervalRaw, out var nudgeInterval) && nudgeInterval > 0
                ? nudgeInterval
                : SettingDefaults.NotificationNudgeIntervalMinutes;
        var agentBaseUrl = await settings.GetOrDefaultAsync(SettingKeys.AgentBaseUrl, SettingDefaults.AgentBaseUrl, ct);
        var agentModel = await settings.GetOrDefaultAsync(SettingKeys.AgentModel, SettingDefaults.AgentModel, ct);
        var agentApiKeyConfigured = await settings.HasAsync(SettingKeys.AgentApiKey, ct);
        var agentPerTurnMaxTokensRaw = await settings.GetOrDefaultAsync(
            SettingKeys.AgentPerTurnMaxTokens,
            SettingDefaults.AgentPerTurnMaxTokens.ToString(CultureInfo.InvariantCulture), ct);
        var agentPerTurnMaxTokens =
            int.TryParse(agentPerTurnMaxTokensRaw, out var perTurnMaxTokens) && perTurnMaxTokens > 0
                ? perTurnMaxTokens
                : SettingDefaults.AgentPerTurnMaxTokens;
        var agentDailyBudgetRaw = await settings.GetOrDefaultAsync(
            SettingKeys.AgentDailyBudget,
            SettingDefaults.AgentDailyBudget.ToString(CultureInfo.InvariantCulture), ct);
        var agentDailyBudget =
            int.TryParse(agentDailyBudgetRaw, out var dailyBudget) && dailyBudget >= 0
                ? dailyBudget
                : SettingDefaults.AgentDailyBudget;
        var agentRouteMatchThresholdRaw = await settings.GetOrDefaultAsync(
            SettingKeys.AgentRouteMatchThreshold,
            SettingDefaults.AgentRouteMatchThreshold.ToString(CultureInfo.InvariantCulture), ct);
        var agentRouteMatchThreshold =
            double.TryParse(agentRouteMatchThresholdRaw, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var routeMatchThreshold) && routeMatchThreshold is > 0 and < 1
                ? routeMatchThreshold
                : SettingDefaults.AgentRouteMatchThreshold;
        var agentAlias = await settings.GetOrDefaultAsync(SettingKeys.AgentAlias, SettingDefaults.AgentAlias, ct);
        var agentGrimoireBaseUrl = await settings.GetOrDefaultAsync(
            SettingKeys.AgentGrimoireBaseUrl, SettingDefaults.AgentGrimoireBaseUrl, ct);
        var agentGrimoireTome = await settings.GetOrDefaultAsync(
            SettingKeys.AgentGrimoireTome, SettingDefaults.AgentGrimoireTome, ct);
        var agentGrimoireCompactionHoursRaw = await settings.GetOrDefaultAsync(
            SettingKeys.AgentGrimoireCompactionHours,
            SettingDefaults.AgentGrimoireCompactionHours.ToString(CultureInfo.InvariantCulture), ct);
        var agentGrimoireCompactionHours =
            int.TryParse(agentGrimoireCompactionHoursRaw, out var compactionHours) && compactionHours >= 0
                ? compactionHours
                : SettingDefaults.AgentGrimoireCompactionHours;
        var agentInstructions = await settings.GetOrDefaultAsync(
            SettingKeys.AgentInstructions, SettingDefaults.AgentInstructions, ct);
        var agentDelegateApprovalsDefault = await settings.GetOrDefaultAsync(
            SettingKeys.AgentDelegateApprovalsDefault, SettingDefaults.AgentDelegateApprovalsDefault, ct);
        var telegramEnabledRaw = await settings.GetOrDefaultAsync(
            SettingKeys.TelegramEnabled, SettingDefaults.TelegramEnabled, ct);
        var telegramBotTokenConfigured = await settings.HasAsync(SettingKeys.TelegramBotToken, ct);
        var telegramAllowedChatIdsRaw = await settings.GetOrDefaultAsync(
            SettingKeys.TelegramAllowedChatIds, SettingDefaults.TelegramAllowedChatIds, ct);

        return new SettingsResponse(
            provider, ollamaBaseUrl, ollamaModel, openAiBaseUrl, qdrantEndpoint, apiKeyConfigured,
            planningMaterializationDefault, planningRequireHumanValidationDefault, planningAgentNamingRulesDefault,
            planningMaterializationSync, notificationNudgeMode, notificationNudgeIntervalMinutes,
            agentBaseUrl, agentModel, agentApiKeyConfigured, agentAlias, agentPerTurnMaxTokens, agentDailyBudget,
            agentRouteMatchThreshold,
            agentGrimoireBaseUrl, agentGrimoireTome, agentGrimoireCompactionHours, agentInstructions,
            agentDelegateApprovalsDefault,
            bool.TryParse(telegramEnabledRaw, out var telegramEnabled) && telegramEnabled,
            telegramBotTokenConfigured,
            [.. TelegramTransportOptionsProvider.ParseChatIds(telegramAllowedChatIdsRaw)]);
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
