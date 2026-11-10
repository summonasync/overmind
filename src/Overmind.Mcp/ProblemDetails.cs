namespace Overmind.Mcp;

internal sealed record ProblemDetails(
    string? Title,
    string? Detail,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Errors);
