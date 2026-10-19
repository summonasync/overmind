namespace Overmind.Infrastructure.Grimoire;

public sealed class GrimoireBinaryLocator : IGrimoireBinaryLocator
{
    public const string BinaryName = "grimoire";

    private static readonly string[] WindowsExtensions = [".exe", ".cmd", ".bat"];

    private readonly Func<string, bool> _fileExists;
    private readonly string? _pathVariable;

    public GrimoireBinaryLocator(Func<string, bool>? fileExists = null, string? pathVariable = null)
    {
        _fileExists = fileExists ?? File.Exists;
        _pathVariable = pathVariable ?? Environment.GetEnvironmentVariable("PATH");
    }

    public string? Locate(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath) && _fileExists(overridePath))
        {
            return overridePath;
        }

        return FindOnPath();
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
