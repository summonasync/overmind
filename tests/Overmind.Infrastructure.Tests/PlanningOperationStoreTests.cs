using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningOperationStoreTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly PlanningOperationStore _store;
    private readonly Guid _projectId;
    private readonly Guid _otherProjectId;

    public PlanningOperationStoreTests()
    {
        _context = _db.CreateContext();
        _store = new PlanningOperationStore(_context);

        var project = Project.Create($"ops-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        var other = Project.Create($"ops-other-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.AddRange(project, other);
        _context.SaveChanges();
        _projectId = project.Id;
        _otherProjectId = other.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Save_Find_RoundTripsTheStoredResponse()
    {
        var operationId = Guid.NewGuid();

        await _store.SaveAsync(_projectId, operationId, "hash-1", 201, "{\"slug\":\"001-x\"}", CancellationToken.None);
        var stored = await _store.FindAsync(_projectId, operationId, CancellationToken.None);

        stored.ShouldNotBeNull();
        stored.RequestHash.ShouldBe("hash-1");
        stored.StatusCode.ShouldBe(201);
        stored.ResponseBody.ShouldBe("{\"slug\":\"001-x\"}");
        stored.CreatedAt.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
    }

    [Fact]
    public async Task Find_UnknownOperation_ReturnsNull()
    {
        (await _store.FindAsync(_projectId, Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task Save_PurgesOnlyExpiredRowsOfTheSameProject()
    {
        var expired = PlanningOperationRecord.Create(
            _projectId, Guid.NewGuid(), "stale", 200, "{}", DateTime.UtcNow.AddHours(-25));
        var freshOnOtherProject = PlanningOperationRecord.Create(
            _otherProjectId, Guid.NewGuid(), "stale", 200, "{}", DateTime.UtcNow.AddHours(-25));
        _context.PlanningOperationRecords.AddRange(expired, freshOnOtherProject);
        await _context.SaveChangesAsync();

        await _store.SaveAsync(_projectId, Guid.NewGuid(), "hash-2", 200, "{}", CancellationToken.None);

        var remaining = await _context.PlanningOperationRecords
            .AsNoTracking()
            .ToListAsync();
        remaining.ShouldContain(r => r.ProjectId == _otherProjectId);
        remaining.Where(r => r.ProjectId == _projectId).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Save_DuplicateOperationId_KeepsTheFirstRow()
    {
        var operationId = Guid.NewGuid();
        await _store.SaveAsync(_projectId, operationId, "hash-first", 200, "{\"n\":1}", CancellationToken.None);

        await _store.SaveAsync(_projectId, operationId, "hash-second", 200, "{\"n\":2}", CancellationToken.None);

        var stored = await _store.FindAsync(_projectId, operationId, CancellationToken.None);
        stored.ShouldNotBeNull();
        stored.RequestHash.ShouldBe("hash-first");
        stored.ResponseBody.ShouldBe("{\"n\":1}");
    }

    [Fact]
    public async Task ResolveProjectByArtifact_UnknownArtifact_ReturnsNull()
    {
        (await _store.ResolveProjectByArtifactAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task ResolveProjectByArtifact_ArtifactOfTheProject_ResolvesTheOwner()
    {
        var feature = PlanningFeature.Create(_projectId, "001-ops", "Ops", "body");
        _context.PlanningFeatures.Add(feature);
        _context.SaveChanges();

        var artifact = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Task, "<goal>x</goal>");
        _context.PlanningArtifacts.Add(artifact);
        await _context.SaveChangesAsync();

        var resolved = await _store.ResolveProjectByArtifactAsync(artifact.Id, CancellationToken.None);

        resolved.ShouldBe(_projectId);
    }
}
