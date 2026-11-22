namespace Overmind.Host.Api;

public static class HttpErrors
{
    public static IResult BadRequest(string detail, string? title = null, string? fix = null) =>
        ProblemHttp(StatusCodes.Status400BadRequest, title ?? "Bad Request", detail, fix);

    public static IResult NotFound(string detail, string? title = null, string? fix = null) =>
        ProblemHttp(StatusCodes.Status404NotFound, title ?? "Not Found", detail, fix);

    public static IResult Conflict(string detail, string? title = null, string? fix = null) =>
        ProblemHttp(StatusCodes.Status409Conflict, title ?? "Conflict", detail, fix);

    public static IResult Gone(string detail, string? title = null, string? fix = null) =>
        ProblemHttp(StatusCodes.Status410Gone, title ?? "Gone", detail, fix);

    // The fix rides as a ProblemDetails extension — one top-level "fix" string next to title/detail,
    // distinct from the soft-cap warnings[] shape, which only ever appears on accepted payloads.
    private static IResult ProblemHttp(int statusCode, string title, string detail, string? fix) =>
        Results.Problem(
            detail, null, statusCode, title, null,
            string.IsNullOrWhiteSpace(fix)
                ? null
                : new Dictionary<string, object?> { ["fix"] = fix });
}
