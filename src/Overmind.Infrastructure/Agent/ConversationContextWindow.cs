using Overmind.Core;

namespace Overmind.Infrastructure.Agent;

public static class ConversationContextWindow
{
    public const int PerRoleLimit = 10;
    public const int MaxMessages = PerRoleLimit * 2;

    public static IReadOnlyList<ConversationMessage> Apply(IReadOnlyList<ConversationMessage> history)
    {
        var dialogue = history
            .Where(m => m.Role is ConversationMessage.UserRole or ConversationMessage.AssistantRole)
            .ToList();

        if (dialogue.Count <= MaxMessages)
        {
            return dialogue;
        }

        var kept = dialogue
            .Where(m => m.Role == ConversationMessage.UserRole)
            .TakeLast(PerRoleLimit)
            .Concat(dialogue.Where(m => m.Role == ConversationMessage.AssistantRole).TakeLast(PerRoleLimit))
            .ToHashSet();

        return dialogue.Where(kept.Contains).ToList();
    }
}
