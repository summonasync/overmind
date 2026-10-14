using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

internal static class EntryMirrorNaming
{
    public static string Directory(EntryType type) => type switch
    {
        EntryType.FeatureRequest => "feature-request",
        _ => type.ToString().ToLowerInvariant()
    };

    public static string Wire(EntryType type) => type switch
    {
        EntryType.FeatureRequest => "featureRequest",
        _ => type.ToString().ToLowerInvariant()
    };
}
