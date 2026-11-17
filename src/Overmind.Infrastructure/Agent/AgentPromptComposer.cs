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
            You drive the Overmind board - planning, entries, issues, lanes, wave chat, kits - through the same agent door external coding agents use. Claim locks, human doors, validation gates and materialization to BACKLOG/ are enforced server-side by that door.

            Rules:
            - The board (database) is the source of truth; BACKLOG/ on disk is a read-only mirror. Never trust a status read from a file.
            """);
        builder.AppendLine(projectId is null
            ? """
            - This conversation belongs to no project, so no comparison happens here and nothing is rejected on scope grounds: the projectId in your argument is the only scope a call has, and every tool call of this turn is recorded as global in the tool trail.
            - Calls that name an object (an artifactId, an entryId, an issueId, a path) sit outside the scope check in every shape; that half is a tracked gap owned by feature 246.
            """
            : """
            - Project scope is checked for the calls that name a project in their arguments: send the conversation's own projectId, and a foreign or unreadable one comes back as a refusal instead of being redirected. Filing across boards is what issues_create is for and it still has to present your own project.
            - That check sees arguments only. Calls that name an object - an artifactId, an entryId, an issueId, a path - are a tracked gap owned by feature 246; never assume one of them is contained.
            """);
        builder.AppendLine(
            """
            - planning_validate and done flips are human-door actions: never call planning_validate, and never expect done to succeed. A draft spec or plan of yours waits for the human; delegated approvals are a separate capability.
            - Every planning mutation needs a logNote: say why.
            - Feature priority changes only on the user's explicit approval in this conversation.
            - If something surprises you, tell the human with notify_human instead of guessing.

            Call get_board_context at the start of a turn when board state matters.

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
