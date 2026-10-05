namespace Overmind.Mcp;

public static class GitWorktreeListParser
{
    private const string BranchPrefix = "refs/heads/";

    public static IReadOnlyList<GitWorktree> Parse(string output)
    {
        var worktrees = new List<GitWorktree>();
        string? path = null, head = null, branch = null;
        var bare = false;
        var detached = false;

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                if (path is not null)
                {
                    worktrees.Add(new GitWorktree(path, head, branch, bare, detached));
                    path = null;
                    head = null;
                    branch = null;
                    bare = false;
                    detached = false;
                }

                continue;
            }

            if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                path = line["worktree ".Length..];
            }
            else if (line.StartsWith("HEAD ", StringComparison.Ordinal))
            {
                head = line["HEAD ".Length..];
            }
            else if (line.StartsWith("branch ", StringComparison.Ordinal))
            {
                var reference = line["branch ".Length..];
                branch = reference.StartsWith(BranchPrefix, StringComparison.Ordinal)
                    ? reference[BranchPrefix.Length..]
                    : reference;
            }
            else if (line is "bare")
            {
                bare = true;
            }
            else if (line is "detached")
            {
                detached = true;
            }
        }

        if (path is not null)
        {
            worktrees.Add(new GitWorktree(path, head, branch, bare, detached));
        }

        return worktrees;
    }
}
