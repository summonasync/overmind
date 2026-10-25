using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class CrossProjectIssuesApi
{
    public static RouteGroupBuilder MapCrossProjectIssuesApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/issues");

        group.MapGet(string.Empty, ListAll);

        return group;
    }

    internal static async Task<IResult> ListAll(
        string? status,
        string? kind,
        IssueService service,
        CancellationToken ct)
    {
        var (parsedStatus, statusError) = IssueStatusParser.Parse(status);
        if (statusError is not null)
        {
            return HttpErrors.BadRequest(statusError);
        }

        var rows = await service.ListAllAsync(parsedStatus, kind, ct);
        return Results.Ok(rows.Select(ToRow).ToArray());
    }

    private static CrossProjectIssueRow ToRow(IssueBoardRow row) =>
        new(row.Id, row.ProjectId, row.Title, row.Status,
            row.AuthorProjectId, row.CreatedAt, row.ClosedAt,
            row.PromotedToArtifactId, row.AppendCount, row.Kind);
}
