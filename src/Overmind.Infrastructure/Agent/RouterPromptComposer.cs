using System.Text;
using Microsoft.Extensions.AI;
using Overmind.Core;
using Overmind.Infrastructure.Agent.Routes;

namespace Overmind.Infrastructure.Agent;

public sealed class RouterPromptComposer : IRouterPromptComposer
{
    public const int MaxHistoryMessages = 20;
    public const string ClassificationContract =
        """Reply with one line of JSON only: {"target":"chat","reason":"up to 12 words"} where target is either "chat" or "worker".""";

    public Task<IReadOnlyList<ChatMessage>> ComposeClassificationAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        IReadOnlyList<AgentRouteRecord> hotHead,
        string userText,
        CancellationToken ct)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, ComposeSystemPrompt(conversation, hotHead, ClassificationContract))
        };
        AppendDialogue(messages, history);
        messages.Add(new ChatMessage(ChatRole.User, userText));
        return Task.FromResult<IReadOnlyList<ChatMessage>>(messages);
    }

    public Task<IReadOnlyList<ChatMessage>> ComposeChatAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        IReadOnlyList<AgentRouteRecord> hotHead,
        string userText,
        CancellationToken ct)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, ComposeSystemPrompt(conversation, hotHead,
                "Answer the user directly from the dialogue. You have no tools and cannot touch Overmind; keep replies short and say so when the request needs the worker."))
        };
        AppendDialogue(messages, history);
        messages.Add(new ChatMessage(ChatRole.User, userText));
        return Task.FromResult<IReadOnlyList<ChatMessage>>(messages);
    }

    private static string ComposeSystemPrompt(Conversation conversation, IReadOnlyList<AgentRouteRecord> hotHead, string charterTail)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            $"You are the intent router of the Overmind embedded crew. Today is {DateTime.UtcNow:yyyy-MM-dd}.");
        if (conversation.ProjectId is { } scopedProject)
        {
            builder.AppendLine($"The conversation is scoped to project {scopedProject}.");
        }

        builder.AppendLine();
        builder.AppendLine(
            """
            Route every request: greetings, small talk and self-contained questions are yours to answer ("chat"); anything that would touch Overmind projects, the planning board, entries, kits, waves, notifications or files goes to "worker".
            """);
        AppendHotHead(builder, hotHead);
        builder.AppendLine();
        builder.AppendLine(charterTail);
        return builder.ToString();
    }

    private static void AppendHotHead(StringBuilder builder, IReadOnlyList<AgentRouteRecord> hotHead)
    {
        if (hotHead.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Learned routing table (route to the listed target when the request clearly matches a pattern):");
        var budget = RouterTokenBudget.MaxHotHeadChars;
        foreach (var route in hotHead)
        {
            var line = $"- \"{route.Pattern}\" -> {route.TargetAgent ?? RoutingTargets.Worker} ({route.Domain})";
            if (line.Length > budget)
            {
                break;
            }

            builder.AppendLine(line);
            budget -= line.Length;
        }
    }

    private static void AppendDialogue(List<ChatMessage> messages, IReadOnlyList<ConversationMessage> history)
    {
        foreach (var row in history
                     .Where(m => m.Role is ConversationMessage.UserRole or ConversationMessage.AssistantRole)
                     .TakeLast(MaxHistoryMessages))
        {
            messages.Add(new ChatMessage(
                row.Role == ConversationMessage.UserRole ? ChatRole.User : ChatRole.Assistant,
                row.Content));
        }
    }
}
