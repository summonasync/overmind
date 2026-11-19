using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Overmind.Mcp;

public sealed class WorkspaceIdentityFile(string? rootDirectory = null, string? workingDirectory = null)
{
    private readonly string? _rootDirectory = rootDirectory;
    private readonly string? _workingDirectory = workingDirectory;

    public SessionIdentitySnapshot? TryRead()
    {
        var path = FilePath();
        if (!File.Exists(path))
        {
            return null;
        }

        string content;
        try
        {
            content = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Console.Error.WriteLine($"[overmind-mcp] WARN: workspace identity file unreadable: {ex.Message}");
            return null;
        }

        try
        {
            var saved = JsonSerializer.Deserialize<SessionIdentitySnapshot>(content, SessionIdentityFiles.PayloadOptions);
            return Restorable(saved) ? saved : null;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine(
                $"[overmind-mcp] WARN: workspace identity file unparsable, ignoring it: {ex.Message}");
            return null;
        }
    }

    public void Write(SessionIdentitySnapshot snapshot)
    {
        if (!Restorable(snapshot))
        {
            return;
        }

        SessionIdentityFiles.Write(FilePath(), snapshot);
    }

    private string FilePath() =>
        Path.Combine(
            SessionIdentityFiles.IdentityDirectory(_rootDirectory),
            $"workspace-{WorkspaceKey()}.json");

    private string WorkspaceKey()
    {
        var cwd = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(_workingDirectory ?? Environment.CurrentDirectory));
        var root = ProjectPinFile.GitRoot(cwd) ?? cwd;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root)))[..16].ToLowerInvariant();
    }

    private static bool Restorable(SessionIdentitySnapshot? snapshot) =>
        snapshot is { } saved
        && !AgentIdentity.IsPlaceholderAlias(saved.Alias)
        && !string.IsNullOrWhiteSpace(saved.Vendor);
}
