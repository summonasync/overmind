using System.Security.Cryptography;
using Overmind.Infrastructure.Planning;

namespace Overmind.Infrastructure.Materialization;

public static class MirrorVerifier
{
    private const string BacklogIndexRelativePath = "BACKLOG.md";

    public static async Task<MirrorVerificationResult> VerifyAsync(
        string root, IReadOnlyCollection<MirrorFileRow> rows, CancellationToken ct)
    {
        var drifted = new List<string>();
        var missing = new List<string>();

        foreach (var row in rows)
        {
            var absolute = PathGuard.ResolveUnderRoot(root, row.RelativePath);
            if (!File.Exists(absolute))
            {
                missing.Add(row.RelativePath);
                continue;
            }

            if (!await MatchesTrackedCopyAsync(absolute, row.RelativePath, row.Sha256, ct))
            {
                drifted.Add(row.RelativePath);
            }
        }

        return new MirrorVerificationResult(rows.Count, drifted, missing);
    }

    private static async Task<bool> MatchesTrackedCopyAsync(
        string absolute, string relativePath, string sha256, CancellationToken ct)
    {
        try
        {
            if (PathGuard.PathsEqual(relativePath, BacklogIndexRelativePath))
            {
                var content = await File.ReadAllTextAsync(absolute, ct);
                return ManagedBlock.TryExtract(content, out var block) &&
                       string.Equals(ContentHash.Sha256(block), sha256, StringComparison.OrdinalIgnoreCase);
            }

            var bytes = await File.ReadAllBytesAsync(absolute, ct);
            return string.Equals(
                Convert.ToHexString(SHA256.HashData(bytes)), sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
