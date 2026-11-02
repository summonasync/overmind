using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Validation;

public sealed class CreateConversationRequestValidator : AbstractValidator<CreateConversationRequest>
{
    public CreateConversationRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(Conversation.MaxTitleLength);
    }
}

public sealed class RenameConversationRequestValidator : AbstractValidator<RenameConversationRequest>
{
    public RenameConversationRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Conversation.MaxTitleLength);
    }
}

public sealed class SendConversationMessageRequestValidator : AbstractValidator<SendConversationMessageRequest>
{
    public SendConversationMessageRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(ConversationMessage.MaxTextLength)
            .WithMessage($"Text must not exceed {ConversationMessage.MaxTextLength} characters.");
    }
}
