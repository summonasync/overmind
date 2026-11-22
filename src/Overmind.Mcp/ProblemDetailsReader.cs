using System.Text.Json;

namespace Overmind.Mcp;

internal static class ProblemDetailsReader
{
    public static async Task<ProblemDetails?> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var payload = await response.Content.ReadAsStringAsync(ct);
            if (payload.Length == 0)
            {
                return null;
            }

            using var document = JsonDocument.Parse(payload);
            return document.RootElement.ValueKind == JsonValueKind.Object ? Map(document.RootElement) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string FormatErrors(ProblemDetails problem) => string.Join(
        "; ",
        problem.Errors
            .OrderBy(field => field.Key, StringComparer.Ordinal)
            .SelectMany(field => field.Value.Select(message => $"{field.Key}: {message}")));

    private static ProblemDetails Map(JsonElement root) => new(
        TextOrNull(root, "title"),
        TextOrNull(root, "detail"),
        TextOrNull(root, "fix"),
        Errors(root));

    private static string? TextOrNull(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Errors(JsonElement root)
    {
        var errors = new Dictionary<string, IReadOnlyList<string>>();
        if (!root.TryGetProperty("errors", out var fields) || fields.ValueKind != JsonValueKind.Object)
        {
            return errors;
        }

        foreach (var field in fields.EnumerateObject())
        {
            List<string> messages = [];
            if (field.Value.ValueKind == JsonValueKind.Array)
            {
                messages = [.. field.Value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()!)];
            }

            if (messages.Count > 0)
            {
                errors[field.Name] = messages;
            }
        }

        return errors;
    }
}
