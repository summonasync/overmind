using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entities;

public class EntryLinkTests
{
    [Fact]
    public void CreateDerived_SetsFieldsAndDerivedOrigin()
    {
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var link = EntryLink.CreateDerived(from, EntryLinkKind.Feature, to);

        link.Id.ShouldNotBe(Guid.Empty);
        link.FromEntryId.ShouldBe(from);
        link.ToKind.ShouldBe(EntryLinkKind.Feature);
        link.ToId.ShouldBe(to);
        link.Origin.ShouldBe(EntryLinkOrigin.Derived);
        link.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
        link.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(EntryLinkKind.Entry)]
    [InlineData(EntryLinkKind.Feature)]
    [InlineData(EntryLinkKind.Project)]
    public void CreateDerived_AnyTargetKind_IsStored(EntryLinkKind kind)
    {
        var link = EntryLink.CreateDerived(Guid.NewGuid(), kind, Guid.NewGuid());

        link.ToKind.ShouldBe(kind);
    }

    [Fact]
    public void CreateDerived_SelfLink_Throws()
    {
        var id = Guid.NewGuid();

        Should.Throw<InvalidOperationException>(() =>
            EntryLink.CreateDerived(id, EntryLinkKind.Entry, id));
    }

    [Fact]
    public void CreateDerived_EmptyTarget_Throws()
    {
        var ex = Should.Throw<ArgumentException>(() =>
            EntryLink.CreateDerived(Guid.NewGuid(), EntryLinkKind.Project, Guid.Empty));

        ex.ParamName.ShouldBe("toId");
    }

    [Fact]
    public void CreateDerived_EachCall_GetsItsOwnId()
    {
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();

        var first = EntryLink.CreateDerived(from, EntryLinkKind.Entry, to);
        var second = EntryLink.CreateDerived(from, EntryLinkKind.Entry, to);

        first.Id.ShouldNotBe(second.Id);
    }

    [Fact]
    public void EntryLinkKind_IsTheClosedTargetVocabulary()
    {
        Enum.GetNames<EntryLinkKind>().ShouldBe(["Entry", "Feature", "Project"]);
        Enum.GetValues<EntryLinkKind>().Select(kind => (int)kind).ShouldBe([0, 1, 2]);
    }

    [Fact]
    public void EntryLinkOrigin_IsAppendOnlyWithManualReserved()
    {
        Enum.GetNames<EntryLinkOrigin>().ShouldBe(["Derived", "Manual"]);
        Enum.GetValues<EntryLinkOrigin>().Select(origin => (int)origin).ShouldBe([0, 1]);
    }
}
