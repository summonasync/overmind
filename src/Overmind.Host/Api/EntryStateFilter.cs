using Overmind.Core;

namespace Overmind.Host.Api;

public static class EntryStateFilter
{
    public const string All = "all";

    private static readonly string[] Known =
    [
        ContextEntry.ActiveState,
        ContextEntry.LiftedState,
        ContextEntry.SupersededState,
        All
    ];

    public static IResult? Validate(string? requested, string? type, out string state)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            state = DefaultsToActive(type) ? ContextEntry.ActiveState : All;
            return null;
        }

        var candidate = Known.FirstOrDefault(value =>
            value.Equals(requested.Trim(), StringComparison.OrdinalIgnoreCase));

        if (candidate is null)
        {
            state = All;
            return HttpErrors.BadRequest(
                $"Unknown state filter '{requested}'. Valid values: {string.Join(", ", Known)}.");
        }

        state = candidate;
        return null;
    }

    private static bool DefaultsToActive(string? type) =>
        Enum.TryParse<EntryType>(type, ignoreCase: true, out var parsed)
        && parsed is EntryType.Decision or EntryType.Gate or EntryType.Guard;
}
