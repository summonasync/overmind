using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class PlanningDirectivesApi
{
    public static RouteGroupBuilder MapPlanningDirectivesApi(this IEndpointRouteBuilder routes)
    {
        var human = routes.MapGroup("/api");
        human.MapPost("/projects/{projectId:guid}/planning/features/{slug}/directives", AppendForHuman).OpenCoordination();
        human.MapPost("/projects/{projectId:guid}/planning/features/{slug}/directives/{directiveId:guid}/ack", AckForHuman).OpenCoordination();

        var agent = routes.MapGroup("/api/agent");
        agent.MapPost("/projects/{projectId:guid}/planning/features/{slug}/directives", AppendForAgent).OpenCoordination();
        agent.MapPost("/projects/{projectId:guid}/planning/features/{slug}/directives/{directiveId:guid}/ack", AckForAgent).OpenCoordination();

        return human;
    }

    private static RouteHandlerBuilder OpenCoordination(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(ClaimLockFilter.Instance)
            .WithMetadata(OpenCoordinationSurface.Instance);

    internal static async Task<IResult> AppendForAgent(
        Guid projectId,
        string slug,
        AppendPlanningDirectiveRequest request,
        IValidator<AppendPlanningDirectiveRequest> validator,
        DirectiveService directives,
        OvermindDbContext db,
        HttpRequest http,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        if (!SessionHeader.TryRead(http, out var sessionId))
        {
            return AgentIdentityRequired();
        }

        var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            return AgentIdentityRequired();
        }

        return await AppendAsync(projectId, slug, request.Text, new DirectiveAuthor(session.Alias, session.Vendor, session.Id), directives, ct);
    }

    internal static async Task<IResult> AppendForHuman(
        Guid projectId,
        string slug,
        AppendPlanningDirectiveHumanRequest request,
        IValidator<AppendPlanningDirectiveHumanRequest> validator,
        DirectiveService directives,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return await AppendAsync(
            projectId, slug, request.Text, new DirectiveAuthor(request.Alias, request.Vendor, null), directives, ct);
    }

    internal static async Task<IResult> AckForAgent(
        Guid projectId,
        string slug,
        Guid directiveId,
        DirectiveService directives,
        OvermindDbContext db,
        HttpRequest http,
        CancellationToken ct)
    {
        if (!SessionHeader.TryRead(http, out var sessionId))
        {
            return AgentIdentityRequired();
        }

        var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            return AgentIdentityRequired();
        }

        return await AckAsync(
            projectId, slug, directiveId, new DirectiveAuthor(session.Alias, session.Vendor, session.Id), directives, ct);
    }

    internal static async Task<IResult> AckForHuman(
        Guid projectId,
        string slug,
        Guid directiveId,
        DirectiveService directives,
        [FromBody] AckPlanningDirectiveRequest? request,
        CancellationToken ct) =>
        await AckAsync(
            projectId, slug, directiveId, new DirectiveAuthor(request?.Alias, request?.Vendor, null), directives, ct);

    private static async Task<IResult> AppendAsync(
        Guid projectId, string slug, string text, DirectiveAuthor author,
        DirectiveService directives, CancellationToken ct)
    {
        var trimmed = text.Trim();
        var outcome = SoftCap.Apply(trimmed, PlanningDirective.MaxTextLength);
        if (outcome.Warning is { Truncated: true } rejected)
        {
            return PlanningErrors.Map(PlanningException.DirectiveTextTooLong(
                trimmed.Length, PlanningDirective.MaxTextLength,
                SoftCap.Ceiling(PlanningDirective.MaxTextLength), rejected.LostTailPreview));
        }

        try
        {
            var directive = await directives.AppendAsync(projectId, slug, outcome.Stored!, author, ct);
            return Results.Ok(ToResponse(directive) with
            {
                Warnings = outcome.Warning is null ? null : [outcome.Warning]
            });
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
        catch (ArgumentException ex)
        {
            return HttpErrors.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> AckAsync(
        Guid projectId, string slug, Guid directiveId, DirectiveAuthor author,
        DirectiveService directives, CancellationToken ct)
    {
        try
        {
            var outcome = await directives.AckAsync(projectId, slug, directiveId, author, ct);
            var directive = outcome.Directive;
            return Results.Ok(new DirectiveAckResponse(
                directive.Id,
                outcome.AckedNow,
                directive.AckByAlias,
                directive.AckVendor,
                directive.AckAt));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static OpenDirectiveResponse ToResponse(PlanningDirective directive) =>
        new(directive.Id, directive.Text, directive.AuthorAlias, directive.AuthorVendor, directive.CreatedAt, directive.Acked);

    private static IResult AgentIdentityRequired() =>
        HttpErrors.BadRequest(
            $"Directive authorship requires the {SessionHeader.Name} header and a registered agent session — " +
            "register via POST /api/agent/sessions.");
}
