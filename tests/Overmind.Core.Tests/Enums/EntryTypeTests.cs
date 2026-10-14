using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Enums;

public class EntryTypeTests
{
    [Fact]
    public void Members_StayAppendOnlyWithSerialValues()
    {
        Enum.GetNames<EntryType>().ShouldBe(
            ["Note", "Decision", "Gate", "Guard", "Skill", "Tombstone", "FeatureRequest"]);
        Enum.GetValues<EntryType>().Select(type => (int)type).ShouldBe([0, 1, 2, 3, 4, 5, 6]);
    }
}
