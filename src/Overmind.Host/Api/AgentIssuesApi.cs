using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class AgentIssuesApi
{
    public static RouteGroupBuilder MapAgentIssuesApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/agent/projects/{projectId:guid}/issues");

        group.MapPost(string.Empty, Create);
        group.MapGet(string.Empty, List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("/{id:guid}/append", Append);
        group.MapPost("/{id:guid}/status", SetStatus);
        group.MapPost("/{id:guid}/promote", Promote);

        var idGroup = routes.MapGroup("/api/agent/issues");

        idGroup.MapGet("/{id:guid}", GetById);
        idGroup.MapPost("/{id:guid}/append", AppendById);
        idGroup.MapPost("/{id:guid}/status", SetStatusById);
        idGroup.MapPost("/{id:guid}/promote", PromoteById);

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
                PlanningLogSource.Agent, ct);
            return Results.Ok(new CreateIssueResponse(issue.Id, issue.Status, issue.Title));
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

        IssueStatus? parsedStatus = null;
        if (status is not null)
        {
            if (!PlanningFeaturesApi.TryParseEnum<IssueStatus>(status, out var statusValue))
            {
                return HttpErrors.BadRequest(
                    $"Unknown status '{status}'. Allowed values: open, triaged, promoted, closed.");
            }

            parsedStatus = statusValue;
        }

        var issues = await service.ListAsync(projectId, parsedStatus, authorFilter, ct);
        var counts = await service.AppendCountsAsync([.. issues.Select(i => i.Id)], ct);
        var rows = issues.Select(i => ToRow(i, counts.GetValueOrDefault(i.Id))).ToArray();
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

            return Results.Ok(ToCard(issue, appends));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> GetById(
        Guid id, IssueService service, CancellationToken ct)
    {
        try
        {
            var (issue, appends) = await service.GetAsync(id, ct);
            return Results.Ok(ToCard(issue, appends));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> AppendById(
        Guid id,
        AppendIssueRequest request,
        IssueService service,
        CancellationToken ct)
    {
        try
        {
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

    internal static async Task<IResult> SetStatusById(
        Guid id,
        SetIssueStatusRequest request,
        IssueService service,
        CancellationToken ct)
    {
        try
        {
            var issue = await service.SetStatusAsync(id, request.Status, request.Source, ct);
            return Results.Ok(new SetIssueStatusResponse(
                issue.Id, issue.Status, issue.StatusSource, issue.ClosedAt));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> PromoteById(
        Guid id,
        PromoteIssueRequest request,
        IssueService service,
        HttpRequest http,
        CancellationToken ct)
    {
        SessionHeader.TryRead(http, out var sessionId);
        try
        {
            var (issue, artifact) = await service.PromoteAsync(
                id, request.FeatureSlug, request.Source, sessionId, ct);
            return Results.Ok(new IssuePromoteResponse(
                issue.Id, issue.Status, artifact.Id, artifact.FeatureId, artifact.Version));
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
            await EnsureIssueOnBoardAsync(service, projectId, id, ct);
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
            await EnsureIssueOnBoardAsync(service, projectId, id, ct);
            var issue = await service.SetStatusAsync(id, request.Status, request.Source, ct);
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
        HttpRequest http,
        CancellationToken ct)
    {
        SessionHeader.TryRead(http, out var sessionId);
        try
        {
            await EnsureIssueOnBoardAsync(service, projectId, id, ct);
            var (issue, artifact) = await service.PromoteAsync(
                id, request.FeatureSlug, request.Source, sessionId, ct);
            return Results.Ok(new IssuePromoteResponse(
                issue.Id, issue.Status, artifact.Id, artifact.FeatureId, artifact.Version));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task EnsureIssueOnBoardAsync(
        IssueService service, Guid projectId, Guid issueId, CancellationToken ct)
    {
        var (issue, _) = await service.GetAsync(issueId, ct);
        if (issue.ProjectId != projectId)
        {
            throw PlanningException.MissingIssue(issueId);
        }
    }

    internal static IssueRow ToRow(Issue issue, int appendCount) =>
        new(issue.Id, issue.Title, issue.Status, issue.AuthorProjectId,
            issue.CreatedAt, issue.ClosedAt, issue.PromotedToArtifactId, appendCount);

    internal static IssueCard ToCard(Issue issue, IReadOnlyList<IssueAppend> appends) =>
        new(issue.Id, issue.Title, issue.Body, issue.Status, issue.AuthorProjectId,
            issue.CreatedAt, issue.ClosedAt, issue.PromotedToArtifactId,
            [.. appends.Select(a => new IssueAppendItem(a.Id, a.AuthorProjectId, a.Text, a.CreatedAt))]);
}
