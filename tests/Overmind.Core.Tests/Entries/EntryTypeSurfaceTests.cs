using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entries;

public sealed class EntryTypeSurfaceTests
{
    // The two types the agent door never writes, and why: journal is the owner's personal surface (feature 184 R6),
    // skill entries carry skill files and a kit lifecycle the UI owns. A new EntryType member lands in neither set,
    // so this test fails until whoever adds it decides which side it belongs to — that decision is the point.
    private static readonly EntryType[] AgentGatedTypes = [EntryType.Skill, EntryType.Journal];

    [Fact]
    public void EveryEntryTypeIsClassifiedForTheAgentDoor()
    {
        var classified = EntryTypeSurface.AgentWritable.Concat(AgentGatedTypes).ToArray();

        classified.ShouldContain(EntryType.Journal);
        classified.ShouldContain(EntryType.Skill);
        Enum.GetValues<EntryType>().Length.ShouldBe(classified.Length);
        classified.Distinct().Count().ShouldBe(classified.Length);

        foreach (var type in Enum.GetValues<EntryType>())
        {
            classified.ShouldContain(type);
        }
    }

    [Fact]
    public void WritableNamesMatchTheWritableTypesAndStayTheDoorMessage()
    {
        EntryTypeSurface.AgentWritableNames
            .ShouldBe("note, decision, gate, guard, idea, audit, tombstone");

        EntryTypeSurface.AgentWritableNames
            .ShouldBe(string.Join(", ", EntryTypeSurface.AgentWritable.Select(EntryTypeSurface.Normalize)));

        EntryTypeSurface.AgentWritable.ShouldNotContain(EntryType.Journal);
    }

    [Fact]
    public void AllNamesCoverEveryMemberInLowercase()
    {
        EntryTypeSurface.AllNames.Length.ShouldBe(Enum.GetValues<EntryType>().Length);
        EntryTypeSurface.AllNames.ShouldContain("journal");
        EntryTypeSurface.AllNames.ShouldContain("tombstone");
        EntryTypeSurface.AllNames.ShouldNotContain("Journal");
    }
}
