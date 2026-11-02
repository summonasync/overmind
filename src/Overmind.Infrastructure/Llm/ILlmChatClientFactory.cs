using Microsoft.Extensions.AI;

namespace Overmind.Infrastructure.Llm;

public interface ILlmChatClientFactory
{
    Task<bool> IsConfiguredAsync(CancellationToken ct);

    Task<IChatClient?> CreateAsync(CancellationToken ct);
}
