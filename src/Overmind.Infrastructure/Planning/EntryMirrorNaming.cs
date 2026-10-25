using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

internal static class EntryMirrorNaming
{
    public static string Directory(EntryType type) => type.ToString().ToLowerInvariant();

    public static string Wire(EntryType type) => type.ToString().ToLowerInvariant();
}
