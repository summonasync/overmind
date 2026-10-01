using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class FeatureClaimService
{
    private readonly OvermindDbContext _db;
    private readonly IPlanningMaterializer _materializer;
    private readonly ILogger<FeatureClaimService> _logger;

    public FeatureClaimService(
        OvermindDbContext db,
        IPlanningMaterializer materializer,
        ILogger<FeatureClaimService> logger)
    {
        _db = db;
        _materializer = materializer;
        _logger = logger;
    }

    public async Task<PlanningFeature> ClaimAsync(
        Guid projectId, string slug, string alias, string vendor, Guid sessionId,
        CancellationToken ct = default)
    {
        var project = await RequireProjectAsync(projectId, ct);
        var feature = await RequireFeatureAsync(projectId, slug, ct);
        var now = DateTime.UtcNow;

        if (feature.ClaimSessionId is { } holderId)
        {
            var holder = await _db.AgentSessions.FirstOrDefaultAsync(s => s.Id == holderId, ct);
            var liveness = holder?.Liveness(now, project.ClaimStaleThresholdMinutes)
                           ?? AgentSessionLiveness.Ended;

            if (holderId == sessionId)
            {
                if (liveness == AgentSessionLiveness.Alive)
                {
                    return feature;
                }

                feature.Claim(alias, vendor, sessionId, now);
                await PersistAsync(projectId, alias, vendor, sessionId, now, ct);
                return feature;
            }

            if (liveness == AgentSessionLiveness.Alive)
            {
                throw PlanningException.FeatureClaimHeld(
                    slug, feature.ClaimedByAlias!, feature.ClaimedByVendor!, liveness);
            }

            var adopted = liveness == AgentSessionLiveness.Ended
                          && string.Equals(feature.ClaimedByAlias, alias.Trim(), StringComparison.Ordinal)
                          && string.Equals(feature.ClaimedByVendor, vendor.Trim(), StringComparison.Ordinal);
            var note = adopted
                ? $"session restarted, claim adopted by {alias} by {vendor}"
                : $"{alias} by {vendor} took over {liveness.ToString().ToLowerInvariant()} claim " +
                  $"of {feature.ClaimedByAlias} by {feature.ClaimedByVendor}";

            feature.Claim(alias, vendor, sessionId, now);
            feature.AppendLog(note, PlanningLogSource.Agent);
            await PersistAsync(projectId, alias, vendor, sessionId, now, ct);
            return feature;
        }

        feature.Claim(alias, vendor, sessionId, now);
        feature.AppendLog($"{alias} by {vendor} claimed the feature", PlanningLogSource.Agent);
        await PersistAsync(projectId, alias, vendor, sessionId, now, ct);
        return feature;
    }

    public async Task<PlanningFeature> UnclaimAsync(
        Guid projectId, string slug, Guid sessionId, CancellationToken ct = default)
    {
        var project = await RequireProjectAsync(projectId, ct);
        var feature = await RequireFeatureAsync(projectId, slug, ct);

        if (feature.ClaimSessionId is not { } holderId)
        {
            return feature;
        }

        if (holderId != sessionId)
        {
            throw await ClaimHeldAsync(feature, project, holderId, ct);
        }

        var session = await _db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        session?.Touch();
        feature.Release();
        feature.AppendLog(
            $"{session?.Alias ?? "unknown"} by {session?.Vendor ?? "unknown"} released the claim",
            PlanningLogSource.Agent);
        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, projectId, ct);
        return feature;
    }

    public async Task EnsureHolderAsync(
        Guid projectId, string slug, Guid? sessionId, CancellationToken ct = default)
    {
        if (sessionId is null)
        {
            return;
        }

        var feature = await RequireFeatureAsync(projectId, slug, ct);
        await EnsureHolderCoreAsync(feature, sessionId, ct);
    }

    public async Task EnsureHolderForArtifactAsync(
        Guid? sessionId, Guid artifactId, CancellationToken ct = default)
    {
        if (sessionId is null)
        {
            return;
        }

        var feature = await _db.PlanningArtifacts
            .Where(a => a.Id == artifactId)
            .Join(_db.PlanningFeatures, a => a.FeatureId, f => f.Id, (a, f) => f)
            .FirstOrDefaultAsync(ct);

        if (feature is null)
        {
            throw PlanningException.MissingArtifact(artifactId);
        }

        await EnsureHolderCoreAsync(feature, sessionId, ct);
    }

    private async Task EnsureHolderCoreAsync(PlanningFeature feature, Guid? sessionId, CancellationToken ct)
    {
        if (feature.ClaimSessionId is not { } holderId || holderId == sessionId)
        {
            return;
        }

        var project = await RequireProjectAsync(feature.ProjectId, ct);
        throw await ClaimHeldAsync(feature, project, holderId, ct);
    }

    private async Task<PlanningException> ClaimHeldAsync(
        PlanningFeature feature, Project project, Guid holderId, CancellationToken ct)
    {
        var holder = await _db.AgentSessions.FirstOrDefaultAsync(s => s.Id == holderId, ct);
        var liveness = holder?.Liveness(DateTime.UtcNow, project.ClaimStaleThresholdMinutes)
                       ?? AgentSessionLiveness.Ended;
        return PlanningException.FeatureClaimHeld(
            feature.Slug, feature.ClaimedByAlias!, feature.ClaimedByVendor!, liveness);
    }

    private async Task PersistAsync(
        Guid projectId, string alias, string vendor, Guid sessionId, DateTime now, CancellationToken ct)
    {
        var session = await _db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            _db.AgentSessions.Add(AgentSession.Restore(sessionId, alias, vendor, now, now));
        }
        else
        {
            session.Touch();
        }

        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, projectId, ct);
    }

    private async Task<Project> RequireProjectAsync(Guid projectId, CancellationToken ct) =>
        await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
        ?? throw PlanningException.MissingProject(projectId);

    private async Task<PlanningFeature> RequireFeatureAsync(Guid projectId, string slug, CancellationToken ct) =>
        await _db.PlanningFeatures.FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct)
        ?? throw PlanningException.MissingFeature(slug);
}
