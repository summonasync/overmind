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
        IssueService service,
        CancellationToken ct)
    {
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

        var rows = await service.ListAllAsync(parsedStatus, ct);
        return Results.Ok(rows.Select(ToRow).ToArray());
    }

    private static CrossProjectIssueRow ToRow(IssueBoardRow row) =>
        new(row.Id, row.ProjectId, row.ProjectName, row.Title, row.Status,
            row.AuthorProjectId, row.CreatedAt, row.ClosedAt,
            row.PromotedToArtifactId, row.AppendCount);
}
