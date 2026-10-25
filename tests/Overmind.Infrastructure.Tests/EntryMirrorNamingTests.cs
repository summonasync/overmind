using Overmind.Core;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class EntryMirrorNamingTests
{
    [Theory]
    [InlineData(EntryType.Note, "note")]
    [InlineData(EntryType.Decision, "decision")]
    [InlineData(EntryType.Gate, "gate")]
    [InlineData(EntryType.Guard, "guard")]
    [InlineData(EntryType.Skill, "skill")]
    [InlineData(EntryType.Tombstone, "tombstone")]
    [InlineData(EntryType.Idea, "idea")]
    [InlineData(EntryType.Audit, "audit")]
    public void Directory_MapsEveryTypeToItsMirrorFolder(EntryType type, string expected) =>
        EntryMirrorNaming.Directory(type).ShouldBe(expected);

    [Theory]
    [InlineData(EntryType.Note, "note")]
    [InlineData(EntryType.Decision, "decision")]
    [InlineData(EntryType.Gate, "gate")]
    [InlineData(EntryType.Guard, "guard")]
    [InlineData(EntryType.Skill, "skill")]
    [InlineData(EntryType.Tombstone, "tombstone")]
    [InlineData(EntryType.Idea, "idea")]
    [InlineData(EntryType.Audit, "audit")]
    public void Wire_MapsEveryTypeToItsWireToken(EntryType type, string expected) =>
        EntryMirrorNaming.Wire(type).ShouldBe(expected);
}
