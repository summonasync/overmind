using Microsoft.AspNetCore.Http.Metadata;
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
//
// Two rules sit on this gate, not one. R6's type rule (a gated entry type is never written through the agent
// door) and its completeness rule (the gate only classifies bodies it has vetted, so a verb that addresses more
// than the route-named entry is refused instead of half-checked). Both answer through the same typed refusal so a
// caller never has to guess whether a pass was a permission or an oversight.
public sealed class JournalWriteLockFilter : IEndpointFilter
{
    public static readonly JournalWriteLockFilter Instance = new();

    // The types the agent door never writes, each with the code it answers by. Skill is here because the skill
    // lifecycle is UI-managed (files, kits) and both adapters already refuse it client-side — the door states the
    // same thing so the prose stops over-claiming the server. EntryTypeSurface.AgentWritable is the complement.
    public static readonly IReadOnlyDictionary<EntryType, GatedWrite> Gated = new Dictionary<EntryType, GatedWrite>
    {
        [EntryType.Journal] = new(
            "JournalEntryWriteRefused",
            $"Journal entries are the human surface; the agent door accepts {EntryTypeSurface.AgentWritableNames}."),
        [EntryType.Skill] = new(
            "SkillEntryWriteRefused",
            "Skill entries are managed through the human door: they carry skill files and a kit lifecycle, " +
            $"so the agent door accepts {EntryTypeSurface.AgentWritableNames}."),
    };

    // One entry per request, the one the route names (or the one being created). The gate knows these four shapes
    // and nothing else: an unvetted body — including any verb that wants a second entry — is refused rather than
    // classified from its primary target alone.
    public static IReadOnlyCollection<Type> VettedBodies { get; } =
    [
        typeof(CreateEntryRequest),
        typeof(UpdateEntryRequest),
        typeof(EntryLiftRequest),
        typeof(EntryAppendRequest),
    ];

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

        // Checked before the target lookups: the gate refuses to classify a request whose body it has not read
        // the semantics of, and it does that without touching the database.
        if (!RequestBodyIsVetted(http))
        {
            return ShapeUndetermined(
                "its body is a request contract this gate has not vetted, so it cannot tell which entries the " +
                "verb mutates");
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
            return targetType is { } type && Gated.TryGetValue(type, out var gatedOnTarget)
                ? Refused(gatedOnTarget)
                : await next(context);
        }

        // The create verb carries no id, so its type rides the request contract.
        if (context.Arguments.OfType<CreateEntryRequest>().FirstOrDefault() is { } creation)
        {
            return Gated.TryGetValue(creation.Type, out var gatedOnCreate)
                ? Refused(gatedOnCreate)
                : await next(context);
        }

        return ShapeUndetermined("no entry id addresses it and its body is not a create the gate can read");
    }

    private static bool RequestBodyIsVetted(HttpContext http) =>
        http.GetEndpoint()?.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType is not { } body ||
        VettedBodies.Contains(body);

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

    private static IResult Refused(GatedWrite gated) => HttpErrors.BadRequest(gated.Message, gated.Code);

    private static IResult ShapeUndetermined(string because) => HttpErrors.BadRequest(
        "The agent entry write gate refused the request because " + because + ". " +
        "Register the route on the /api/agent/entries subgroup with a body the gate recognises " +
        $"({string.Join(", ", [.. VettedBodies.Select(shape => shape.Name)])}), " +
        "or address the target entry by an id route value.",
        "AgentEntryWriteShapeUndetermined");

    public sealed record GatedWrite(string Code, string Message);
}
