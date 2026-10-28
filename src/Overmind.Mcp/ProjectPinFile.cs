using System.Text.Json;

namespace Overmind.Mcp;

public static class ProjectPinFile
{
    private const string PinRelativePath = ".overmind/project";

    public static ProjectPin Read(string startDirectory)
    {
        var current = FullPath(startDirectory);
        var bound = GitRoot(current) ?? current;
        while (true)
        {
            var pin = ReadAt(current);
            if (pin.FilePath is not null || current == bound)
            {
                return pin;
            }

            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null)
            {
                return pin;
            }

            current = FullPath(parent);
        }
    }

    private static ProjectPin ReadAt(string directory)
    {
        var filePath = Path.Combine(directory, ".overmind", "project");
        if (!File.Exists(filePath))
        {
            return new ProjectPin(null, null);
        }

        string content;
        try
        {
            content = File.ReadAllText(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new ProjectPin(null, filePath);
        }

        return Parse(content) is { } projectId
            ? new ProjectPin(projectId, filePath)
            : new ProjectPin(null, filePath);
    }

    private static Guid? Parse(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (Guid.TryParse(trimmed, out var raw))
        {
            return raw;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("projectId", out var projectId) &&
                   projectId.ValueKind == JsonValueKind.String &&
                   Guid.TryParse(projectId.GetString(), out var fromJson)
                ? fromJson
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GitRoot(string startDirectory)
    {
        var current = startDirectory;
        while (true)
        {
            var gitPath = Path.Combine(current, ".git");
            if (File.Exists(gitPath) || Directory.Exists(gitPath))
            {
                return current;
            }

            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null)
            {
                return null;
            }

            current = FullPath(parent);
        }
    }

    private static string FullPath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
