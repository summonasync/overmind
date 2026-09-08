namespace Overmind.Host.Api;

public static class HttpErrors
{
    public static IResult BadRequest(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request", detail: detail);

    public static IResult NotFound(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: detail);

    public static IResult Conflict(string detail, string? title = null) =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: title ?? "Conflict", detail: detail);
}
