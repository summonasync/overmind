using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Validation;

public sealed class PostWaveMessageRequestValidator : AbstractValidator<PostWaveMessageRequest>
{
    public PostWaveMessageRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty()
            .WithMessage("Text must not be empty.");
    }
}

public sealed class PostWaveMessageHumanRequestValidator : AbstractValidator<PostWaveMessageHumanRequest>
{
    public PostWaveMessageHumanRequestValidator()
    {
        RuleFor(x => x.AuthorAlias).NotEmpty().MaximumLength(WaveMessage.MaxIdentityLength);
        RuleFor(x => x.Text).NotEmpty()
            .WithMessage("Text must not be empty.");
    }
}
