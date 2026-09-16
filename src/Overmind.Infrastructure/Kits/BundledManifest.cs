using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Overmind.Infrastructure.Kits;

public sealed record BundledManifest(int BundleVersion, IReadOnlyList<string> Kits)
{
    public static bool TryLoad(string path, [NotNullWhen(true)] out BundledManifest? manifest)
    {
        manifest = null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return TryRead(document.RootElement, out manifest);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static bool TryRead(JsonElement root, out BundledManifest? manifest)
    {
        manifest = null;
        if (root.ValueKind is not JsonValueKind.Object
            || !root.TryGetProperty("bundleVersion", out var versionElement)
            || versionElement.ValueKind is not JsonValueKind.Number
            || !versionElement.TryGetInt32(out var bundleVersion)
            || bundleVersion < 1)
        {
            return false;
        }

        if (!root.TryGetProperty("kits", out var kitsElement)
            || kitsElement.ValueKind is not JsonValueKind.Array)
        {
            return false;
        }

        var kits = new List<string>();
        foreach (var kitElement in kitsElement.EnumerateArray())
        {
            if (kitElement.ValueKind is not JsonValueKind.String)
            {
                return false;
            }

            var kit = kitElement.GetString()!.Trim();
            if (kit.Length == 0)
            {
                return false;
            }

            kits.Add(kit);
        }

        manifest = new BundledManifest(bundleVersion, kits);
        return true;
    }
}
