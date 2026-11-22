using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Entries;

public sealed class ProjectNameMatchTests
{
    private static readonly DateTime Older = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Newer = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

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
