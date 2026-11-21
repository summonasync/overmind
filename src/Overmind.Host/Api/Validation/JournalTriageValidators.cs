using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Validation;

public sealed class PromoteNoteToFeatureRequestValidator : AbstractValidator<PromoteNoteToFeatureRequest>
{
    public PromoteNoteToFeatureRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Body).NotEmpty();
    }
}

public sealed class PromoteNoteToTaskRequestValidator : AbstractValidator<PromoteNoteToTaskRequest>
{
    public PromoteNoteToTaskRequestValidator()
    {
        RuleFor(x => x.FeatureId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty();
        RuleFor(x => x.Brief)
            .MaximumLength(PlanningArtifact.BriefMaxLength)
            .Must(b => !b!.Contains('\r') && !b.Contains('\n'))
            .When(x => x.Brief is not null);
    }
}
