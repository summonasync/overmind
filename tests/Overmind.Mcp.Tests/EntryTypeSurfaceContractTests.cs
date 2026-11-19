using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

// The allowed-type set lived as prose in four places with no mechanical link to EntryType, so feature 238 was on
// track to add a fifth copy. Overmind.Core is reachable from this test project but not from the adapter — the
// adapter references nothing by design — so the bridge is an assertion: what the adapter declares must equal what
// the enum says, and nobody can satisfy it by editing one side.
public sealed class EntryTypeSurfaceContractTests
{
    [Fact]
    public void AdapterWritableSet_IsExactlyTheCoreWritableSet()
    {
        Sorted(OvermindTools.AgentWritableTypes)
            .ShouldBe(Sorted(EntryTypeSurface.AgentWritable.Select(EntryTypeSurface.Normalize)));
    }

    [Fact]
    public void AdapterReadableSet_CoversEveryEntryTypeMember()
    {
        Sorted(OvermindTools.ReadableTypes)
            .ShouldBe(Sorted(Enum.GetValues<EntryType>().Select(EntryTypeSurface.Normalize)));
    }

    [Fact]
    public void TheWritableMessageNamesTheSetTheCoreSurfaceDeclares()
    {
        // The refusal text an agent reads is built from the same string the validators consult; if the two drift,
        // the door names types it does not accept.
        var messageTypes = OvermindTools.AgentWritableTypes.Select(t => t.ToLowerInvariant()).ToArray();

        string.Join(", ", messageTypes).ShouldBe(EntryTypeSurface.AgentWritableNames);
    }

    private static string[] Sorted(IEnumerable<string> names) =>
        [.. names.Select(name => name.ToLowerInvariant()).OrderBy(name => name, StringComparer.Ordinal)];

    private static string[] Sorted(IEnumerable<EntryType> types) => Sorted(types.Select(EntryTypeSurface.Normalize));
}
