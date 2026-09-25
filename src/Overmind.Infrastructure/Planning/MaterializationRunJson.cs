using System.Text.Json;
using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

internal static class MaterializationRunJson
{
    public static string Serialize(MaterializationRunReport run) =>
        JsonSerializer.Serialize(run);

    public static MaterializationRunReport? Deserialize(string? json)
    {
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MaterializationRunReport>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
