using Overmind.Core;

namespace Overmind.Host.Api;

// Door-side adapter for the soft-cap engine: cap the payload, feed the warning into the
// response's warnings sink, hand the storable value to the service. The arithmetic lives in
// SoftCap only — doors name the limit, never derive band numbers.
internal static class SoftCapSoften
{
    public static string? Cap(string? value, int limit, List<object> warnings, bool singleLine = false)
    {
        var outcome = SoftCap.Apply(value, limit, singleLine);
        if (outcome.Warning is { } warning)
        {
            warnings.Add(warning);
        }

        return outcome.Stored;
    }

    public static IReadOnlyList<object>? Merge(List<object> warnings, IReadOnlyList<string>? domainWarnings = null)
    {
        if (warnings.Count == 0 && domainWarnings is not { Count: > 0 })
        {
            return null;
        }

        var merged = new List<object>(warnings);
        if (domainWarnings is { Count: > 0 })
        {
            merged.AddRange(domainWarnings.Cast<object>());
        }

        return merged;
    }
}
