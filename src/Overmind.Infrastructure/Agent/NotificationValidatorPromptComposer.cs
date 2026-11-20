using System.Text;
using Microsoft.Extensions.AI;
using Overmind.Core;

namespace Overmind.Infrastructure.Agent;

public sealed class NotificationValidatorPromptComposer : IAgentPromptComposer
{
    public const int MaxHistoryMessages = 12;

    public Task<IReadOnlyList<ChatMessage>> ComposeAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        CancellationToken ct)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, ComposeSystemPrompt(conversation.ProjectId))
        };

        foreach (var row in history.Where(m => m.Role is ConversationMessage.UserRole or ConversationMessage.AssistantRole)
                     .TakeLast(MaxHistoryMessages))
        {
            messages.Add(new ChatMessage(
                row.Role == ConversationMessage.UserRole ? ChatRole.User : ChatRole.Assistant,
                row.Content));
        }

        messages.Add(new ChatMessage(ChatRole.User, userText));
        return Task.FromResult<IReadOnlyList<ChatMessage>>(messages);
    }

    private static string ComposeSystemPrompt(Guid? projectId)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            $"You are notification-validator, the Overmind notification validator running inside the Overmind Host. Today is {DateTime.UtcNow:yyyy-MM-dd}.");
        if (projectId is { } scopedProject)
        {
            builder.AppendLine($"The conversation is scoped to project {scopedProject}.");
        }

        builder.AppendLine();
        builder.AppendLine(
            """
            A telegram message from the human owner carries one instruction about a gate notification. Resolve the context and act or answer - your turn ends at the act or the report.

            Rules:
            - Call get_board_context first when the notification's project or artifact is not fully named in the instruction.
            """);
        builder.AppendLine(projectId is null
            ? """
            - This validator conversation belongs to no project, so no scope comparison happens on this turn and nothing is rejected on scope grounds: the projectId in a call's argument is the only scope it has, and every tool call of this turn is recorded as global in the tool trail.
            """
            : """
            - A projectId this turn sends is compared with the conversation's project, and a foreign or unreadable one comes back as a refusal instead of being redirected.
            """);
        builder.AppendLine(
            """
            - When the owner's reply approves the pending gate and delegated approvals are effectively on for the project, call planning_validate (or planning_set_feature_status for done flips) - the loop attaches the delegated instruction reference to your tool call automatically.
            - When delegated approvals are off, do not call planning_validate or planning_set_feature_status: say the human door in the Overmind UI stays open and stop.
            - When the reply is a question or a context request, answer it concisely from the tool results.
            - planning_validate and planning_set_feature_status address an artifact by id, so whether the host accepts one depends on the conversation's project owning that artifact (feature 246); on a project-less turn there is no owner to match, which is a pass-through, not a licence.
            - Every planning mutation needs a logNote: state the owner's intent.
            - You are not a chat buddy: no small talk, no promises of follow-ups; end the turn after the act or the answer.
            """);
        return builder.ToString();
    }
}
