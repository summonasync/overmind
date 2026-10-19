namespace Overmind.Infrastructure.Grimoire;

public sealed class OvermindCliLocator : IOvermindCliLocator
{
    public const string BinaryName = "overmind-mcp";

    private static readonly string[] WindowsExtensions = [".exe"];

    private readonly string _baseDirectory;
    private readonly Func<string, bool> _fileExists;
    private readonly string? _pathVariable;

    public OvermindCliLocator(
        string? baseDirectory = null,
        Func<string, bool>? fileExists = null,
        string? pathVariable = null)
    {
        _baseDirectory = baseDirectory ?? AppContext.BaseDirectory;
        _fileExists = fileExists ?? File.Exists;
        _pathVariable = pathVariable ?? Environment.GetEnvironmentVariable("PATH");
    }

    public string? Locate()
    {
        var packed = FindPackedLayout();
        if (packed is not null)
        {
            return packed;
        }

        return FindOnPath();
    }

    private string? FindPackedLayout()
    {
        var mcpDirectory = Path.GetFullPath(Path.Combine(_baseDirectory, "..", "mcp"));
        foreach (var candidate in CandidateFileNames())
        {
            var fullPath = Path.Combine(mcpDirectory, candidate);
            if (_fileExists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private string? FindOnPath()
    {
        if (string.IsNullOrWhiteSpace(_pathVariable))
        {
            return null;
        }

        var directories = _pathVariable.Split(
            Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var directory in directories)
        {
            foreach (var candidate in CandidateFileNames())
            {
                var fullPath = Path.Combine(directory, candidate);
                if (_fileExists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateFileNames()
    {
        yield return BinaryName;
        if (OperatingSystem.IsWindows())
        {
            foreach (var extension in WindowsExtensions)
            {
                yield return BinaryName + extension;
            }
        }
    }
}
