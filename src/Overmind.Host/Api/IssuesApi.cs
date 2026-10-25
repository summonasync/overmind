using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class IssuesApi
{
    public static RouteGroupBuilder MapIssuesApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/projects/{projectId:guid}/issues");

        group.MapPost(string.Empty, Create);
        group.MapGet(string.Empty, List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("/{id:guid}/append", Append);
        group.MapPost("/{id:guid}/status", SetStatus);
        group.MapPost("/{id:guid}/promote", Promote);

        return group;
    }

    internal static async Task<IResult> Create(
        Guid projectId,
        CreateIssueRequest request,
        IssueService service,
        CancellationToken ct)
    {
        try
        {
            var issue = await service.CreateAsync(
                projectId, request.AuthorProjectId, request.Title, request.Body,
                PlanningLogSource.Ui, request.Kind, ct);
            return Results.Created(
                $"/api/projects/{projectId}/issues/{issue.Id}",
                new CreateIssueResponse(issue.Id, issue.Status, issue.Title, issue.Kind));
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

    internal static async Task<IResult> List(
        Guid projectId,
        string? status,
        string? authorProjectId,
        string? kind,
        IssueService service,
        CancellationToken ct)
    {
        Guid? authorFilter = null;
        if (authorProjectId is not null)
        {
            if (!Guid.TryParse(authorProjectId, out var authorValue))
            {
                return HttpErrors.BadRequest(
                    $"'{authorProjectId}' is not a valid authorProjectId (expected a GUID).");
            }

            authorFilter = authorValue;
        }

        var (parsedStatus, statusError) = IssueStatusParser.Parse(status);
        if (statusError is not null)
        {
            return HttpErrors.BadRequest(statusError);
        }

        var issues = await service.ListAsync(projectId, parsedStatus, authorFilter, kind, ct);
        var counts = await service.AppendCountsAsync([.. issues.Select(i => i.Id)], ct);
        var rows = issues.Select(i => AgentIssuesApi.ToRow(i, counts.GetValueOrDefault(i.Id))).ToArray();
        return Results.Ok(rows);
    }

    internal static async Task<IResult> Get(
        Guid projectId, Guid id, IssueService service, CancellationToken ct)
    {
        try
        {
            var (issue, appends) = await service.GetAsync(id, ct);
            if (issue.ProjectId != projectId)
            {
                return HttpErrors.NotFound($"Issue '{id}' was not found.");
            }

            return Results.Ok(AgentIssuesApi.ToCard(issue, appends));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> Append(
        Guid projectId,
        Guid id,
        AppendIssueRequest request,
        IssueService service,
        CancellationToken ct)
    {
        try
        {
            await AgentIssuesApi.EnsureIssueOnBoardAsync(service, projectId, id, ct);
            var append = await service.AppendAsync(id, request.AuthorProjectId, request.Text, ct);
            return Results.Ok(new IssueAppendItem(
                append.Id, append.AuthorProjectId, append.Text, append.CreatedAt));
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

    internal static async Task<IResult> SetStatus(
        Guid projectId,
        Guid id,
        SetIssueStatusRequest request,
        IssueService service,
        CancellationToken ct)
    {
        try
        {
            await AgentIssuesApi.EnsureIssueOnBoardAsync(service, projectId, id, ct);
            var issue = await service.SetStatusAsync(id, request.Status, PlanningLogSource.Ui, ct);
            return Results.Ok(new SetIssueStatusResponse(
                issue.Id, issue.Status, issue.StatusSource, issue.ClosedAt));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> Promote(
        Guid projectId,
        Guid id,
        PromoteIssueRequest request,
        IssueService service,
        CancellationToken ct)
    {
        try
        {
            await AgentIssuesApi.EnsureIssueOnBoardAsync(service, projectId, id, ct);
            var (issue, artifact) = await service.PromoteAsync(
                id, request.FeatureSlug, PlanningLogSource.Ui, sessionId: null, ct);
            return Results.Ok(new IssuePromoteResponse(
                issue.Id, issue.Status, artifact.Id, artifact.FeatureId, artifact.Version));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }
}
