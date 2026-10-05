namespace Overmind.Mcp;

public static class GitBranchListParser
{
    public static IReadOnlyList<GitBranchHead> Parse(string output)
    {
        var heads = new List<GitBranchHead>();
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split('\t');
            if (parts.Length == 3)
            {
                heads.Add(new GitBranchHead(parts[0], parts[1], parts[2]));
            }
        }

        return heads;
    }
}
