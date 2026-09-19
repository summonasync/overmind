using FluentValidation;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Validation;

public sealed class OnboardRequestValidator : AbstractValidator<OnboardRequest>
{
    public OnboardRequestValidator()
    {
        RuleFor(x => x.Cwd).NotEmpty().When(x => x.ProjectId is null);
    }
}
