using Overmind.Core;

namespace Overmind.Host.Api;

public static class PlanningErrors
{
    public static IResult Map(PlanningException ex) => ex.Kind switch
    {
        PlanningFailureKind.NotFound => HttpErrors.NotFound(ex.Message),
        PlanningFailureKind.Gone => HttpErrors.Gone(ex.Message, title: ex.Code.ToString()),
        PlanningFailureKind.Duplicate => HttpErrors.Conflict(ex.Message, title: ex.Code.ToString()),
        PlanningFailureKind.RuleViolation => HttpErrors.Conflict(ex.Message, title: ex.Code.ToString()),
        PlanningFailureKind.InvalidStatus or PlanningFailureKind.InvalidInput => Results.Problem(
            statusCode: StatusCodes.Status400BadRequest, title: ex.Code.ToString(), detail: ex.Message),
        _ => Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError, title: "Planning error", detail: ex.Message),
    };
}
