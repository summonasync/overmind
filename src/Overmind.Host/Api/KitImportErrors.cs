using Overmind.Core;

namespace Overmind.Host.Api;

public static class KitImportErrors
{
    public static IResult Map(KitImportException ex) => ex.Kind switch
    {
        KitImportFailureKind.KitNotFound => HttpErrors.NotFound(ex.Message),
        KitImportFailureKind.DuplicateName
            or KitImportFailureKind.ReImportRequired
            or KitImportFailureKind.NoSource
            or KitImportFailureKind.SourceMissing => HttpErrors.Conflict(ex.Message, title: ex.Kind.ToString()),
        KitImportFailureKind.PathMissing
            or KitImportFailureKind.NotADirectory
            or KitImportFailureKind.CandidateNotFound => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest, title: ex.Kind.ToString(), detail: ex.Message),
        _ => Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError, title: "Kit import failed", detail: ex.Message),
    };
}
