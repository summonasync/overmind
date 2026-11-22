namespace Overmind.Mcp;

internal sealed record ProblemDetails(
    string? Title,
    string? Detail,
    string? Fix,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Errors);
