using Overmind.Core;

namespace Overmind.Host.Api;

public static class MaterializationErrors
{
    public static IResult Map(MaterializationException ex) => ex.Kind switch
    {
        MaterializationFailureKind.NotFound => HttpErrors.NotFound(ex.Message),
        MaterializationFailureKind.PathTraversal or MaterializationFailureKind.InvalidTarget =>
            Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid materialization target",
                detail: ex.Message),
        MaterializationFailureKind.OverwriteBlocked or MaterializationFailureKind.LocalEditsBlocked
            or MaterializationFailureKind.Refused =>
            HttpErrors.Conflict(ex.Message),
        _ => Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Materialization failed",
            detail: ex.Message),
    };
}
