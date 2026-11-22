using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Validation;

public sealed class CreateEntryRequestValidator : AbstractValidator<CreateEntryRequest>
{
    internal const string CommaRule = "Tag names cannot contain commas: entry files list tags comma-separated.";

    public CreateEntryRequestValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty()
            .Unless(x => x.Type == EntryType.Journal);

        // The empty guid is a different input from JSON null and NotEmpty only owns the null: on a Guid? it
        // compares against default(Guid?), so a present-but-zero guid walks straight through it. Spec R2 lets a
        // journal thought lose its destination; no other type may, because an ownerless create is exactly what
        // the handler's reserved-container bootstrap answers.
        RuleFor(x => x.ProjectId)
            .Must(projectId => projectId is null || projectId.Value != Guid.Empty)
            .WithMessage("ProjectId cannot be the empty guid.")
            .Unless(x => x.Type == EntryType.Journal);
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
        RuleFor(x => x.Enforces)
            .Must(enforces => enforces != true)
            .WithMessage(EnforcesDecisionOnly)
            .When(x => x.Type != EntryType.Decision);
    }

    internal const string EnforcesDecisionOnly = "Only decision entries can carry the enforces flag.";
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

public sealed class EntryLiftRequestValidator : AbstractValidator<EntryLiftRequest>
{
    public EntryLiftRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty();
    }
}

public sealed class EntryAppendRequestValidator : AbstractValidator<EntryAppendRequest>
{
    public EntryAppendRequestValidator()
    {
        // Append text is soft-capped at the door (266) — whole inside the grace band, truncated beyond.
        RuleFor(x => x.Text).NotEmpty();
    }
}

public sealed class EntryResolveLinksRequestValidator : AbstractValidator<EntryResolveLinksRequest>
{
    public EntryResolveLinksRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty();
    }
}
