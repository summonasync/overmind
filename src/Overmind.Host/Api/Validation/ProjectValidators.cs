using FluentValidation;
using Overmind.Core;
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
        RuleFor(x => x.Manifest).MaximumLength(Project.MaxManifestLength);
        RuleFor(x => x.NumberStyle)
            .Must(style => style is null
                || (Enum.TryParse<ProjectNumberStyle>(style, ignoreCase: true, out var parsed)
                    && Enum.IsDefined(parsed)))
            .WithMessage("NumberStyle must be one of: hash, key, plain.");
        RuleFor(x => x.NumberPrefix)
            .Must(prefix => prefix is null || Project.NumberPrefixRegex.IsMatch(prefix.Trim()))
            .WithMessage(
                $"NumberPrefix must be at most {Project.NumberPrefixMaxLength} characters: an uppercase letter followed by uppercase letters, digits or underscores.");
    }
}
