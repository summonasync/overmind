using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Validation;

public sealed class CreatePlanningFeatureRequestValidator : AbstractValidator<CreatePlanningFeatureRequest>
{
    public CreatePlanningFeatureRequestValidator()
    {
        RuleFor(x => x.Slug).MaximumLength(64);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdatePlanningFeatureRequestValidator : AbstractValidator<UpdatePlanningFeatureRequest>
{
    public UpdatePlanningFeatureRequestValidator()
    {
        When(x => x.Title is not null, () =>
        {
            RuleFor(x => x.Title).MaximumLength(200);
        });

        RuleFor(x => x.Lane)
            .MaximumLength(PlanningLane.MaxNameLength)
            .When(x => x.Lane is not null);

        RuleFor(x => x.CodeSurfaces)
            .Must(s => s!.Length <= PlanningFeature.MaxCodeSurfaces)
            .WithMessage(CodeSurfaceRules.SetSizeMessage)
            .When(x => x.CodeSurfaces is not null);

        RuleForEach(x => x.CodeSurfaces)
            .Must(CodeSurfaceRules.IsValidEntry)
            .WithMessage(CodeSurfaceRules.EntryMessage)
            .When(x => x.CodeSurfaces is not null);
    }
}

public sealed class UpdatePlanningFeatureByAgentRequestValidator
    : AbstractValidator<UpdatePlanningFeatureByAgentRequest>
{
    public UpdatePlanningFeatureByAgentRequestValidator()
    {
        var allowedPriorities = string.Join(
            ", ", Enum.GetNames<FeaturePriority>().Select(n => n.ToLowerInvariant()));

        RuleFor(x => x.LogNote).NotEmpty();

        RuleFor(x => x.Title)
            .Must(t => !string.IsNullOrWhiteSpace(t))
            .WithMessage("Title must be non-empty text when provided.")
            .When(x => x.Title is not null);

        RuleFor(x => x.Body)
            .Must(b => !string.IsNullOrWhiteSpace(b))
            .WithMessage("Body must be non-empty text when provided.")
            .When(x => x.Body is not null);

        RuleFor(x => x.Priority)
            .Must(p => Enum.TryParse(p, ignoreCase: true, out FeaturePriority parsed) && Enum.IsDefined(parsed))
            .WithMessage($"Must be one of: {allowedPriorities}.")
            .When(x => x.Priority is not null);

        RuleFor(x => x.Lane)
            .MaximumLength(PlanningLane.MaxNameLength)
            .When(x => x.Lane is not null);

        RuleFor(x => x.CodeSurfaces)
            .Must(s => s!.Length <= PlanningFeature.MaxCodeSurfaces)
            .WithMessage(CodeSurfaceRules.SetSizeMessage)
            .When(x => x.CodeSurfaces is not null);

        RuleForEach(x => x.CodeSurfaces)
            .Must(CodeSurfaceRules.IsValidEntry)
            .WithMessage(CodeSurfaceRules.EntryMessage)
            .When(x => x.CodeSurfaces is not null);
    }
}

public sealed class PatchPlanningFeatureRequestValidator : AbstractValidator<PatchPlanningFeatureRequest>
{
    public PatchPlanningFeatureRequestValidator()
    {
        RuleFor(x => x.Section).NotEmpty()
            .Must(PlanningSections.IsNameParseable)
            .WithMessage(
                "Section must be a lowercase tag name: letters, digits and hyphens, starting with a letter " +
                "(e.g. 'goal', 'anti-scope', 'my-tag') — an unparseable name could never be found again.");

        RuleFor(x => x.LogNote).NotEmpty();

        RuleFor(x => x.Reason).NotEmpty()
            .When(x => x.Remove);

        RuleFor(x => x.Content).Null()
            .WithMessage("content must be omitted when remove=true — the tombstone line replaces the section and carries the reason only.")
            .When(x => x.Remove);

        RuleFor(x => x.Content)
            .Must(c => !string.IsNullOrWhiteSpace(c))
            .WithMessage("content must be non-empty text when provided — omit it to insert a declared-empty section.")
            .When(x => !x.Remove && x.Content is not null);
    }
}

public sealed class SetLanesRequestValidator : AbstractValidator<SetLanesRequest>
{
    public SetLanesRequestValidator()
    {
        RuleFor(x => x.Lanes).NotNull()
            .WithMessage("The roster must be an array of lane objects; an empty array is a no-op — the roster is reshaped, never cleared.");

        RuleForEach(x => x.Lanes).ChildRules(lane =>
        {
            lane.RuleFor(l => l.Name).NotEmpty().MaximumLength(PlanningLane.MaxNameLength);
            // Contour length is soft-capped at the doors (266); LaneService holds the ceiling backstop.
            lane.RuleFor(l => l.BoundAlias).MaximumLength(PlanningLane.MaxBoundAliasLength);
            lane.RuleFor(l => l.SerialOrder).GreaterThanOrEqualTo(0).When(l => l.SerialOrder.HasValue);
            lane.RuleFor(l => l.Priority).InclusiveBetween(0, PlanningLane.MaxPriority)
                .When(l => l.Priority.HasValue);
            lane.RuleFor(l => l.LiveSurfaces).Must(LiveSurfacesValid)
                .WithMessage("liveSurfaces entries are invalid: " +
                             CodeSurfaceRules.EntryMessage + " " + CodeSurfaceRules.LiveSurfaceOwnerMessage)
                .When(l => l.LiveSurfaces is not null);
        });
    }

    internal static bool LiveSurfacesValid(LiveSurfaceRequest[]? surfaces)
    {
        if (surfaces is null)
        {
            return true;
        }

        if (surfaces.Length > PlanningLane.MaxLiveSurfaces)
        {
            return false;
        }

        return surfaces.All(s =>
            s is not null &&
            RepoGlobRule.IsValid(s.Glob) &&
            !string.IsNullOrWhiteSpace(s.Owner) &&
            s.Owner.Trim().Length <= PlanningLane.MaxLiveSurfaceOwnerLength);
    }
}

public sealed class UpdateLaneRequestValidator : AbstractValidator<UpdateLaneRequest>
{
    public UpdateLaneRequestValidator()
    {
        // Contour length is soft-capped at the doors (266); LaneService holds the ceiling backstop.
        RuleFor(x => x.BoundAlias).MaximumLength(PlanningLane.MaxBoundAliasLength);
        RuleFor(x => x.SerialOrder).GreaterThanOrEqualTo(0).When(x => x.SerialOrder.HasValue);
        RuleFor(x => x.Priority).InclusiveBetween(0, PlanningLane.MaxPriority).When(x => x.Priority.HasValue);
        RuleFor(x => x.LiveSurfaces).Must(SetLanesRequestValidator.LiveSurfacesValid)
            .WithMessage("liveSurfaces entries are invalid: " +
                         CodeSurfaceRules.EntryMessage + " " + CodeSurfaceRules.LiveSurfaceOwnerMessage)
            .When(x => x.LiveSurfaces is not null);
    }
}

public sealed class CreateLaneRequestValidator : AbstractValidator<CreateLaneRequest>
{
    public CreateLaneRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(PlanningLane.MaxNameLength);
        // Contour length is soft-capped at the doors (266); LaneService holds the ceiling backstop.
        RuleFor(x => x.BoundAlias).MaximumLength(PlanningLane.MaxBoundAliasLength);
        RuleFor(x => x.Priority).InclusiveBetween(0, PlanningLane.MaxPriority).When(x => x.Priority.HasValue);
    }
}

public sealed class CreatePlanningArtifactRequestValidator : AbstractValidator<CreatePlanningArtifactRequest>
{
    public CreatePlanningArtifactRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().WithMessage(
            "Body is required — send the full markdown body, " +
            "e.g. { \"kind\": \"task\", \"body\": \"<goal>Fix the login redirect.</goal>\" }.");
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0).When(x => x.Order.HasValue);
        // Brief length is soft-capped at the doors (266); only the single-line rule stays hard.
        RuleFor(x => x.Brief).Must(b => !b!.Contains('\r') && !b.Contains('\n'))
            .WithMessage("Brief must be a single line.")
            .When(x => x.Brief is not null);
        RuleForEach(x => x.BlockedBy).GreaterThanOrEqualTo(0)
            .WithMessage("blockedBy must contain non-negative task orders.");
    }
}

public sealed class UpdatePlanningArtifactRequestValidator : AbstractValidator<UpdatePlanningArtifactRequest>
{
    public UpdatePlanningArtifactRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty();
        // Brief length is soft-capped at the doors (266); only the single-line rule stays hard.
        RuleFor(x => x.Brief).Must(b => !b!.Contains('\r') && !b.Contains('\n'))
            .WithMessage("Brief must be a single line.")
            .When(x => x.Brief is not null);
    }
}

public sealed class CreatePlanningTasksRequestValidator : AbstractValidator<CreatePlanningTasksRequest>
{
    public CreatePlanningTasksRequestValidator()
    {
        RuleFor(x => x.Tasks).NotEmpty().WithMessage(
            "A task batch needs at least one item — the batch rides the 'tasks' array: " +
            "{ \"tasks\": [ { \"body\": \"<goal>Fix the login redirect.</goal>\", \"order\": 0 } ] }.");
        RuleForEach(x => x.Tasks).NotNull().WithMessage(
            "Each task item must be an object: { \"body\": \"<goal>…</goal>\", \"order\": 0 }.");
        RuleForEach(x => x.Tasks).SetValidator(new PlanningTaskUpsertRequestValidator());
    }
}

public sealed class PlanningTaskUpsertRequestValidator : AbstractValidator<PlanningTaskUpsertRequest>
{
    public PlanningTaskUpsertRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().WithMessage(
            "Body is required on every task item — e.g. { \"body\": \"<goal>Fix the login redirect.</goal>\", \"order\": 0 }.");
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0).When(x => x.Order.HasValue);
        // Brief length is soft-capped at the doors (266); only the single-line rule stays hard.
        RuleFor(x => x.Brief).Must(b => !b!.Contains('\r') && !b.Contains('\n'))
            .WithMessage("Brief must be a single line.")
            .When(x => x.Brief is not null);
    }
}

public sealed class SetPlanningFeatureCodePinRequestValidator
    : AbstractValidator<SetPlanningFeatureCodePinRequest>
{
    public SetPlanningFeatureCodePinRequestValidator()
    {
        RuleFor(x => x.Branch).MaximumLength(200).Must(b => !b!.Contains('\r') && !b.Contains('\n'))
            .WithMessage("Branch must be a single line.")
            .When(x => x.Branch is not null);

        RuleFor(x => x.WorktreePath).MaximumLength(400).Must(p => !p!.Contains('\r') && !p.Contains('\n'))
            .WithMessage("WorktreePath must be a single line.")
            .When(x => x.WorktreePath is not null);
    }
}

public sealed class SetPlanningFeatureStatusRequestValidator : AbstractValidator<SetPlanningFeatureStatusRequest>
{
    public SetPlanningFeatureStatusRequestValidator()
    {
        RuleFor(x => x.LogNote).NotEmpty();
    }
}

public sealed class SetPlanningArtifactStatusRequestValidator : AbstractValidator<SetPlanningArtifactStatusRequest>
{
    public SetPlanningArtifactStatusRequestValidator()
    {
        RuleFor(x => x.LogNote).NotEmpty();
    }
}

public sealed class PlanningBatchRequestValidator : AbstractValidator<PlanningBatchRequest>
{
    public PlanningBatchRequestValidator()
    {
        RuleFor(x => x.Ops).NotNull()
            .WithMessage("A batch must carry ops[]: the request without it would commit nothing.");
        RuleFor(x => x.Ops).NotEmpty()
            .WithMessage("A batch must carry at least one op: an empty ops[] would commit nothing and still spend a materialization pass.");
        RuleForEach(x => x.Ops).NotNull()
            .WithMessage("Every ops[] entry must be an object carrying an op name.");
    }
}

public sealed class AppendPlanningLogRequestValidator : AbstractValidator<AppendPlanningLogRequest>
{
    public AppendPlanningLogRequestValidator()
    {
        RuleFor(x => x.Note).NotEmpty();
    }
}

public sealed class EditPlanningLogRequestValidator : AbstractValidator<EditPlanningLogRequest>
{
    public EditPlanningLogRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty();
    }
}

public sealed class DeclinePlanningArtifactRequestValidator : AbstractValidator<DeclinePlanningArtifactRequest>
{
    public DeclinePlanningArtifactRequestValidator()
    {
        // Reason length is soft-capped at the door (266); the single-line rule stays hard.
        RuleFor(x => x.Reason).Must(r => !r!.Contains('\r') && !r.Contains('\n'))
            .WithMessage("Decline reason must be a single line.")
            .When(x => x.Reason is not null);
    }
}

public sealed class ImportPlanningFeatureRequestValidator : AbstractValidator<ImportPlanningFeatureRequest>
{
    public ImportPlanningFeatureRequestValidator()
    {
        RuleFor(x => x.Path).NotEmpty();
        RuleFor(x => x.Path).Must(path => Path.IsPathFullyQualified(path))
            .When(x => !string.IsNullOrWhiteSpace(x.Path))
            .WithMessage("Path must be an absolute folder under the project root, e.g. /repo/BACKLOG/058-agent-side-import.");
    }
}

public sealed class PlanningImportFolderRequestValidator : AbstractValidator<PlanningImportFolderRequest>
{
    public PlanningImportFolderRequestValidator()
    {
        RuleFor(x => x.Folder).NotEmpty();
        RuleFor(x => x.Folder).Must(BeASingleFolderName)
            .When(x => !string.IsNullOrWhiteSpace(x.Folder))
            .WithMessage("Folder must be a single BACKLOG folder name, e.g. 058-agent-side-import.");
    }

    private static bool BeASingleFolderName(string folder) =>
        folder.Trim() is { Length: > 0 } name &&
        !name.Contains('/') &&
        !name.Contains('\\') &&
        name is not ("." or "..");
}

public sealed class PlanningExportRequestValidator : AbstractValidator<PlanningExportRequest>
{
    public PlanningExportRequestValidator()
    {
        RuleFor(x => x.TargetPath).NotEmpty();
        RuleFor(x => x.TargetPath).Must(Path.IsPathFullyQualified)
            .When(x => !string.IsNullOrWhiteSpace(x.TargetPath))
            .WithMessage("TargetPath must be an absolute directory path, e.g. /repo/docs/planning-snapshot.");
    }
}

public sealed class AddPlanningFeatureDependencyRequestValidator : AbstractValidator<AddPlanningFeatureDependencyRequest>
{
    public AddPlanningFeatureDependencyRequestValidator()
    {
        RuleFor(x => x.BlockedBy).NotEmpty().MaximumLength(64);
    }
}

public sealed class PlanningQueueMoveRequestValidator : AbstractValidator<PlanningQueueMoveRequest>
{
    public PlanningQueueMoveRequestValidator()
    {
        RuleFor(x => x.Move).NotEmpty()
            .Must(m => Normalize(m) is "up" or "down" or "first" or "last" or "toposition")
            .WithMessage("Must be one of: up, down, first, last, toPosition.");

        RuleFor(x => x.Position).NotNull()
            .WithMessage("position is required when move is 'toPosition'; the queue-move contract carries position only for toPosition.")
            .When(x => Normalize(x.Move) == "toposition");

        RuleFor(x => x.Position).Null()
            .WithMessage("position is only allowed when move is 'toPosition'; omit it for the other moves.")
            .When(x => Normalize(x.Move) is not null and not "toposition");

        RuleFor(x => x.Position).GreaterThan(0)
            .WithMessage("position must be a positive int (1-based).")
            .When(x => x.Position.HasValue);
    }

    private static string? Normalize(string? move) => move?.Trim().ToLowerInvariant();
}

public sealed class PlanningQueueReorderRequestValidator : AbstractValidator<PlanningQueueReorderRequest>
{
    public PlanningQueueReorderRequestValidator()
    {
        RuleFor(x => x.Priority).NotEmpty();
        RuleFor(x => x.Slugs).NotNull()
            .WithMessage("A reorder carries the full ordered slug list of one priority band.");
        RuleFor(x => x.Slugs).NotEmpty()
            .WithMessage("A reorder carries the full ordered slug list of one priority band.")
            .When(x => x.Slugs is not null);
        RuleForEach(x => x.Slugs).NotEmpty()
            .WithMessage("Every slug in the reorder list must be non-empty.");
    }
}

public sealed class AppendPlanningDirectiveRequestValidator : AbstractValidator<AppendPlanningDirectiveRequest>
{
    public AppendPlanningDirectiveRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty();
    }
}

public sealed class AppendPlanningDirectiveHumanRequestValidator : AbstractValidator<AppendPlanningDirectiveHumanRequest>
{
    public AppendPlanningDirectiveHumanRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty();
        RuleFor(x => x.Alias).MaximumLength(PlanningDirective.MaxIdentityLength);
        RuleFor(x => x.Vendor).MaximumLength(PlanningDirective.MaxIdentityLength);
    }
}
