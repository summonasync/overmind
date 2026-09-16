using FluentValidation;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Validation;

public sealed class KitScanRequestValidator : AbstractValidator<KitScanRequest>
{
    public KitScanRequestValidator()
    {
        RuleFor(x => x.Path).NotEmpty();
    }
}

public sealed class KitImportRequestValidator : AbstractValidator<KitImportRequest>
{
    public KitImportRequestValidator()
    {
        RuleFor(x => x.Path).NotEmpty();
        RuleFor(x => x.Kits).NotEmpty();
        RuleForEach(x => x.Kits).ChildRules(selection =>
        {
            selection.RuleFor(x => x.RelativePath).NotNull();
            selection.RuleFor(x => x.Name)
                .Must(name => !string.IsNullOrWhiteSpace(name))
                .WithMessage("Kit name is required.")
                .MaximumLength(200);
        });
    }
}
