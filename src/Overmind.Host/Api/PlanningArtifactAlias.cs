using Overmind.Core;

namespace Overmind.Host.Api;

public sealed record PlanningArtifactAlias(PlanningArtifactKind Kind, int? Order)
{
    public static bool TryParse(string suffix, out PlanningArtifactAlias parsed)
    {
        parsed = new PlanningArtifactAlias(PlanningArtifactKind.Task, null);
        if (suffix.Length == 1)
        {
            var kind = char.ToUpperInvariant(suffix[0]) switch
            {
                'S' => PlanningArtifactKind.Spec,
                'P' => PlanningArtifactKind.Plan,
                'R' => PlanningArtifactKind.Result,
                _ => (PlanningArtifactKind?)null,
            };

            if (kind is not { } resolved)
            {
                return false;
            }

            parsed = new PlanningArtifactAlias(resolved, null);
            return true;
        }

        if (suffix[0] is 't' or 'T' && TryParseTaskOrder(suffix[1..]) is { } order)
        {
            parsed = new PlanningArtifactAlias(PlanningArtifactKind.Task, order);
            return true;
        }

        return false;
    }

    private static int? TryParseTaskOrder(string digits) =>
        digits.Length > 0 && digits.All(char.IsAsciiDigit) && int.TryParse(digits, out var order)
            ? (int?)order
            : null;
}
