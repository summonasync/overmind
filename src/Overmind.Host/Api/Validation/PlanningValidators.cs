using FluentValidation;
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

public sealed class CreatePlanningArtifactRequestValidator : AbstractValidator<CreatePlanningArtifactRequest>
{
    public CreatePlanningArtifactRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty();
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0).When(x => x.Order.HasValue);
        RuleFor(x => x.Brief).MaximumLength(500).Must(b => !b!.Contains('\r') && !b.Contains('\n'))
            .WithMessage("Brief must be a single line.")
            .When(x => x.Brief is not null);
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

public sealed class AddPlanningFeatureDependencyRequestValidator : AbstractValidator<AddPlanningFeatureDependencyRequest>
{
    public AddPlanningFeatureDependencyRequestValidator()
    {
        RuleFor(x => x.BlockedBy).NotEmpty().MaximumLength(64);
    }
}
