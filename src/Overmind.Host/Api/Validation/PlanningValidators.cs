using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Validation;

public sealed class CreatePlanningFeatureRequestValidator : AbstractValidator<CreatePlanningFeatureRequest>
{
    public CreatePlanningFeatureRequestValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(64);
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

        RuleFor(x => x)
            .Must(x => x.Title is not null || x.Body is not null || x.Priority is not null)
            .WithMessage("Provide at least one editable field: title, body, priority.");

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
    }
}

public sealed class CreatePlanningArtifactRequestValidator : AbstractValidator<CreatePlanningArtifactRequest>
{
    public CreatePlanningArtifactRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty();
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0).When(x => x.Order.HasValue);
        RuleFor(x => x.Brief).MaximumLength(500).Must(b => !b!.Contains('\r') && !b.Contains('\n'))
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
        RuleFor(x => x.Brief).MaximumLength(500).Must(b => !b!.Contains('\r') && !b.Contains('\n'))
            .WithMessage("Brief must be a single line.")
            .When(x => x.Brief is not null);
    }
}

public sealed class CreatePlanningTasksRequestValidator : AbstractValidator<CreatePlanningTasksRequest>
{
    public CreatePlanningTasksRequestValidator()
    {
        RuleFor(x => x.Tasks).NotEmpty().WithMessage("A task batch needs at least one item.");
        RuleForEach(x => x.Tasks).NotNull().WithMessage("Each task item must be an object.");
        RuleForEach(x => x.Tasks).SetValidator(new PlanningTaskUpsertRequestValidator());
    }
}

public sealed class PlanningTaskUpsertRequestValidator : AbstractValidator<PlanningTaskUpsertRequest>
{
    public PlanningTaskUpsertRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty();
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0).When(x => x.Order.HasValue);
        RuleFor(x => x.Brief).MaximumLength(500).Must(b => !b!.Contains('\r') && !b.Contains('\n'))
            .WithMessage("Brief must be a single line.")
            .When(x => x.Brief is not null);
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

public sealed class AppendPlanningLogRequestValidator : AbstractValidator<AppendPlanningLogRequest>
{
    public AppendPlanningLogRequestValidator()
    {
        RuleFor(x => x.Note).NotEmpty();
    }
}

public sealed class DeclinePlanningArtifactRequestValidator : AbstractValidator<DeclinePlanningArtifactRequest>
{
    public DeclinePlanningArtifactRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500).Must(r => !r!.Contains('\r') && !r.Contains('\n'))
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
