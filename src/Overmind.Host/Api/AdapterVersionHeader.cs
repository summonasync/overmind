namespace Overmind.Host.Api;

public static class AdapterVersionHeader
{
    public const string Name = "X-Overmind-Adapter-Version";

    public static string? Read(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(Name, out var values) || values.Count == 0)
        {
            return null;
        }

        var value = values[^1];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
