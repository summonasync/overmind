using System.Text.Json;
using Overmind.Core;

namespace Overmind.Infrastructure.Materialization;

public sealed class PlatformDirectoryMap
{
    private const string ResourceName = "Overmind.Infrastructure.Materialization.platforms.json";

    private readonly IReadOnlyDictionary<AgentPlatform, string> _skillsDirs;

    private PlatformDirectoryMap(IReadOnlyDictionary<AgentPlatform, string> skillsDirs)
    {
        _skillsDirs = skillsDirs;
    }

    public string GetSkillsDir(AgentPlatform platform) => _skillsDirs[platform];

    public static PlatformDirectoryMap Load()
    {
        var assembly = typeof(PlatformDirectoryMap).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");

        Dictionary<string, JsonElement> raw;
        try
        {
            raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stream)
                ?? throw new InvalidOperationException($"Embedded '{ResourceName}' is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Embedded '{ResourceName}' is not valid JSON: {ex.Message}", ex);
        }

        var map = new Dictionary<AgentPlatform, string>();
        foreach (var platform in Enum.GetValues<AgentPlatform>())
        {
            var key = JsonNamingPolicy.CamelCase.ConvertName(platform.ToString());
            if (!raw.TryGetValue(key, out var node) ||
                node.ValueKind != JsonValueKind.Object ||
                !node.TryGetProperty("skillsDir", out var dir) ||
                dir.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(dir.GetString()))
            {
                throw new InvalidOperationException(
                    $"Embedded '{ResourceName}' must define a non-empty 'skillsDir' for platform '{key}'.");
            }

            map[platform] = dir.GetString()!;
        }

        return new PlatformDirectoryMap(map);
    }
}
