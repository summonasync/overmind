using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Usage;

namespace Overmind.Host.Api;

public static class UsageApi
{
    public static RouteGroupBuilder MapUsageApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapPost("/usage", Ingest);
        group.MapGet("/usage/summary", Summary);
        return group;
    }

    private static async Task<IResult> Ingest(IngestUsageRequest request, OvermindDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Alias))
        {
            return HttpErrors.BadRequest("alias is required.");
        }

        if (request.Alias.Trim().Length > AgentSession.MaxIdentityLength)
        {
            return HttpErrors.BadRequest(
                $"alias must be at most {AgentSession.MaxIdentityLength} characters.");
        }

        if (request.SessionId == Guid.Empty)
        {
            return HttpErrors.BadRequest("sessionId is required.");
        }

        if (request.Ts == default)
        {
            return HttpErrors.BadRequest("ts must be a valid timestamp.");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return HttpErrors.BadRequest("model is required.");
        }

        var negative = NegativeTokenField(request);
        if (negative is not null)
        {
            return HttpErrors.BadRequest($"{negative} must not be negative.");
        }

        UsageRecord record;
        try
        {
            record = UsageRecord.Create(
                null, request.Alias, request.SessionId, request.Ts, request.Model,
                request.InputTokens, request.OutputTokens,
                request.CacheReadTokens, request.CacheWriteTokens);
        }
        catch (ArgumentException ex)
        {
            return HttpErrors.BadRequest(ex.Message);
        }

        db.UsageRecords.Add(record);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/usage/{record.Id}", new IngestUsageResponse(record.Id));
    }

    private static async Task<IResult> Summary(
        Guid projectId, DateTime? since, DateTime? until,
        UsageSummaryService summaries, CancellationToken ct)
    {
        if (projectId == Guid.Empty)
        {
            return HttpErrors.BadRequest("projectId is required.");
        }

        var summary = await summaries.SummarizeAsync(projectId, since, until, ct);
        return Results.Ok(UsageMapper.ToResponse(summary));
    }

    private static string? NegativeTokenField(IngestUsageRequest request) =>
        request.InputTokens < 0 ? "inputTokens"
        : request.OutputTokens < 0 ? "outputTokens"
        : request.CacheReadTokens < 0 ? "cacheReadTokens"
        : request.CacheWriteTokens < 0 ? "cacheWriteTokens"
        : null;
}
