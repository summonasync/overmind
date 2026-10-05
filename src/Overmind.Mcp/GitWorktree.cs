namespace Overmind.Mcp;

public sealed record GitWorktree(
    string Path,
    string? Head,
    string? Branch,
    bool IsBare,
    bool IsDetached);
