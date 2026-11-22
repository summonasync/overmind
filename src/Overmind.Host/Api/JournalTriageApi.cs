using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Entries;

namespace Overmind.Host.Api;

// Spec R6's exit door, and the only place a journal note turns into board work. The route sits on the human
// prefix, outside /api/agent, because promotion is an act on the owner's own thoughts (spec R8): the boundary is
// carried by placement, and the agent planning doors keep their ordinary feature- and task-creation verbs exactly
// because those create work without ever claiming a note. The service owns the transaction, the provenance edge and
// the materialization dispatch, so this slice stays a dispatcher: validate, hand over, translate the refusal.
public static class JournalTriageApi
{
    public static RouteGroupBuilder MapJournalTriageApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapPost("/entries/{id:guid}/promote-to-feature", PromoteToFeature);
        group.MapPost("/entries/{id:guid}/promote-to-task", PromoteToTask);

        return group;
    }

    private static async Task<IResult> PromoteToFeature(
        Guid id,
        PromoteNoteToFeatureRequest request,
        IValidator<PromoteNoteToFeatureRequest> validator,
        TriagePromotionService promotion,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        if (!TryParsePriority(request.Priority, out var priority))
        {
            return HttpErrors.BadRequest(
                $"Unknown priority '{request.Priority}'. Valid values: {string.Join(", ", Names)}.",
                "InvalidFeaturePriority");
        }

        try
        {
            var result = await promotion.PromoteToFeatureAsync(
                id, request.ProjectId, request.Title, request.Body, priority, ct);
            return Results.Ok(ToResponse(result));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static async Task<IResult> PromoteToTask(
        Guid id,
        PromoteNoteToTaskRequest request,
        IValidator<PromoteNoteToTaskRequest> validator,
        TriagePromotionService promotion,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        try
        {
            var result = await promotion.PromoteToTaskAsync(
                id, request.FeatureId, request.Body, request.Brief, ct);
            return Results.Ok(ToResponse(result));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static NotePromotionResponse ToResponse(TriagePromotionResult result) =>
        new(result.EntryId, result.TargetKind, result.TargetId, result.FeatureId, result.FeatureSlug);

    private static readonly string[] Names = [.. Enum.GetNames<FeaturePriority>()];

    // Absent priority is the default the board gives any un-prioritised card; a value that is present but not one
    // of the four names is a typo the owner should hear about, not a silent P2.
    private static bool TryParsePriority(string? value, out FeaturePriority priority)
    {
        priority = FeaturePriority.P2;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out priority) && Enum.IsDefined(priority);
    }
}
