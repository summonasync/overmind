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
    [InlineData(EntryLinkKind.Task)]
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
    public void CreateManual_SetsFieldsAndManualOrigin()
    {
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();

        var link = EntryLink.CreateManual(from, EntryLinkKind.Task, to);

        link.Id.ShouldNotBe(Guid.Empty);
        link.FromEntryId.ShouldBe(from);
        link.ToKind.ShouldBe(EntryLinkKind.Task);
        link.ToId.ShouldBe(to);
        link.Origin.ShouldBe(EntryLinkOrigin.Manual);
        link.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(EntryLinkKind.Entry)]
    [InlineData(EntryLinkKind.Feature)]
    [InlineData(EntryLinkKind.Project)]
    [InlineData(EntryLinkKind.Task)]
    public void CreateManual_AnyTargetKind_IsStored(EntryLinkKind kind)
    {
        var link = EntryLink.CreateManual(Guid.NewGuid(), kind, Guid.NewGuid());

        link.ToKind.ShouldBe(kind);
        link.Origin.ShouldBe(EntryLinkOrigin.Manual);
    }

    [Fact]
    public void CreateManual_SelfLink_Throws()
    {
        var id = Guid.NewGuid();

        Should.Throw<InvalidOperationException>(() =>
            EntryLink.CreateManual(id, EntryLinkKind.Feature, id));
    }

    [Fact]
    public void CreateManual_EmptyTarget_Throws()
    {
        var ex = Should.Throw<ArgumentException>(() =>
            EntryLink.CreateManual(Guid.NewGuid(), EntryLinkKind.Task, Guid.Empty));

        ex.ParamName.ShouldBe("toId");
    }

    [Fact]
    public void CreateManual_SamePairAsDerived_KeepsItsOwnOrigin()
    {
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();

        var derived = EntryLink.CreateDerived(from, EntryLinkKind.Feature, to);
        var manual = EntryLink.CreateManual(from, EntryLinkKind.Feature, to);

        derived.Origin.ShouldBe(EntryLinkOrigin.Derived);
        manual.Origin.ShouldBe(EntryLinkOrigin.Manual);
        manual.Id.ShouldNotBe(derived.Id);
    }

    [Fact]
    public void EntryLinkKind_IsTheClosedTargetVocabulary()
    {
        Enum.GetNames<EntryLinkKind>().ShouldBe(["Entry", "Feature", "Project", "Task"]);
        Enum.GetValues<EntryLinkKind>().Select(kind => (int)kind).ShouldBe([0, 1, 2, 3]);
    }

    [Fact]
    public void EntryLinkKind_OrdinalsAreFrozenForTheWireContract()
    {
        ((int)EntryLinkKind.Entry).ShouldBe(0);
        ((int)EntryLinkKind.Feature).ShouldBe(1);
        ((int)EntryLinkKind.Project).ShouldBe(2);
        ((int)EntryLinkKind.Task).ShouldBe(3);
    }

    [Fact]
    public void EntryLinkOrigin_IsAppendOnlyWithManualReserved()
    {
        Enum.GetNames<EntryLinkOrigin>().ShouldBe(["Derived", "Manual"]);
        Enum.GetValues<EntryLinkOrigin>().Select(origin => (int)origin).ShouldBe([0, 1]);
    }
}
