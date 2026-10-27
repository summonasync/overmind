namespace Overmind.Mcp;

public static class GitRepositoryProbe
{
    public static bool IsRepository(string startPath)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startPath));
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                File.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return true;
            }

            if (File.Exists(Path.Combine(dir.FullName, "HEAD")) &&
                Directory.Exists(Path.Combine(dir.FullName, "objects")) &&
                Directory.Exists(Path.Combine(dir.FullName, "refs")))
            {
                return true;
            }

            dir = dir.Parent;
        }

        return false;
    }
}
