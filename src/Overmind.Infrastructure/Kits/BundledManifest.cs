using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Overmind.Infrastructure.Kits;

public sealed record BundledManifest(
    int BundleVersion,
    IReadOnlyList<BundledKit> Kits,
    IReadOnlyList<string> Legacy)
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

        var kits = new List<BundledKit>();
        foreach (var kitElement in kitsElement.EnumerateArray())
        {
            if (!TryReadKit(kitElement, out var kit))
            {
                return false;
            }

            kits.Add(kit);
        }

        IReadOnlyList<string> legacy = [];
        if (root.TryGetProperty("legacy", out var legacyElement))
        {
            if (legacyElement.ValueKind is not JsonValueKind.Array)
            {
                return false;
            }

            var names = new List<string>();
            foreach (var nameElement in legacyElement.EnumerateArray())
            {
                var name = nameElement.ValueKind == JsonValueKind.String
                    ? nameElement.GetString()!.Trim()
                    : string.Empty;
                if (name.Length == 0)
                {
                    return false;
                }

                names.Add(name);
            }

            legacy = names;
        }

        manifest = new BundledManifest(bundleVersion, kits, legacy);
        return true;
    }

    private static bool TryReadKit(JsonElement element, out BundledKit kit)
    {
        kit = null!;
        if (element.ValueKind is JsonValueKind.String)
        {
            var legacyName = element.GetString()!.Trim();
            if (legacyName.Length == 0)
            {
                return false;
            }

            kit = new BundledKit(legacyName, false);
            return true;
        }

        if (element.ValueKind is not JsonValueKind.Object
            || !element.TryGetProperty("name", out var nameElement)
            || nameElement.ValueKind is not JsonValueKind.String)
        {
            return false;
        }

        var name = nameElement.GetString()!.Trim();
        if (name.Length == 0)
        {
            return false;
        }

        var mandatory = false;
        if (element.TryGetProperty("mandatory", out var mandatoryElement))
        {
            if (mandatoryElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return false;
            }

            mandatory = mandatoryElement.GetBoolean();
        }

        kit = new BundledKit(name, mandatory);
        return true;
    }
}
