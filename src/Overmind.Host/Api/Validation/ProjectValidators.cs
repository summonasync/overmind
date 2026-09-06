using FluentValidation;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Validation;

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DiskPath)
            .NotEmpty()
            .Must(path => Path.IsPathFullyQualified(path))
            .WithMessage("DiskPath must be an absolute path.");
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DiskPath)
            .NotEmpty()
            .Must(path => Path.IsPathFullyQualified(path))
            .WithMessage("DiskPath must be an absolute path.");
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
