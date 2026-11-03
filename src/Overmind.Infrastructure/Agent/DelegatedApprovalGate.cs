using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Agent;

public sealed class DelegatedApprovalGate(OvermindDbContext db, ApprovalPolicy policy) : IAgentDelegationGate
{
    public const string ProvenanceMarker = "[delegated by owner via chat]";
    private const int MaxExcerptLength = 80;

    public async Task<bool> IsEnabledAsync(Guid? projectId, CancellationToken ct = default)
    {
        if (projectId is null)
        {
            return false;
        }

        return await policy.ResolveDelegateApprovalsAsync(projectId.Value, ct);
    }

    public async Task<DelegatedApprovalGrant?> AuthorizeAsync(
        Guid projectId,
        Guid? callerSessionId,
        Guid expectedSessionId,
        Guid? instructionMessageId,
        CancellationToken ct = default)
    {
        if (!await IsEnabledAsync(projectId, ct) ||
            expectedSessionId == Guid.Empty ||
            callerSessionId is not { } caller || caller != expectedSessionId ||
            instructionMessageId is not { } messageId)
        {
            return null;
        }

        var sessionRole = await db.AgentSessions
            .Where(s => s.Id == caller)
            .Select(s => (string?)s.Role)
            .FirstOrDefaultAsync(ct);
        if (!string.Equals(sessionRole, AgentSession.EmbeddedRole, StringComparison.Ordinal))
        {
            return null;
        }

        var instruction = await db.ConversationMessages
            .Where(m => m.Id == messageId &&
                db.Conversations.Any(c => c.Id == m.ConversationId && c.ProjectId == projectId) &&
                m.Role == ConversationMessage.UserRole)
            .Select(m => new { m.Id, m.Content })
            .FirstOrDefaultAsync(ct);

        return instruction is null
            ? null
            : new DelegatedApprovalGrant(instruction.Id, Excerpt(instruction.Content));
    }

    public static string ProvenanceLine(DelegatedApprovalGrant grant) =>
        $"{ProvenanceMarker} owner message {grant.InstructionMessageId} \"{grant.InstructionExcerpt}\"";

    private static string Excerpt(string content)
    {
        var flattened = string.Join(
            ' ',
            content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (flattened.Length == 0)
        {
            return string.Empty;
        }

        return flattened.Length <= MaxExcerptLength
            ? flattened
            : flattened[..(MaxExcerptLength - 3)] + "...";
    }
}
