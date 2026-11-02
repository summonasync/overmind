using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Agent;

public sealed class ConversationStore
{
    private readonly OvermindDbContext _db;

    public ConversationStore(OvermindDbContext db)
    {
        _db = db;
    }

    public async Task<Conversation> CreateAsync(Guid? projectId, string? title, CancellationToken ct = default)
    {
        if (projectId is { } scoped)
        {
            _ = await RequireProjectAsync(scoped, ct);
        }

        var conversation = Conversation.Create(projectId, title);
        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(ct);
        return conversation;
    }

    public async Task<Conversation?> GetAsync(Guid id, CancellationToken ct = default) =>
        await _db.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Conversation>> ListAsync(Guid? projectId, CancellationToken ct = default) =>
        await _db.Conversations
            .AsNoTracking()
            .Where(c => projectId == null || c.ProjectId == projectId)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(ct);

    public async Task<Conversation?> RenameAsync(Guid id, string? title, CancellationToken ct = default)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (conversation is null)
        {
            return null;
        }

        conversation.Rename(title);
        await _db.SaveChangesAsync(ct);
        return conversation;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var deleted = await _db.Conversations
            .Where(c => c.Id == id)
            .ExecuteDeleteAsync(ct);
        return deleted > 0;
    }

    private async Task<Project> RequireProjectAsync(Guid projectId, CancellationToken ct) =>
        await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
        ?? throw PlanningException.MissingProject(projectId);
}
