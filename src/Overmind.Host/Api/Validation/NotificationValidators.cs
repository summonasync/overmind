using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Validation;

public sealed class CreateNotificationRequestValidator : AbstractValidator<CreateNotificationRequest>
{
    public CreateNotificationRequestValidator()
    {
        RuleFor(x => x.Kind).NotEmpty()
            .IsEnumName(typeof(NotificationKind), caseSensitive: false);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.FeatureSlug).MaximumLength(64)
            .When(x => x.FeatureSlug is not null);
        RuleFor(x => x.Tab)
            .Must(t => AgentNotification.AllowedTabs.Contains(t!.Trim().ToLowerInvariant()))
            .When(x => !string.IsNullOrWhiteSpace(x.Tab))
            .WithMessage($"Tab must be one of: {string.Join(", ", AgentNotification.AllowedTabs)}.");
    }
}

public sealed class ReplyNotificationRequestValidator : AbstractValidator<ReplyNotificationRequest>
{
    public ReplyNotificationRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
    }
}
