using System.Text.Json;

namespace Overmind.Infrastructure.Configuration;

public sealed class UsagePricingCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static IReadOnlyDictionary<string, UsagePricing>? _defaults;

    private readonly AppSettingsService _settings;

    public UsagePricingCatalog(AppSettingsService settings)
    {
        _settings = settings;
    }

    public static IReadOnlyDictionary<string, UsagePricing> Defaults => _defaults ??= Parse(DefaultJson);

    public static JsonSerializerOptions SerializerOptions { get; } = JsonOptions;

    public async Task<IReadOnlyDictionary<string, UsagePricing>> LoadAsync(CancellationToken ct)
    {
        var json = await _settings.GetAsync(SettingKeys.UsagePricing, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Defaults;
        }

        try
        {
            return Parse(json);
        }
        catch (JsonException)
        {
            return Defaults;
        }
    }

    public static IReadOnlyDictionary<string, UsagePricing> Parse(string json) => new Dictionary<string, UsagePricing>(
        JsonSerializer.Deserialize<Dictionary<string, UsagePricing>>(json, JsonOptions) ?? [],
        StringComparer.OrdinalIgnoreCase);

    private const string DefaultJson = """
        {
          "claude-opus-5": { "input": 5.00, "output": 25.00, "cacheRead": 0.50, "cacheWrite": 1.25 },
          "claude-sonnet-5": { "input": 2.00, "output": 10.00, "cacheRead": 0.20, "cacheWrite": 0.50 },
          "claude-haiku-4-5": { "input": 1.00, "output": 5.00, "cacheRead": 0.10, "cacheWrite": 0.25 }
        }
        """;
}
