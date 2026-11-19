using Overmind.Core;

namespace Overmind.Host.Api;

public static class KitActivationErrors
{
    public static IResult Map(KitActivationException ex) => ex.Kind switch
    {
        KitActivationFailureKind.MandatoryGlobal =>
            HttpErrors.Conflict(ex.Message, title: nameof(KitActivationFailureKind.MandatoryGlobal)),
        KitActivationFailureKind.NotLinked => HttpErrors.NotFound(ex.Message),
        _ => Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError, title: "Kit activation failed", detail: ex.Message),
    };
}
