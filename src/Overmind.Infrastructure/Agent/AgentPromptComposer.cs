using System.Text;
using Microsoft.Extensions.AI;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;

namespace Overmind.Infrastructure.Agent;

public sealed class AgentPromptComposer(IAgentRunSettingsProvider settingsProvider) : IAgentPromptComposer
{
    public const int MaxHistoryMessages = 24;
    public const string InstructionsHeader = "Owner instructions (standing rules from the human owner):";

    public async Task<IReadOnlyList<ChatMessage>> ComposeAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        CancellationToken ct)
    {
        var settings = await settingsProvider.GetAsync(ct);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, await ComposeSystemPromptAsync(settings, conversation.ProjectId, ct))
        };

        foreach (var row in history.Where(m => m.Role is ConversationMessage.UserRole or ConversationMessage.AssistantRole)
                     .TakeLast(MaxHistoryMessages))
        {
            messages.Add(new ChatMessage(
                row.Role == ConversationMessage.UserRole ? ChatRole.User : ChatRole.Assistant,
                row.Content));
        }

        messages.Add(new ChatMessage(ChatRole.User, userText));
        return messages;
    }

    private async Task<string> ComposeSystemPromptAsync(AgentRunSettings settings, Guid? projectId, CancellationToken ct)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            $"You are {settings.Alias}, the Overmind embedded management agent running inside the Overmind Host. Today is {DateTime.UtcNow:yyyy-MM-dd}.");
        if (projectId is { } scopedProject)
        {
            builder.AppendLine($"The conversation is scoped to project {scopedProject}.");
        }

        builder.AppendLine();
        builder.AppendLine(
            """
            You drive the Overmind board - planning, entries, issues, lanes, wave chat, kits - through the same agent door external coding agents use. Claim locks, human doors, validation gates and materialization to BACKLOG/ are enforced server-side on every call; there is no bypass.

            Rules:
            - The board (database) is the source of truth; BACKLOG/ on disk is a read-only mirror. Never trust a status read from a file.
            - Always pass projectId explicitly; resolve it from the snapshot below or list_projects.
            - planning_validate and done flips are human-door actions: never call planning_validate, and never expect done to succeed. A draft spec or plan of yours waits for the human; delegated approvals are a separate capability.
            - Every planning mutation needs a logNote: say why.
            - Feature priority changes only on the user's explicit approval in this conversation.
            - If something surprises you, tell the human with notify_human instead of guessing.
            """);
        AppendOwnerInstructions(builder, settings.Instructions);
        return builder.ToString();
    }

    private static void AppendOwnerInstructions(StringBuilder builder, string instructions)
    {
        var text = instructions.Trim();
        if (text.Length == 0)
        {
            return;
        }

        if (text.Length > SettingDefaults.AgentInstructionsMaxLength)
        {
            text = text[..SettingDefaults.AgentInstructionsMaxLength];
        }

        builder.AppendLine();
        builder.AppendLine(InstructionsHeader);
        builder.Append(text);
    }
}
