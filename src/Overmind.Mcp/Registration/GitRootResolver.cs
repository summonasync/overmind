namespace Overmind.Mcp.Registration;

public static class GitRootResolver
{
    public static string? FindRoot(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            var gitMarker = Path.Combine(directory.FullName, ".git");
            if (Path.Exists(gitMarker))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
