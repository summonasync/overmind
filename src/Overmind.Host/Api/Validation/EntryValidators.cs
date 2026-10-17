using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Validation;

public sealed class CreateEntryRequestValidator : AbstractValidator<CreateEntryRequest>
{
    internal const string CommaRule = "Tag names cannot contain commas: entry files list tags comma-separated.";

    public CreateEntryRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Content).NotEmpty();
        RuleFor(x => x.Tags)
            .ForEach(tag => tag.NotEmpty().Must(name => name is null || !name.Contains(',')).WithMessage(CommaRule))
            .When(x => x.Tags is not null);
        RuleFor(x => x.SkillFiles)
            .NotEmpty()
            .WithMessage("Skill entries require at least one skill file.")
            .When(x => x.Type == EntryType.Skill);
        RuleForEach(x => x.SkillFiles)
            .ChildRules(files =>
            {
                files.RuleFor(f => f.RelativePath).NotEmpty();
            });
    }
}

public sealed class EntryAckHumanRequestValidator : AbstractValidator<EntryAckHumanRequest>
{
    public EntryAckHumanRequestValidator()
    {
        RuleFor(x => x.Alias).MaximumLength(EntryAck.MaxIdentityLength);
        RuleFor(x => x.Vendor).MaximumLength(EntryAck.MaxIdentityLength);
    }
}

public sealed class UpdateEntryRequestValidator : AbstractValidator<UpdateEntryRequest>
{
    public UpdateEntryRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Content).NotEmpty();
        RuleFor(x => x.Tags)
            .ForEach(tag => tag.NotEmpty().Must(name => name is null || !name.Contains(',')).WithMessage(CreateEntryRequestValidator.CommaRule))
            .When(x => x.Tags is not null);
        RuleForEach(x => x.SkillFiles)
            .ChildRules(files =>
            {
                files.RuleFor(f => f.RelativePath).NotEmpty();
            });
    }
}
