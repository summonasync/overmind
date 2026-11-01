using System.Text;
using System.Text.Json;

namespace Overmind.Infrastructure.Wake;

public sealed class WakeDropWriter(string? rootDirectory = null)
{
    public const string DirectoryVariable = "OVERMIND_WAKE_DIR";

    private const UnixFileMode DirectoryPermissions =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode FilePermissions =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(7);

    public static string RootDirectory =>
        Environment.GetEnvironmentVariable(DirectoryVariable)
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".overmind",
            "wake");

    private readonly string _root = rootDirectory ?? RootDirectory;

    public async Task WriteAsync(WakeDrop drop, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_root);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_root, DirectoryPermissions);
        }

        var name = FileName(drop.Alias);
        var temp = Path.Combine(_root, $"{name}.json.{Guid.NewGuid():N}.tmp");
        var payload = JsonSerializer.SerializeToUtf8Bytes(drop, JsonOptions);

        try
        {
            await WritePayloadAsync(temp, payload, ct);
            File.Move(temp, Path.Combine(_root, $"{name}.json"), overwrite: true);
        }
        catch
        {
            File.Delete(temp);
            throw;
        }
    }

    public int SweepStale()
    {
        if (!Directory.Exists(_root))
        {
            return 0;
        }

        var cutoff = DateTime.UtcNow - StaleAfter;
        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(_root, "*.json"))
        {
            if (File.GetLastWriteTimeUtc(file) >= cutoff)
            {
                continue;
            }

            File.Delete(file);
            removed++;
        }

        return removed;
    }

    private async Task WritePayloadAsync(string path, byte[] payload, CancellationToken ct)
    {
        using var stream = new FileStream(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, FilePermissions);
        }

        await stream.WriteAsync(payload, ct);
    }

    private static string FileName(string alias)
    {
        var name = new StringBuilder(alias.Length);
        foreach (var c in alias.ToLowerInvariant())
        {
            name.Append(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' ? c : '_');
        }

        return name.ToString();
    }
}
