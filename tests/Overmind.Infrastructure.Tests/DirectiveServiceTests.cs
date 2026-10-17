using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class DirectiveServiceTests : IDisposable
{
    private static readonly Guid HolderSessionId = Guid.NewGuid();
    private static readonly Guid ForeignSessionId = Guid.NewGuid();

    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;
    private readonly string _slug = "directed";

    public DirectiveServiceTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();

        var project = Project.Create($"directives-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.PlanningFeatures.Add(PlanningFeature.Create(project.Id, _slug, "Directed", "body"));
        _context.SaveChanges();
        _projectId = project.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Append_FromForeignSession_OnLiveClaimedFeature_Succeeds()
    {
        await ClaimByHolderAsync();

        var directive = await Service().AppendAsync(
            _projectId, _slug, "HOLD: shared file inbound",
            new DirectiveAuthor("Marshal", "wave-marshal", ForeignSessionId));

        directive.Text.ShouldBe("HOLD: shared file inbound");
        directive.AuthorAlias.ShouldBe("Marshal");
        directive.AuthorVendor.ShouldBe("wave-marshal");
        directive.AuthorSessionId.ShouldBe(ForeignSessionId);
        directive.Acked.ShouldBeFalse();
    }

    [Fact]
    public async Task Append_OnDoneFeature_Succeeds()
    {
        var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == _slug);
        _context.PlanningArtifacts.Add(
            PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Result, "close-out"));
        await _context.SaveChangesAsync();
        feature.SetStatus(PlanningFeatureStatus.Done);
        await _context.SaveChangesAsync();

        var directive = await Service().AppendAsync(
            _projectId, _slug, "post-close note", new DirectiveAuthor("Dan", "qwen", HolderSessionId));

        directive.Acked.ShouldBeFalse();
    }

    [Fact]
    public async Task Append_TrimsText_AndRejectsBlankOrOverCap()
    {
        var appended = await Service().AppendAsync(
            _projectId, _slug, "  padded directive  ", new DirectiveAuthor("Dan", "qwen", HolderSessionId));
        appended.Text.ShouldBe("padded directive");

        await Should.ThrowAsync<ArgumentException>(() => Service().AppendAsync(
            _projectId, _slug, "   ", new DirectiveAuthor("Dan", "qwen", HolderSessionId)));

        await Should.ThrowAsync<ArgumentException>(() => Service().AppendAsync(
            _projectId, _slug, new string('x', PlanningDirective.MaxTextLength + 1),
            new DirectiveAuthor("Dan", "qwen", HolderSessionId)));
    }

    [Fact]
    public async Task Append_UnknownFeature_ThrowsMissingFeature()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() => Service().AppendAsync(
            _projectId, "missing-feature", "text", new DirectiveAuthor("Dan", "qwen", HolderSessionId)));

        ex.Code.ShouldBe(PlanningFailureCode.MissingFeature);
    }

    [Fact]
    public async Task Ack_FlipsStampOnce_SecondAckIsIdempotentNoOp()
    {
        var service = Service();
        var directive = await service.AppendAsync(
            _projectId, _slug, "SYNC on proxy.ts", new DirectiveAuthor("Marshal", "wave-marshal", ForeignSessionId));

        var first = await service.AckAsync(
            _projectId, _slug, directive.Id, new DirectiveAuthor("Dan", "qwen", HolderSessionId));
        var second = await service.AckAsync(
            _projectId, _slug, directive.Id, new DirectiveAuthor("Other", "claude", Guid.NewGuid()));

        first.AckedNow.ShouldBeTrue();
        first.Directive.AckByAlias.ShouldBe("Dan");
        first.Directive.AckVendor.ShouldBe("qwen");
        first.Directive.AckAt.ShouldNotBeNull();

        second.AckedNow.ShouldBeFalse();
        second.Directive.AckByAlias.ShouldBe("Dan");
        second.Directive.AckVendor.ShouldBe("qwen");
        second.Directive.AckAt.ShouldBe(first.Directive.AckAt);
    }

    [Fact]
    public async Task Ack_UnknownDirective_ThrowsMissingDirective()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() => Service().AckAsync(
            _projectId, _slug, Guid.NewGuid(), new DirectiveAuthor("Dan", "qwen", HolderSessionId)));

        ex.Code.ShouldBe(PlanningFailureCode.MissingDirective);
    }

    [Fact]
    public async Task Ack_DirectiveOfAnotherFeature_ThrowsMissingDirective()
    {
        _context.PlanningFeatures.Add(PlanningFeature.Create(_projectId, "other-directed", "Other", "body"));
        await _context.SaveChangesAsync();
        var foreign = await Service().AppendAsync(
            _projectId, "other-directed", "wrong channel",
            new DirectiveAuthor("Marshal", "wave-marshal", ForeignSessionId));

        var ex = await Should.ThrowAsync<PlanningException>(() => Service().AckAsync(
            _projectId, _slug, foreign.Id, new DirectiveAuthor("Dan", "qwen", HolderSessionId)));

        ex.Code.ShouldBe(PlanningFailureCode.MissingDirective);
    }

    [Fact]
    public async Task ListOpen_ReturnsUnackedOnly_OldestFirst()
    {
        var service = Service();
        var first = await service.AppendAsync(
            _projectId, _slug, "first", new DirectiveAuthor("Marshal", "wave-marshal", ForeignSessionId));
        await service.AppendAsync(
            _projectId, _slug, "second", new DirectiveAuthor("Marshal", "wave-marshal", ForeignSessionId));
        var third = await service.AppendAsync(
            _projectId, _slug, "third", new DirectiveAuthor("Marshal", "wave-marshal", ForeignSessionId));
        await service.AckAsync(
            _projectId, _slug, first.Id, new DirectiveAuthor("Dan", "qwen", HolderSessionId));

        var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == _slug);
        var open = await service.ListOpenAsync(feature.Id);

        open.Count.ShouldBe(2);
        open.Select(d => d.Text).ShouldBe(["second", "third"]);
        open.ShouldNotContain(d => d.Id == first.Id);
        third.Id.ShouldBe(open.Last().Id);
    }

    [Fact]
    public async Task OpenCounts_AggregatesUnackedPerFeature_WithinTheProjectOnly()
    {
        var service = Service();
        var otherProject = Project.Create($"other-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(otherProject);
        _context.PlanningFeatures.Add(PlanningFeature.Create(otherProject.Id, "elsewhere", "Elsewhere", "body"));
        await _context.SaveChangesAsync();

        var kept = await service.AppendAsync(
            _projectId, _slug, "open one", new DirectiveAuthor("Marshal", "wave-marshal", ForeignSessionId));
        await service.AppendAsync(
            _projectId, _slug, "open two", new DirectiveAuthor("Marshal", "wave-marshal", ForeignSessionId));
        await service.AppendAsync(
            otherProject.Id, "elsewhere", "foreign open", new DirectiveAuthor("Marshal", "wave-marshal", ForeignSessionId));
        await service.AckAsync(
            _projectId, _slug, kept.Id, new DirectiveAuthor("Dan", "qwen", HolderSessionId));

        var counts = await service.OpenCountsAsync(_projectId);

        counts.Count.ShouldBe(1);
        counts.Single().Value.ShouldBe(1);
        (await service.OpenCountsAsync(otherProject.Id)).Single().Value.ShouldBe(1);
    }

    [Fact]
    public void Projection_FromAckedDirective_CarriesAckShape()
    {
        var directive = PlanningDirective.Create(
            Guid.NewGuid(), "text", "Marshal", "wave-marshal", ForeignSessionId);
        DirectiveService.Projection(directive).Acked.ShouldBeFalse();

        directive.Ack("Dan", "qwen", DateTime.UtcNow);
        var projection = DirectiveService.Projection(directive);
        projection.Acked.ShouldBeTrue();
        projection.ByAlias.ShouldBe("Dan");
        projection.Vendor.ShouldBe("qwen");
        projection.At.ShouldNotBeNull();
    }

    private DirectiveService Service() => new(_context);

    private async Task ClaimByHolderAsync()
    {
        var claims = new FeatureClaimService(_context, TestPlanningDispatcher.Create(_context, _materializer));
        await claims.ClaimAsync(_projectId, _slug, "Dan", "qwen", HolderSessionId);
        _context.ChangeTracker.Clear();
    }
}
