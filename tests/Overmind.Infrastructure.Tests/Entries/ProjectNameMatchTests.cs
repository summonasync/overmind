using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Entries;

public sealed class ProjectNameMatchTests
{
    private static readonly DateTime Older = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Newer = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid LowId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid HighId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Theory]
    [InlineData("journal", "Journal", true)]
    [InlineData("journal", "JOURNAL", false)]
    [InlineData("JOURNAL", "journal", false)]
    public void PickDeterministic_CaseVariantSpellings_ExactWinsOtherwiseTheOldestRow(
        string olderName, string newerName, bool exactIsTheNewerRow)
    {
        var olderId = Guid.NewGuid();
        var newerId = Guid.NewGuid();
        var rows = new List<(Guid Id, string Name, DateTime CreatedAt)>
        {
            (olderId, olderName, Older),
            (newerId, newerName, Newer),
        };

        ProjectNameMatch.PickDeterministic(rows, JournalProjectStore.ProjectName)
            .ShouldBe(exactIsTheNewerRow ? newerId : olderId);
    }

    [Fact]
    public void PickDeterministic_ExactSpellingBeatsAnOlderVariant()
    {
        var variantId = Guid.NewGuid();
        var exactId = Guid.NewGuid();
        var rows = new List<(Guid Id, string Name, DateTime CreatedAt)>
        {
            (variantId, "JOURNAL", Older),
            (exactId, JournalProjectStore.ProjectName, Newer),
        };

        ProjectNameMatch.PickDeterministic(rows, JournalProjectStore.ProjectName).ShouldBe(exactId);
    }

    [Fact]
    public void PickDeterministic_VariantsSharingCreatedAt_TheLowestIdWinsRegardlessOfRowOrder()
    {
        var rows = new List<(Guid Id, string Name, DateTime CreatedAt)>
        {
            (HighId, "JOURNAL", Older),
            (LowId, "journal", Older),
        };

        ProjectNameMatch.PickDeterministic(rows, JournalProjectStore.ProjectName).ShouldBe(LowId);
    }

    [Fact]
    public void PickDeterministic_NothingMatchesTheName_ReturnsNull()
    {
        var rows = new List<(Guid Id, string Name, DateTime CreatedAt)>
        {
            (Guid.NewGuid(), "grimoire", Older),
        };

        ProjectNameMatch.PickDeterministic(rows, JournalProjectStore.ProjectName).ShouldBeNull();
    }

    [Fact]
    public void PickDeterministic_AnEmptyRoster_ReturnsNull() =>
        ProjectNameMatch.PickDeterministic([], JournalProjectStore.ProjectName).ShouldBeNull();

    [Fact]
    public void PickStrict_CaseVariantTieWithoutTheExactSpelling_IsUnresolvable()
    {
        var rows = new List<(Guid Id, string Name, DateTime CreatedAt)>
        {
            (Guid.NewGuid(), "stream-other", Older),
            (Guid.NewGuid(), "Stream-Other", Newer),
        };

        ProjectNameMatch.PickStrict(rows, "STREAM-OTHER").ShouldBeNull();
    }

    [Fact]
    public void PickStrict_ExactSpellingWinsAmongVariants()
    {
        var variantId = Guid.NewGuid();
        var exactId = Guid.NewGuid();
        var rows = new List<(Guid Id, string Name, DateTime CreatedAt)>
        {
            (variantId, "Stream-Other", Older),
            (exactId, "stream-other", Newer),
        };

        ProjectNameMatch.PickStrict(rows, "stream-other").ShouldBe(exactId);
    }

    [Fact]
    public void PickStrict_ASingleCaseVariant_IsAdopted()
    {
        var variantId = Guid.NewGuid();
        var rows = new List<(Guid Id, string Name, DateTime CreatedAt)>
        {
            (variantId, "journal", Older),
        };

        ProjectNameMatch.PickStrict(rows, JournalProjectStore.ProjectName).ShouldBe(variantId);
    }

    [Fact]
    public void PickStrict_ASingleExactRow_IsReturned()
    {
        var exactId = Guid.NewGuid();
        var rows = new List<(Guid Id, string Name, DateTime CreatedAt)>
        {
            (exactId, JournalProjectStore.ProjectName, Older),
        };

        ProjectNameMatch.PickStrict(rows, JournalProjectStore.ProjectName).ShouldBe(exactId);
    }
}
