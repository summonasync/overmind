using System.Text.Json;

namespace Overmind.Mcp;

public sealed class SessionIdentityFileWriter(IParentProcessIdSource parentProcessId, string? rootDirectory = null)
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly string? _rootDirectory = rootDirectory;

    public void Write(string alias, string vendor, Guid sessionId)
    {
        if (parentProcessId.Resolve() is not { } parentPid)
        {
            return;
        }

        var target = Path.Combine(IdentityDirectory(), $"parent-{parentPid}.json");
        var temp = $"{target}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(
                temp,
                JsonSerializer.Serialize(new SessionIdentitySnapshot(alias, vendor, sessionId, DateTime.UtcNow), PayloadOptions));
            File.Move(temp, target, true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"[overmind-mcp] WARN: session identity file not updated: {ex.Message}");
            try
            {
                File.Delete(temp);
            }
            catch (Exception)
            {
            }
        }
    }

    private string IdentityDirectory()
    {
        var root = _rootDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".overmind");
        return Path.Combine(root, "identity");
    }
}
