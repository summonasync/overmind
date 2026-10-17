using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class DirectiveService
{
    private readonly OvermindDbContext _db;

    public DirectiveService(OvermindDbContext db)
    {
        _db = db;
    }

    public async Task<PlanningDirective> AppendAsync(
        Guid projectId, string slug, string text, DirectiveAuthor author, CancellationToken ct = default)
    {
        var feature = await RequireFeatureAsync(projectId, slug, ct);
        var directive = PlanningDirective.Create(
            feature.Id, text, author.Alias, author.Vendor, author.SessionId);
        _db.PlanningDirectives.Add(directive);
        await _db.SaveChangesAsync(ct);
        return directive;
    }

    public async Task<DirectiveAckOutcome> AckAsync(
        Guid projectId, string slug, Guid directiveId, DirectiveAuthor author, CancellationToken ct = default)
    {
        var feature = await RequireFeatureAsync(projectId, slug, ct);
        var directive = await _db.PlanningDirectives
            .FirstOrDefaultAsync(d => d.Id == directiveId && d.PlanningFeatureId == feature.Id, ct)
            ?? throw PlanningException.MissingDirective(directiveId);

        if (directive.Acked)
        {
            return new DirectiveAckOutcome(directive, AckedNow: false);
        }

        directive.Ack(author.Alias, author.Vendor, DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return new DirectiveAckOutcome(directive, AckedNow: true);
    }

    public async Task<IReadOnlyList<PlanningDirective>> ListOpenAsync(
        Guid featureId, CancellationToken ct = default) =>
        [.. await _db.PlanningDirectives
            .AsNoTracking()
            .Where(d => d.PlanningFeatureId == featureId && d.AckAt == null)
            .OrderBy(d => d.CreatedAt)
            .ThenBy(d => d.Id)
            .ToListAsync(ct)];

    public async Task<IReadOnlyDictionary<Guid, int>> OpenCountsAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var counts = await _db.PlanningDirectives
            .AsNoTracking()
            .Where(d => d.AckAt == null)
            .Join(_db.PlanningFeatures.Where(f => f.ProjectId == projectId),
                d => d.PlanningFeatureId, f => f.Id, (d, _) => d)
            .GroupBy(d => d.PlanningFeatureId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return counts.ToDictionary(c => c.Key, c => c.Count);
    }

    public static AckProjection Projection(PlanningDirective directive) =>
        AckProjection.From(directive.AckByAlias, directive.AckVendor, directive.AckAt);

    private async Task<PlanningFeature> RequireFeatureAsync(Guid projectId, string slug, CancellationToken ct) =>
        await _db.PlanningFeatures
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct)
            ?? throw PlanningException.MissingFeature(slug);
}
