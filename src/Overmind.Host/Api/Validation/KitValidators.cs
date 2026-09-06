using FluentValidation;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Validation;

public sealed class CreateKitRequestValidator : AbstractValidator<CreateKitRequest>
{
    public CreateKitRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}

public sealed class UpdateKitRequestValidator : AbstractValidator<UpdateKitRequest>
{
    public UpdateKitRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
