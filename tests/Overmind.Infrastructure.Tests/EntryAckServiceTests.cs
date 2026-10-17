using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class EntryAckServiceTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly Guid _projectId;
    private readonly Guid _entryId;

    public EntryAckServiceTests()
    {
        _context = _db.CreateContext();

        var project = Project.Create($"acks-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        var entry = ContextEntry.Create(project.Id, EntryType.Note, "lane marshalling", "read before picking work");
        _context.ContextEntries.Add(entry);
        _context.SaveChanges();
        _projectId = project.Id;
        _entryId = entry.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Ack_AttributesTheIdentity_AndLeavesTheEntryUntouched()
    {
        var before = await _context.ContextEntries.AsNoTracking().SingleAsync(e => e.Id == _entryId);

        var outcome = await Service().AckAsync(
            _projectId, _entryId, new DirectiveAuthor("Dan", "qwen", Guid.NewGuid()));

        outcome.AckedNow.ShouldBeTrue();
        outcome.Ack.ContextEntryId.ShouldBe(_entryId);
        outcome.Ack.Alias.ShouldBe("Dan");
        outcome.Ack.Vendor.ShouldBe("qwen");
        outcome.Ack.At.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);

        var after = await _context.ContextEntries.AsNoTracking().SingleAsync(e => e.Id == _entryId);
        after.Content.ShouldBe(before.Content);
        after.UpdatedAt.ShouldBe(before.UpdatedAt);
        after.CurrentVersion.ShouldBe(before.CurrentVersion);
    }

    [Fact]
    public async Task Ack_SameIdentityTwice_IsIdempotentNoOpKeepingTheFirstAt()
    {
        var service = Service();
        var first = await service.AckAsync(
            _projectId, _entryId, new DirectiveAuthor("Dan", "qwen", Guid.NewGuid()));
        var second = await service.AckAsync(
            _projectId, _entryId, new DirectiveAuthor(" Dan ", "qwen", Guid.NewGuid()));

        second.AckedNow.ShouldBeFalse();
        second.Ack.Id.ShouldBe(first.Ack.Id);
        second.Ack.At.ShouldBe(first.Ack.At);
        second.Ack.Alias.ShouldBe("Dan");
        (await RowCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Ack_DifferentIdentity_AddsASecondRow()
    {
        var service = Service();
        await service.AckAsync(_projectId, _entryId, new DirectiveAuthor("Dan", "qwen", Guid.NewGuid()));
        await service.AckAsync(_projectId, _entryId, new DirectiveAuthor("Van", "claude", Guid.NewGuid()));

        (await RowCountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Ack_BlankHumanIdentity_NormalizesToNull_AndStaysIdempotent()
    {
        var service = Service();
        var first = await service.AckAsync(_projectId, _entryId, new DirectiveAuthor("  ", null, null));
        var second = await service.AckAsync(_projectId, _entryId, new DirectiveAuthor(null, "", null));

        first.AckedNow.ShouldBeTrue();
        first.Ack.Alias.ShouldBeNull();
        first.Ack.Vendor.ShouldBeNull();
        second.AckedNow.ShouldBeFalse();
        second.Ack.At.ShouldBe(first.Ack.At);
        (await RowCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Ack_IdentityOverCap_Throws()
    {
        await Should.ThrowAsync<ArgumentException>(() => Service().AckAsync(
            _projectId, _entryId, new DirectiveAuthor(new string('x', 33), "qwen", Guid.NewGuid())));
    }

    [Fact]
    public async Task Ack_UnknownEntry_ThrowsMissingEntry()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() => Service().AckAsync(
            _projectId, Guid.NewGuid(), new DirectiveAuthor("Dan", "qwen", Guid.NewGuid())));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
        ex.Code.ShouldBe(PlanningFailureCode.MissingEntry);
    }

    [Fact]
    public async Task Ack_EntryOfAnotherProject_ThrowsMissingEntry()
    {
        var other = Project.Create($"other-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(other);
        await _context.SaveChangesAsync();

        var ex = await Should.ThrowAsync<PlanningException>(() => Service().AckAsync(
            other.Id, _entryId, new DirectiveAuthor("Dan", "qwen", Guid.NewGuid())));

        ex.Code.ShouldBe(PlanningFailureCode.MissingEntry);
    }

    [Fact]
    public async Task ListForEntry_ReturnsRowsOldestFirst()
    {
        var service = Service();
        await service.AckAsync(_projectId, _entryId, new DirectiveAuthor("first", "claude", Guid.NewGuid()));
        await Task.Delay(5);
        await service.AckAsync(_projectId, _entryId, new DirectiveAuthor("second", "qwen", Guid.NewGuid()));
        await Task.Delay(5);
        await service.AckAsync(_projectId, _entryId, new DirectiveAuthor("third", "codex", Guid.NewGuid()));

        var acks = await service.ListForEntryAsync(_entryId);

        acks.Select(a => a.Alias).ShouldBe(["first", "second", "third"]);
    }

    private EntryAckService Service() => new(_context);

    private async Task<int> RowCountAsync() =>
        await _context.EntryAcks.CountAsync(a => a.ContextEntryId == _entryId);
}
