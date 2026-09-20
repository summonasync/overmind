using FluentValidation;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Validation;

public sealed class UpdateTagRequestValidator : AbstractValidator<UpdateTagRequest>
{
    public UpdateTagRequestValidator()
    {
        RuleFor(x => x.Color)
            .MaximumLength(16)
            .Matches("^#[0-9A-Fa-f]{6}\\z")
            .When(x => !string.IsNullOrEmpty(x.Color));
    }
}
