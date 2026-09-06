namespace Overmind.Host.Api;

public static class HttpErrors
{
    public static IResult NotFound(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: detail);

    public static IResult Conflict(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: detail);
}
