namespace Overmind.Infrastructure.Planning;

public static class FeatureNumberGuard
{
    public static FeatureNumberCollision? FindCollision(IReadOnlyList<FeatureNumberEntry> features, string slug)
    {
        if (!FeatureNumberPrefix.TryParse(slug, out var requested))
        {
            return null;
        }

        FeatureNumberEntry? colliding = null;
        var max = 0;
        foreach (var feature in features)
        {
            if (!FeatureNumberPrefix.TryParse(feature.Slug, out var number))
            {
                continue;
            }

            if (number == requested)
            {
                colliding ??= feature;
            }

            max = Math.Max(max, number);
        }

        var next = max == int.MaxValue ? max : max + 1;

        return colliding is null ? null : new FeatureNumberCollision(colliding.Slug, colliding.Status, next);
    }
}
