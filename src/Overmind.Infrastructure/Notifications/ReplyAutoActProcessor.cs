using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Infrastructure.Notifications;

public sealed class ReplyAutoActProcessor(
    OvermindDbContext db,
    PlanningArtifactService artifacts,
    PlanningFeatureService features,
    FeatureDependencyService dependencies,
    ILogger<ReplyAutoActProcessor> logger) : IReplyAutoActProcessor
{
    public async Task<ReplyAutoActResult> ProcessAsync(Guid notificationId, CancellationToken ct)
    {
        var notification = await db.AgentNotifications
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notificationId, ct);

        if (notification is null)
        {
            return ReplyAutoActResult.Skipped("notification no longer exists");
        }

        if (notification.Kind != NotificationKind.Approval
            || notification.FeatureSlug is null
            || notification.ProjectId is null)
        {
            return ReplyAutoActResult.Skipped("reply is not an approval carrying a feature slug");
        }

        var autoActEnabled = await db.Projects
            .AsNoTracking()
            .Where(p => p.Id == notification.ProjectId)
            .Select(p => (bool?)p.AutoActOnReplies)
            .FirstOrDefaultAsync(ct);

        if (autoActEnabled is null)
        {
            return ReplyAutoActResult.Skipped("project no longer exists");
        }

        if (!autoActEnabled.Value)
        {
            return ReplyAutoActResult.Skipped("auto-act disabled for the project");
        }

        var feature = await db.PlanningFeatures
            .AsNoTracking()
            .FirstOrDefaultAsync(
                f => f.ProjectId == notification.ProjectId && f.Slug == notification.FeatureSlug, ct);

        if (feature is null)
        {
            return ReplyAutoActResult.Skipped($"feature '{notification.FeatureSlug}' not found");
        }

        if (notification.Tab is null)
        {
            return await CloseFeatureAsync(notification, feature, ct);
        }

        var document = await ResolveDocumentAsync(feature.Id, notification.Tab, ct);
        if (document is { Status: PlanningArtifactStatus.Draft })
        {
            return await ValidateDocumentAsync(notification, document, ct);
        }

        return ReplyAutoActResult.Skipped(
            $"feature '{notification.FeatureSlug}' has no draft '{notification.Tab}' artifact to validate");
    }

    private async Task<ReplyAutoActResult> ValidateDocumentAsync(
        AgentNotification notification, PlanningArtifact document, CancellationToken ct)
    {
        try
        {
            await artifacts.ValidateAsync(
                document.Id, logNote: null, source: PlanningLogSource.Auto, requireHumanProof: true, ct);
        }
        catch (PlanningException ex) when (ex.Code == PlanningFailureCode.ValidationProofRequired)
        {
            return ReplyAutoActResult.Skipped("reply is stale against the artifact's last content update");
        }

        await artifacts.AppendLogAsync(document.Id, AuditLine(notification.Id), ct: ct);

        logger.LogInformation(
            "auto-act validated {Kind} of feature {FeatureSlug} from human reply {NotificationId}",
            document.Kind, notification.FeatureSlug, notification.Id);

        await NotifyAuthorAsync(
            notification,
            $"auto-act: {document.Kind.ToString().ToLowerInvariant()} of '{notification.FeatureSlug}' validated by your reply",
            ct);

        return ReplyAutoActResult.Performed($"{document.Kind} validated");
    }

    private async Task<ReplyAutoActResult> CloseFeatureAsync(
        AgentNotification notification, PlanningFeature feature, CancellationToken ct)
    {
        if (feature.Status == PlanningFeatureStatus.Done)
        {
            return ReplyAutoActResult.Skipped("feature already done");
        }

        var openBlockers = await dependencies.OpenBlockersAsync(feature.ProjectId, feature.Slug, ct);
        if (openBlockers.Count > 0)
        {
            return ReplyAutoActResult.Skipped(
                $"unclosed blockers: {string.Join(", ", openBlockers.Select(b => b.Slug))}");
        }

        var resultValidated = await db.PlanningArtifacts
            .AsNoTracking()
            .AnyAsync(
                a => a.FeatureId == feature.Id
                    && a.Kind == PlanningArtifactKind.Result
                    && a.Status == PlanningArtifactStatus.Validated,
                ct);

        if (!resultValidated)
        {
            return ReplyAutoActResult.Skipped("no validated result");
        }

        try
        {
            await features.SetStatusAsync(
                feature.ProjectId, feature.Slug, PlanningFeatureStatus.Done,
                AuditLine(notification.Id), requireHumanTap: true, ct);
        }
        catch (PlanningException ex) when (
            ex.Code is PlanningFailureCode.FeatureDoneTapRequired
                or PlanningFailureCode.FeatureResultRequired
                or PlanningFailureCode.InvalidFeatureTransition)
        {
            return ReplyAutoActResult.Skipped(ex.Message);
        }

        logger.LogInformation(
            "auto-act closed feature {FeatureSlug} done from human reply {NotificationId}",
            feature.Slug, notification.Id);

        await NotifyAuthorAsync(
            notification,
            $"auto-act: feature '{feature.Slug}' closed done by your reply",
            ct);

        return ReplyAutoActResult.Performed("feature closed done");
    }

    private async Task NotifyAuthorAsync(
        AgentNotification notification, string text, CancellationToken ct)
    {
        db.AgentNotifications.Add(AgentNotification.Create(
            NotificationKind.Info,
            text,
            notification.ProjectId,
            notification.FeatureSlug,
            notification.Tab,
            notification.Agent));
        await db.SaveChangesAsync(ct);
    }

    private async Task<PlanningArtifact?> ResolveDocumentAsync(Guid featureId, string? tab, CancellationToken ct)
    {
        PlanningArtifactKind? kind = tab switch
        {
            "spec" => PlanningArtifactKind.Spec,
            "plan" => PlanningArtifactKind.Plan,
            "result" => PlanningArtifactKind.Result,
            _ => null,
        };

        if (kind is null)
        {
            return null;
        }

        return await db.PlanningArtifacts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.FeatureId == featureId && a.Kind == kind, ct);
    }

    private static string AuditLine(Guid notificationId) => $"[auto-act] by human reply {notificationId}";
}
