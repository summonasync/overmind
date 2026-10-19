using System.Text.Json;

namespace Overmind.Infrastructure.Grimoire;

public sealed class GrimoireConfigReader
{
    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".grimoire");

    private readonly string _filePath;

    public GrimoireConfigReader(string? directory = null)
    {
        _filePath = Path.Combine(directory ?? DefaultDirectory, "config.json");
    }

    public GrimoireRuntimeConfig Read()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new GrimoireRuntimeConfig(null);
            }

            using var document = JsonDocument.Parse(File.ReadAllText(_filePath));
            return new GrimoireRuntimeConfig(ReadPort(document.RootElement));
        }
        catch (Exception ex) when (ex is JsonException or IOException or ArgumentException)
        {
            return new GrimoireRuntimeConfig(null);
        }
    }

    private static int? ReadPort(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("serve", out var serve)
            || serve.ValueKind != JsonValueKind.Object
            || !serve.TryGetProperty("port", out var port)
            || port.ValueKind != JsonValueKind.Number
            || !port.TryGetInt32(out var value))
        {
            return null;
        }

        return value;
    }
}
