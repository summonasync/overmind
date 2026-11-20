using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class FeatureClaimService
{
    private readonly OvermindDbContext _db;
    private readonly PlanningMaterializationDispatcher _materialization;
    private readonly NotificationsService? _notifications;

    public FeatureClaimService(
        OvermindDbContext db,
        PlanningMaterializationDispatcher materialization,
        NotificationsService? notifications = null)
    {
        _db = db;
        _materialization = materialization;
        _notifications = notifications;
    }

    // sessionId null = the web human door: no session restrictions, the claim is bound to a fresh
    // session row that goes stale like any other, and the log lands with the Ui source. The human
    // door skips the lane checks for the same reason it skips the claim lock. Lane checks run
    // after the alive-holder throw, and never gate the restart-adopt of a feature already claimed
    // by the same alias — the agent keeps its lane across adapter restarts.
    public async Task<PlanningFeature> ClaimAsync(
        Guid projectId, string slug, string? alias, string? vendor, Guid? sessionId = null,
        bool overrideLaneCheck = false, CancellationToken ct = default)
    {
        var project = await RequireProjectAsync(projectId, ct);
        var feature = await RequireFeatureAsync(projectId, slug, ct);
        (alias, vendor) = await ResolveIdentityAsync(alias, vendor, sessionId, ct);
        var now = DateTime.UtcNow;
        var isHuman = sessionId is null;
        var source = isHuman ? PlanningLogSource.Ui : PlanningLogSource.Agent;
        var effectiveSessionId = sessionId ?? Guid.NewGuid();

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

                feature.Claim(alias, vendor, effectiveSessionId, now, byHuman: isHuman);
                await PersistAsync(feature, alias, vendor, effectiveSessionId, now, ct);
                return feature;
            }

            if (liveness == AgentSessionLiveness.Alive && !isHuman)
            {
                throw PlanningException.FeatureClaimHeld(
                    slug, feature.ClaimedByAlias!, feature.ClaimedByVendor!, liveness);
            }

            var sameAlias = string.Equals(feature.ClaimedByAlias, alias, StringComparison.Ordinal);

            if (!isHuman && !sameAlias && !overrideLaneCheck && feature.Lane is not null)
            {
                await EnsureLaneFreeAsync(project, feature, alias, vendor, ct);
            }

            var adopted = !isHuman && liveness == AgentSessionLiveness.Ended && sameAlias
                          && string.Equals(feature.ClaimedByVendor, vendor, StringComparison.Ordinal);
            var formerAlias = feature.ClaimedByAlias ?? "unknown";
            var formerVendor = feature.ClaimedByVendor ?? "unknown";
            var foreign = !string.Equals(formerAlias, alias, StringComparison.Ordinal)
                          || !string.Equals(formerVendor, vendor, StringComparison.Ordinal);
            var note = adopted
                ? $"session restarted, claim adopted by {alias} by {vendor}"
                : $"{alias} by {vendor} took over {liveness.ToString().ToLowerInvariant()} claim " +
                  $"of {formerAlias} by {formerVendor}";

            feature.Claim(alias, vendor, effectiveSessionId, now, byHuman: isHuman);
            feature.AppendLog(note, source);
            await PersistAsync(feature, alias, vendor, effectiveSessionId, now, ct);

            if (foreign)
            {
                await NotifyTakeoverAsync(projectId, slug, alias, vendor, formerAlias, formerVendor, liveness, ct);
            }

            return feature;
        }

        if (!isHuman && !overrideLaneCheck && feature.Lane is not null)
        {
            await EnsureLaneFreeAsync(project, feature, alias, vendor, ct);
        }

        feature.Claim(alias, vendor, effectiveSessionId, now, byHuman: isHuman);
        feature.AppendLog($"{alias} by {vendor} claimed the feature", source);
        await PersistAsync(feature, alias, vendor, effectiveSessionId, now, ct);
        return feature;
    }

    public async Task<PlanningFeature> UnclaimAsync(
        Guid projectId, string slug, Guid? sessionId = null, CancellationToken ct = default)
    {
        var project = await RequireProjectAsync(projectId, ct);
        var feature = await RequireFeatureAsync(projectId, slug, ct);

        if (feature.ClaimSessionId is not { } holderId)
        {
            return feature;
        }

        if (sessionId is not { } callerId)
        {
            feature.Release();
            feature.AppendLog("claim released via the human door", PlanningLogSource.Ui);
            await ClaimSpanLedger.CloseOpenAsync(_db, feature.Id, DateTime.UtcNow, ct);
            await _db.SaveChangesAsync(ct);
            await _materialization.DispatchAsync(projectId, ct);
            return feature;
        }

        if (holderId != callerId)
        {
            throw await ClaimHeldAsync(feature, project, holderId, ct);
        }

        var session = await _db.AgentSessions.FirstOrDefaultAsync(s => s.Id == callerId, ct);
        session?.Touch();
        feature.Release();
        feature.AppendLog(
            $"{session?.Alias ?? "unknown"} by {session?.Vendor ?? "unknown"} released the claim",
            PlanningLogSource.Agent);
        await ClaimSpanLedger.CloseOpenAsync(_db, feature.Id, DateTime.UtcNow, ct);
        await _db.SaveChangesAsync(ct);
        await _materialization.DispatchAsync(projectId, ct);
        return feature;
    }

    // A renamed session takes its held claims with it: every snapshot bound to the session id moves
    // to the new pair, one feature log line records the move, and the open span is re-opened under
    // it. Claim time and session binding are untouched — the hold does not restart because the alias
    // did. Returns the slugs that moved, so the caller can echo them.
    public async Task<IReadOnlyList<string>> ReattributeSessionClaimsAsync(
        Guid sessionId, string alias, string vendor, CancellationToken ct = default)
    {
        var held = await _db.PlanningFeatures
            .Where(f => f.ClaimSessionId == sessionId)
            .OrderBy(f => f.Slug)
            .ToListAsync(ct);

        List<string> moved = [];
        List<Guid> movedProjects = [];
        var now = DateTime.UtcNow;

        foreach (var feature in held)
        {
            var formerAlias = feature.ClaimedByAlias ?? "unknown";
            var formerVendor = feature.ClaimedByVendor ?? "unknown";

            if (!feature.Reattribute(alias, vendor))
            {
                continue;
            }

            feature.AppendLog(
                $"claim re-attributed: {formerAlias} by {formerVendor} → {alias} by {vendor}",
                PlanningLogSource.Agent);
            await ClaimSpanLedger.ReattributeOpenAsync(_db, feature, alias, vendor, now, ct);
            moved.Add(feature.Slug);
            movedProjects.Add(feature.ProjectId);
        }

        if (moved.Count == 0)
        {
            return moved;
        }

        await _db.SaveChangesAsync(ct);
        foreach (var projectId in movedProjects.Distinct())
        {
            await _materialization.DispatchAsync(projectId, ct);
        }

        return moved;
    }

    public async Task<IReadOnlyDictionary<Guid, AgentSessionLiveness>> ClaimLivenessAsync(
        Guid projectId, IReadOnlyCollection<PlanningFeature> features, CancellationToken ct = default)
    {
        var claimed = features.Where(f => f.ClaimSessionId is not null).ToList();
        if (claimed.Count == 0)
        {
            return new Dictionary<Guid, AgentSessionLiveness>();
        }

        var project = await RequireProjectAsync(projectId, ct);
        var sessionIds = claimed.Select(f => f.ClaimSessionId!.Value).Distinct().ToList();
        var sessions = await _db.AgentSessions
            .Where(s => sessionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);
        var now = DateTime.UtcNow;

        return claimed.ToDictionary(
            f => f.Id,
            f => sessions.TryGetValue(f.ClaimSessionId!.Value, out var session)
                ? session.Liveness(now, project.ClaimStaleThresholdMinutes).ToClaimState()
                : AgentSessionLiveness.Released);
    }

    // Read-only warning projection: the lane's live surfaces whose globs overlap the feature's
    // codeSurfaces. Informational only — a feature without codeSurfaces or a lane gets silence,
    // never an error, and nothing here blocks editing.
    public async Task<IReadOnlyList<LiveSurfaceEntry>> LiveSurfacesForClaimAsync(
        PlanningFeature feature, CancellationToken ct = default)
    {
        if (feature.Lane is null || string.IsNullOrWhiteSpace(feature.CodeSurfaces))
        {
            return [];
        }

        var patterns = CodeSurfacesJson.Deserialize(feature.CodeSurfaces);
        if (patterns.Count == 0)
        {
            return [];
        }

        var lane = await _db.PlanningLanes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.ProjectId == feature.ProjectId && l.Name == feature.Lane, ct);
        if (string.IsNullOrEmpty(lane?.LiveSurfaces))
        {
            return [];
        }

        return [.. LiveSurfacesJson.Deserialize(lane.LiveSurfaces)
            .Where(e => patterns.Any(p => CodeSurfaceOverlap.GlobsOverlap(e.Glob, p)))];
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
        Guid artifactId, Guid? sessionId, CancellationToken ct = default)
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

    public async Task EnsureImportAllowedAsync(
        Guid projectId, Guid? sessionId, string slug, CancellationToken ct = default)
    {
        if (sessionId is null)
        {
            return;
        }

        var feature = await _db.PlanningFeatures
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct);
        if (feature is null)
        {
            return;
        }

        await EnsureHolderCoreAsync(feature, sessionId, ct);
    }

    // Write-path gate behind the ClaimLockFilter. Claim binding follows the session id: the same
    // session keeps its claim alive (Touch re-animates a stale row) and gets a logged re-adopt when
    // the row comes back stale or ended; any other session keeps the explicit claim call as the
    // only takeover door, alive or stale — matching aliases no longer auto-adopt on writes.
    private async Task EnsureHolderCoreAsync(PlanningFeature feature, Guid? sessionId, CancellationToken ct)
    {
        if (feature.ClaimSessionId is not { } holderId)
        {
            return;
        }

        var project = await RequireProjectAsync(feature.ProjectId, ct);
        var holder = await _db.AgentSessions.FirstOrDefaultAsync(s => s.Id == holderId, ct);
        var liveness = holder?.Liveness(DateTime.UtcNow, project.ClaimStaleThresholdMinutes)
                       ?? AgentSessionLiveness.Ended;

        if (holderId != sessionId)
        {
            throw PlanningException.FeatureClaimHeld(
                feature.Slug, feature.ClaimedByAlias!, feature.ClaimedByVendor!, liveness);
        }

        if (liveness == AgentSessionLiveness.Alive || holder is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        feature.Claim(holder.Alias, holder.Vendor, holderId, now);
        feature.AppendLog(
            $"stale claim re-adopted by resumed session {holder.Alias} by {holder.Vendor}",
            PlanningLogSource.Agent);
        await PersistAsync(feature, holder.Alias, holder.Vendor, holderId, now, ct);
    }

    private async Task<(string Alias, string Vendor)> ResolveIdentityAsync(
        string? alias, string? vendor, Guid? sessionId, CancellationToken ct)
    {
        var knownAlias = BlankToIdentity(alias);
        var knownVendor = BlankToIdentity(vendor);

        if (knownAlias is not null && knownVendor is not null)
        {
            return (knownAlias, knownVendor);
        }

        if (sessionId is not { } callerId)
        {
            throw new ArgumentException("Alias and vendor are required to claim through the human door.");
        }

        var session = await _db.AgentSessions.FirstOrDefaultAsync(s => s.Id == callerId, ct)
                      ?? throw new ArgumentException(
                          $"Session '{callerId}' is not registered — claim with explicit alias and vendor.");

        return (knownAlias ?? session.Alias, knownVendor ?? session.Vendor);
    }

    private static string? BlankToIdentity(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<PlanningException> ClaimHeldAsync(
        PlanningFeature feature, Project project, Guid holderId, CancellationToken ct)
    {
        var holder = await _db.AgentSessions.FirstOrDefaultAsync(s => s.Id == holderId, ct);
        var liveness = holder?.Liveness(DateTime.UtcNow, project.ClaimStaleThresholdMinutes)
                       ?? AgentSessionLiveness.Ended;
        return PlanningException.FeatureClaimHeld(
            feature.Slug, feature.ClaimedByAlias!, feature.ClaimedByVendor!, liveness);
    }

    private async Task NotifyTakeoverAsync(
        Guid projectId, string slug, string alias, string vendor,
        string formerAlias, string formerVendor, AgentSessionLiveness liveness, CancellationToken ct)
    {
        if (_notifications is null)
        {
            return;
        }

        var text = $"{alias} by {vendor} took over the {liveness.ToString().ToLowerInvariant()} claim " +
                   $"of '{slug}' from {formerAlias} by {formerVendor}";
        await _notifications.CreateAsync(NotificationKind.Info, text, projectId, slug, tab: null, agent: null, ct);
    }

    private async Task PersistAsync(
        PlanningFeature feature, string alias, string vendor, Guid sessionId,
        DateTime now, CancellationToken ct)
    {
        await ClaimSpanLedger.OpenAsync(_db, feature, alias, vendor, sessionId, now, ct);

        var session = await _db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            _db.AgentSessions.Add(AgentSession.Restore(sessionId, alias, vendor, now, now));
        }
        else
        {
            if (session.Alias.Equals(AgentSession.PlaceholderAlias, StringComparison.Ordinal) &&
                !session.Alias.Equals(alias, StringComparison.Ordinal))
            {
                session.RenameAlias(alias);
            }

            session.Touch();
        }

        await _db.SaveChangesAsync(ct);
        await _materialization.DispatchAsync(feature.ProjectId, ct);
    }

    private async Task EnsureLaneFreeAsync(
        Project project, PlanningFeature target, string alias, string vendor, CancellationToken ct)
    {
        var lane = target.Lane!;
        var candidates = await _db.PlanningFeatures
            .Where(f => f.ProjectId == project.Id && f.Lane != null && f.Id != target.Id)
            .ToListAsync(ct);
        var laneFeatures = candidates
            .Where(f => string.Equals(f.Lane, lane, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (laneFeatures.Count == 0)
        {
            return;
        }

        var liveness = await ClaimLivenessAsync(project.Id, laneFeatures, ct);

        var foreignClaim = laneFeatures.FirstOrDefault(f =>
            f.ClaimSessionId is not null &&
            liveness.GetValueOrDefault(f.Id) == AgentSessionLiveness.Alive &&
            !HoldsCallerIdentity(f, alias, vendor));
        if (foreignClaim is not null)
        {
            throw PlanningException.LaneClaimHeld(
                lane, foreignClaim.Slug, foreignClaim.ClaimedByAlias!, foreignClaim.ClaimedByVendor!,
                AgentSessionLiveness.Alive);
        }
    }

    private static bool HoldsCallerIdentity(PlanningFeature feature, string alias, string vendor) =>
        string.Equals(feature.ClaimedByAlias, alias, StringComparison.Ordinal) &&
        string.Equals(feature.ClaimedByVendor, vendor, StringComparison.Ordinal);

    private async Task<Project> RequireProjectAsync(Guid projectId, CancellationToken ct) =>
        await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
        ?? throw PlanningException.MissingProject(projectId);

    private async Task<PlanningFeature> RequireFeatureAsync(Guid projectId, string slug, CancellationToken ct) =>
        await _db.PlanningFeatures.FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct)
        ?? throw PlanningException.MissingFeature(slug);
}
