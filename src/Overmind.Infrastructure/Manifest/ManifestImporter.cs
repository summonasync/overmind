using Overmind.Core;
using Overmind.Infrastructure.Materialization;

namespace Overmind.Infrastructure.Manifest;

public sealed class ManifestImporter
{
    private static readonly string[] Candidates = ["AGENTS.md", "CLAUDE.md", "QWEN.md"];

    public async Task<ProjectManifestImport?> ImportAsync(string projectRoot, CancellationToken ct)
    {
        if (!Path.IsPathFullyQualified(projectRoot))
        {
            throw MaterializationException.NotAbsolute(projectRoot, projectRoot);
        }

        var root = Path.GetFullPath(projectRoot);

        foreach (var candidate in Candidates)
        {
            var path = PathGuard.ResolveUnderRoot(root, candidate);
            if (!File.Exists(path))
            {
                continue;
            }

            string content;
            try
            {
                if (new FileInfo(path).Length > 4L * Project.MaxManifestLength)
                {
                    // UTF-8 needs at most 4 bytes per character; anything past this bound cannot
                    // fit under the stored-manifest cap. Fail loud instead of silently importing
                    // a lower-priority candidate's manifest.
                    throw MaterializationException.InvalidTarget(
                        path,
                        $"{candidate} exceeds the {Project.MaxManifestLength} character manifest limit; trim it before importing.");
                }

                content = await File.ReadAllTextAsync(path, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw MaterializationException.IoFailure(path, ex);
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            return new ProjectManifestImport(candidate, $"<!-- imported from {candidate} -->\n\n{content.Trim()}");
        }

        return null;
    }
}
