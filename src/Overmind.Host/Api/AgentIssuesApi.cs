using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class AgentIssuesApi
{
    public static RouteGroupBuilder MapAgentIssuesApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/agent/projects/{projectId:guid}/issues").AddEndpointFilter(UnknownPropertyProbeFilter.Instance);

        group.MapPost(string.Empty, Create);
        group.MapGet(string.Empty, List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("/{id:guid}/append", Append);
        group.MapPost("/{id:guid}/status", SetStatus);
        group.MapPost("/{id:guid}/promote", Promote);

        var idGroup = routes.MapGroup("/api/agent/issues").AddEndpointFilter(UnknownPropertyProbeFilter.Instance);

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
                PlanningLogSource.Agent, request.Kind, ct);
            return Results.Ok(new CreateIssueResponse(
                issue.Id, issue.Status, issue.Title, issue.Kind));
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
        Guid id, IssueService service, HttpRequest http, CancellationToken ct)
    {
        if (!AgentCallerScope.TryReadProjectId(http, out var callerProjectId, out var callerRefusal))
        {
            return callerRefusal!;
        }

        try
        {
            var (issue, appends) = await service.GetAsync(id, ct, callerProjectId);
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
        HttpRequest http,
        CancellationToken ct)
    {
        if (!AgentCallerScope.TryReadProjectId(http, out var callerProjectId, out var callerRefusal))
        {
            return callerRefusal!;
        }

        var warnings = new List<object>();
        var text = SoftCapSoften.Cap(request.Text, SoftCapLimits.FollowUpText, warnings);
        try
        {
            var append = await service.AppendAsync(id, request.AuthorProjectId, text!, ct, callerProjectId);
            return Results.Ok(new IssueAppendItem(
                append.Id, append.AuthorProjectId, append.Text, append.CreatedAt)
            {
                Warnings = SoftCapSoften.Merge(warnings)
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

    internal static async Task<IResult> SetStatusById(
        Guid id,
        SetIssueStatusRequest request,
        IssueService service,
        HttpRequest http,
        CancellationToken ct)
    {
        if (!AgentCallerScope.TryReadProjectId(http, out var callerProjectId, out var callerRefusal))
        {
            return callerRefusal!;
        }

        try
        {
            var issue = await service.SetStatusAsync(id, request.Status, request.Source, ct, callerProjectId);
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
        if (!AgentCallerScope.TryReadProjectId(http, out var callerProjectId, out var callerRefusal))
        {
            return callerRefusal!;
        }

        try
        {
            var (issue, artifact) = await service.PromoteAsync(
                id, request.FeatureSlug, request.Source, sessionId,
                SessionTouchMiddleware.PreTouchLastSeenOf(http), ct, callerProjectId);
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
        var warnings = new List<object>();
        var text = SoftCapSoften.Cap(request.Text, SoftCapLimits.FollowUpText, warnings);
        try
        {
            await EnsureIssueOnBoardAsync(service, projectId, id, ct);
            var append = await service.AppendAsync(id, request.AuthorProjectId, text!, ct);
            return Results.Ok(new IssueAppendItem(
                append.Id, append.AuthorProjectId, append.Text, append.CreatedAt)
            {
                Warnings = SoftCapSoften.Merge(warnings)
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
                id, request.FeatureSlug, request.Source, sessionId,
                SessionTouchMiddleware.PreTouchLastSeenOf(http), ct);
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
            issue.CreatedAt, issue.ClosedAt, issue.PromotedToArtifactId, appendCount, issue.Kind);

    internal static IssueCard ToCard(Issue issue, IReadOnlyList<IssueAppend> appends) =>
        new(issue.Id, issue.Title, issue.Body, issue.Status, issue.AuthorProjectId,
            issue.CreatedAt, issue.ClosedAt, issue.PromotedToArtifactId,
            [.. appends.Select(a => new IssueAppendItem(a.Id, a.AuthorProjectId, a.Text, a.CreatedAt))],
            issue.Kind);
}
