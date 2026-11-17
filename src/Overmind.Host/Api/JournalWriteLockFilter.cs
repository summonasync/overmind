using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

// The journal author boundary as a route gate, in the shape ClaimLockFilter set for security-relevant filters:
// it hangs on the /api/agent/entries subgroup (see EntriesApi.MapEntriesApi), so a verb registered there inherits
// the refusal instead of remembering it. The hole this closes was exactly that forgetting — append, lift and
// delete were added without the per-handler flag the earlier draft of the contract wanted.
//
// Reads stay open by design: a safe method is passed through untouched, journal entries included, and the ack
// route lives outside the subgroup because an ack writes an additive readBy row, not the entry.
public sealed class JournalWriteLockFilter : IEndpointFilter
{
    public static readonly JournalWriteLockFilter Instance = new();

    private JournalWriteLockFilter()
    {
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method))
        {
            return await next(context);
        }

        if (TryTargetEntryId(http.Request.RouteValues, out var entryId))
        {
            var db = http.RequestServices.GetRequiredService<OvermindDbContext>();
            var targetType = await db.ContextEntries
                .AsNoTracking()
                .Where(entry => entry.Id == entryId)
                .Select(entry => (EntryType?)entry.Type)
                .FirstOrDefaultAsync(http.RequestAborted);

            // A missing row is the handler's 404, not this filter's business.
            return targetType == EntryType.Journal ? Refused() : await next(context);
        }

        // The create verb carries no id, so its type rides the request contract.
        if (context.Arguments.OfType<CreateEntryRequest>().FirstOrDefault() is { } creation)
        {
            return creation.Type == EntryType.Journal ? Refused() : await next(context);
        }

        // An agent entry mutation the gate cannot classify is refused rather than waved through: guessing
        // "allow" here is the original bug wearing a new hat. Teach this filter the shape or reuse CreateEntryRequest.
        return HttpErrors.BadRequest(
            "The agent entry write gate cannot classify this request against the journal rule, so it refused it. " +
            "Register the route on the /api/agent/entries subgroup with a body the gate recognises, " +
            "or address the target entry by an id route value.",
            "AgentEntryWriteShapeUndetermined");
    }

    private static bool TryTargetEntryId(
        Microsoft.AspNetCore.Routing.RouteValueDictionary routeValues, out Guid entryId)
    {
        foreach (var key in new[] { "id", "entryId" })
        {
            if (routeValues.TryGetValue(key, out var raw) && Guid.TryParse(raw?.ToString(), out entryId))
            {
                return true;
            }
        }

        entryId = default;
        return false;
    }

    private static IResult Refused() =>
        HttpErrors.BadRequest(
            $"Journal entries are the human surface; the agent door accepts {EntryTypeSurface.AgentWritableNames}.",
            "JournalEntryWriteRefused");
}
