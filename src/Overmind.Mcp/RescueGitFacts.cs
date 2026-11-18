namespace Overmind.Mcp;

public sealed record RescueGitFacts(
    int? DirtyFiles,
    int? UntrackedFiles,
    int? UnpushedCommits,
    string? UnpushedBase,
    bool? Unmerged,
    string? Mainline,
    string? LastCommitHash,
    string? LastCommitDate,
    string? Error)
{
    public bool NothingMeasured =>
        DirtyFiles is null && UnpushedCommits is null && Unmerged is null && LastCommitHash is null;
}
