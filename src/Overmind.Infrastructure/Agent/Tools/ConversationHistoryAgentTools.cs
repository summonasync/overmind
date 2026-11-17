using System.Text.Json;
using Overmind.Core;

namespace Overmind.Infrastructure.Agent.Tools;

public static class ConversationHistoryAgentTools
{
    public const int DefaultPageLimit = 20;
    public const int MaxPageLimit = 50;
    public const int MaxRowContentLength = 2000;

    public static IReadOnlyList<AgentToolSpec> Build(ConversationMessageStore messages)
    {
        return [PageSpec(messages)];
    }

    private static AgentToolSpec PageSpec(ConversationMessageStore messages) => new(
        "conversation_history",
        "Pages backward through the stored messages of the CURRENT conversation - use it when the owner asks " +
        "to look further back than the context window reaches. Args: before (optional message id cursor; the " +
        "page lists messages older than it), limit (optional, default 20, capped at 50). Long messages are " +
        "clipped in the listing.",
        """
        {"type":"object","properties":{"before":{"type":"string"},"limit":{"type":"integer"}}}
        """,
        (_, _) => throw new NotSupportedException(
            "conversation_history is dispatched with the current conversation scope, never the plain handler."),
        AgentToolCategory.Standard,
        null,
        async (args, conversationId, ct) =>
        {
            var limit = Math.Clamp(AgentToolArgs.OptionalInt(args, "limit") ?? DefaultPageLimit, 1, MaxPageLimit);
            var before = await CursorOf(messages, args, conversationId, ct);
            var page = await messages.ListPageAsync(conversationId, limit, before, ct);
            return JsonSerializer.SerializeToElement(new
            {
                conversationId,
                count = page.Count,
                items = page.Select(message => new
                {
                    id = message.Id,
                    role = message.Role,
                    content = Clip(message.Content),
                    createdAt = message.CreatedAt,
                }),
            });
        },
        Scope: AgentToolScope.SessionScoped);

    private static async Task<DateTime?> CursorOf(
        ConversationMessageStore messages, JsonElement args, Guid conversationId, CancellationToken ct)
    {
        if (AgentToolArgs.OptionalGuid(args, "before") is not { } messageId)
        {
            return null;
        }

        return await messages.FindAsync(conversationId, messageId, ct) is { } cursor
            ? cursor.CreatedAt
            : throw new ArgumentException(
                $"Argument 'before' must name a message of the current conversation; '{messageId}' was not found.");
    }

    private static string Clip(string content) =>
        content.Length <= MaxRowContentLength ? content : content[..MaxRowContentLength] + "…";
}
