using Overmind.Core;

namespace Overmind.Host.Api;

public static class PlanningErrors
{
    public static IResult Map(PlanningException ex) => ex.Kind switch
    {
        PlanningFailureKind.NotFound => HttpErrors.NotFound(ex.Message, title: ex.Code.ToString(), fix: ex.Fix),
        PlanningFailureKind.Gone => HttpErrors.Gone(ex.Message, title: ex.Code.ToString(), fix: ex.Fix),
        PlanningFailureKind.Duplicate => HttpErrors.Conflict(ex.Message, title: ex.Code.ToString(), fix: ex.Fix),
        PlanningFailureKind.RuleViolation => HttpErrors.Conflict(ex.Message, title: ex.Code.ToString(), fix: ex.Fix),
        PlanningFailureKind.InvalidStatus or PlanningFailureKind.InvalidInput => HttpErrors.BadRequest(
            ex.Message, title: ex.Code.ToString(), fix: ex.Fix),
        _ => Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError, title: "Planning error", detail: ex.Message),
    };
}
