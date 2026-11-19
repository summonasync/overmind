using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Kits;

public sealed class KitScanner
{
    public const int MaxDirectories = 500;
    public const long MaxTotalFileBytes = 10 * 1024 * 1024;

    private const string SkillFileName = "SKILL.md";

    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        "node_modules",
        "bin",
        "obj"
    };

    private static readonly string[] PlatformNativeFields =
    [
        "allowed-tools",
        "when_to_use",
        "when-to-use",
        "whenToUse",
        "arguments",
        "paths",
        "priority",
        "hooks"
    ];

    private readonly OvermindDbContext _db;
    private readonly IReadOnlyList<(AgentPlatform Platform, string[] Segments)> _platforms;

    public KitScanner(OvermindDbContext db, PlatformDirectoryMap platformMap)
    {
        _db = db;
        _platforms = Enum.GetValues<AgentPlatform>()
            .Select(p => (p, Segments: platformMap.GetSkillsDir(p).Split('/', StringSplitOptions.RemoveEmptyEntries)))
            .ToList();
    }

    public async Task<KitScanResult> ScanAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw KitImportException.PathMissing(path ?? string.Empty);
        }

        var root = Path.GetFullPath(path);
        if (File.Exists(root))
        {
            throw KitImportException.NotADirectory(root);
        }

        if (!Directory.Exists(root))
        {
            throw KitImportException.PathMissing(root);
        }

        var existingKits = (await _db.SkillKits.AsNoTracking()
                .Select(k => new { k.Id, k.Name, k.SourcePath })
                .ToListAsync(ct))
            .Select(k => new ExistingKit(k.Id, k.Name, k.SourcePath))
            .ToList();

        var walk = Walk(root, ct);

        var skills = walk.Dirs
            .Select(d => new SkillDirectory(d.FullPath, d.RelPath))
            .Where(d => walk.Files.ContainsKey(CombineRel(d.RelPath, SkillFileName)))
            .ToList();

        var topLevelDirs = walk.Dirs
            .Select(d => d.RelPath)
            .Where(rel => rel.Length > 0 && !rel.Contains(Path.DirectorySeparatorChar))
            .Where(rel => !IsPlatformPrefix(rel))
            .ToHashSet(StringComparer.Ordinal);

        var result = new KitScanResult { ScannedPath = root, Truncated = walk.Truncated };

        var candidateNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var top in topLevelDirs.Order(StringComparer.Ordinal))
        {
            var ownedPrefix = top + Path.DirectorySeparatorChar;
            var owned = skills
                .Where(s => s.RelPath.StartsWith(ownedPrefix, StringComparison.Ordinal))
                .ToList();

            // A named folder qualifies by owning any file at all: the kit split ships kits whose
            // membership is prose only (sociality), and a bundle that declares six kits has to
            // land six, not five plus a failed import on every install. The root case below keeps
            // the stricter rule — a stray README beside the kit folders is not a kit.
            if (owned.Count == 0 && !walk.Files.Keys.Any(f => f.StartsWith(ownedPrefix, StringComparison.Ordinal)))
            {
                continue;
            }

            candidateNames.Add(top);
            result.Candidates.Add(await BuildCandidateAsync(
                top, top, owned,
                file => file.StartsWith(ownedPrefix, StringComparison.Ordinal),
                walk, existingKits, root, ct));
        }

        var rootSkills = skills
            .Where(s => !candidateNames.Any(t =>
                s.RelPath.StartsWith(t + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            .ToList();
        if (rootSkills.Count > 0 || HasGateFiles(walk.Files, string.Empty))
        {
            var rootName = new DirectoryInfo(root).Name;
            result.Candidates.Insert(0, await BuildCandidateAsync(
                rootName, string.Empty, rootSkills,
                file => !candidateNames.Any(t =>
                    file.StartsWith(t + Path.DirectorySeparatorChar, StringComparison.Ordinal)),
                walk, existingKits, root, ct));
        }

        return result;
    }

    private async Task<KitScanCandidate> BuildCandidateAsync(
        string name,
        string relPath,
        IReadOnlyList<SkillDirectory> ownedSkills,
        Func<string, bool> ownsFile,
        WalkResult walk,
        IReadOnlyList<ExistingKit> existingKits,
        string root,
        CancellationToken ct)
    {
        var fullPath = relPath.Length == 0 ? root : Path.Combine(root, relPath);

        var collision = existingKits
            .FirstOrDefault(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase));
        var reImport = existingKits.FirstOrDefault(k =>
            k.SourcePath is not null &&
            string.Equals(NormalizePath(k.SourcePath), fullPath, StringComparison.Ordinal));

        var candidate = new KitScanCandidate
        {
            Name = name,
            Path = fullPath,
            RelativePath = relPath,
            NameCollision = collision is not null,
            NameCollisionKitId = collision?.Id,
            ReImportMatchKitId = reImport?.Id
        };

        var handledFiles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var skill in ownedSkills.OrderBy(s => s.RelPath, StringComparer.Ordinal))
        {
            var skillFileRel = CombineRel(skill.RelPath, SkillFileName);
            handledFiles.Add(skillFileRel);

            var skillPrefix = skill.RelPath.Length == 0
                ? null
                : skill.RelPath + Path.DirectorySeparatorChar;
            var fileCount = skillPrefix is null
                ? walk.Files.Keys.Count(ownsFile)
                : walk.Files.Keys.Count(f => f.StartsWith(skillPrefix, StringComparison.Ordinal));

            var platform = DetectPlatform(skill.FullPath);
            candidate.Skills.Add(new KitScanSkill
            {
                Name = Path.GetFileName(skill.FullPath),
                RelativePath = skill.RelPath,
                Platform = platform,
                FileCount = fileCount
            });

            string content;
            try
            {
                content = await File.ReadAllTextAsync(Path.Combine(root, skillFileRel), ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                candidate.Issues.Add(new KitScanIssue
                {
                    RelativePath = skillFileRel,
                    Code = KitScanIssueCode.UnreadableFile,
                    Message = "Skill file could not be read."
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                candidate.Issues.Add(new KitScanIssue
                {
                    RelativePath = skillFileRel,
                    Code = KitScanIssueCode.EmptyFile,
                    Message = "Skill file is empty."
                });
                continue;
            }

            candidate.Issues.AddRange(ValidateSkillFile(
                Path.GetFileName(skill.FullPath), skillFileRel, content, platform));
        }

        await AddGatesAsync(candidate.Gates, "gates", relPath, walk, handledFiles, candidate.Issues, root, ct);
        await AddGatesAsync(candidate.Guards, "guards", relPath, walk, handledFiles, candidate.Issues, root, ct);

        foreach (var fileRel in walk.Files.Keys.Order(StringComparer.Ordinal))
        {
            if (!ownsFile(fileRel) || handledFiles.Contains(fileRel))
            {
                continue;
            }

            if (walk.Files[fileRel] == 0)
            {
                candidate.Issues.Add(new KitScanIssue
                {
                    RelativePath = fileRel,
                    Code = KitScanIssueCode.EmptyFile,
                    Message = "File is empty."
                });
            }
        }

        foreach (var unreadable in walk.UnreadablePaths.Where(ownsFile))
        {
            candidate.Issues.Add(new KitScanIssue
            {
                RelativePath = unreadable,
                Code = KitScanIssueCode.UnreadableFile,
                Message = "Path could not be read."
            });
        }

        foreach (var link in walk.SymbolicLinkPaths.Where(ownsFile))
        {
            candidate.Issues.Add(new KitScanIssue
            {
                RelativePath = link,
                Code = KitScanIssueCode.UnreadableFile,
                Message = "Symbolic links are not supported."
            });
        }

        var ordered = candidate.Issues
            .OrderBy(i => i.RelativePath, StringComparer.Ordinal)
            .ThenBy(i => i.Code)
            .ToList();
        candidate.Issues.Clear();
        candidate.Issues.AddRange(ordered);

        return candidate;
    }

    private static List<KitScanIssue> ValidateSkillFile(
        string skillDirName, string skillFileRel, string content, AgentPlatform? platform)
    {
        var issues = new List<KitScanIssue>();

        if (!SkillFrontmatter.TryParse(content, out var fields, out var error))
        {
            issues.Add(new KitScanIssue
            {
                RelativePath = skillFileRel,
                Code = KitScanIssueCode.InvalidFrontmatter,
                Message = error ?? "Frontmatter is not valid."
            });
            return issues;
        }

        if (!fields.TryGetValue("name", out var name) || !IsCanonicalSkillName(name))
        {
            issues.Add(new KitScanIssue
            {
                RelativePath = skillFileRel,
                Code = KitScanIssueCode.InvalidName,
                Message = fields.ContainsKey("name")
                    ? $"Name '{name}' is not canonical (lowercase letters, digits and hyphens, max 64 chars, no leading/trailing hyphen, no double hyphen)."
                    : "Frontmatter is missing the required 'name' field."
            });
        }
        else if (!string.Equals(name, skillDirName, StringComparison.Ordinal))
        {
            issues.Add(new KitScanIssue
            {
                RelativePath = skillFileRel,
                Code = KitScanIssueCode.InvalidName,
                Message = $"Name '{name}' does not match the directory name '{skillDirName}'."
            });
        }

        if (!fields.TryGetValue("description", out var description) || string.IsNullOrWhiteSpace(description))
        {
            issues.Add(new KitScanIssue
            {
                RelativePath = skillFileRel,
                Code = KitScanIssueCode.InvalidDescription,
                Message = "Frontmatter is missing a non-empty 'description' field."
            });
        }
        else if (description.Length > 1024)
        {
            issues.Add(new KitScanIssue
            {
                RelativePath = skillFileRel,
                Code = KitScanIssueCode.InvalidDescription,
                Message = $"Description is {description.Length} chars, exceeding the 1024-char limit."
            });
        }

        if (fields.TryGetValue("type", out var type) && type != "prompt")
        {
            issues.Add(new KitScanIssue
            {
                RelativePath = skillFileRel,
                Code = KitScanIssueCode.InvalidType,
                Message = $"Type '{type}' is not supported; omit the field or use 'prompt'."
            });
        }

        if (platform is null)
        {
            foreach (var nativeField in PlatformNativeFields)
            {
                if (fields.ContainsKey(nativeField))
                {
                    issues.Add(new KitScanIssue
                    {
                        RelativePath = skillFileRel,
                        Code = KitScanIssueCode.PlatformNativeField,
                        Message = $"Platform-agnostic skill uses platform-native field '{nativeField}'."
                    });
                }
            }
        }

        return issues;
    }

    private static bool IsCanonicalSkillName(string name) =>
        name.Length is > 0 and <= 64
        && name.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')
        && !name.StartsWith('-')
        && !name.EndsWith('-')
        && !name.Contains("--", StringComparison.Ordinal);

    private static async Task AddGatesAsync(
        List<KitScanGate> target,
        string kind,
        string candidateRel,
        WalkResult walk,
        HashSet<string> handledFiles,
        List<KitScanIssue> issues,
        string root,
        CancellationToken ct)
    {
        var dirRel = CombineRel(candidateRel, kind);
        var gateFiles = walk.Files.Keys
            .Where(f => string.Equals(ParentRel(f), dirRel, StringComparison.Ordinal)
                        && string.Equals(Path.GetExtension(f), ".md", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal);

        foreach (var fileRel in gateFiles)
        {
            handledFiles.Add(fileRel);

            string content;
            try
            {
                content = await File.ReadAllTextAsync(Path.Combine(root, fileRel), ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                issues.Add(new KitScanIssue
                {
                    RelativePath = fileRel,
                    Code = KitScanIssueCode.UnreadableFile,
                    Message = $"{kind} file could not be read."
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                issues.Add(new KitScanIssue
                {
                    RelativePath = fileRel,
                    Code = KitScanIssueCode.EmptyFile,
                    Message = $"{kind} file is empty."
                });
                continue;
            }

            var title = ExtractTitle(content) ?? Path.GetFileNameWithoutExtension(fileRel);
            target.Add(new KitScanGate { RelativePath = fileRel, Title = title });
        }
    }

    private static string? ExtractTitle(string content)
    {
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (!trimmed.StartsWith("# "))
            {
                continue;
            }

            var title = trimmed[2..].Trim();
            if (title.Length > 0)
            {
                return title;
            }
        }

        return null;
    }

    private WalkResult Walk(string root, CancellationToken ct)
    {
        var dirs = new List<VisitedDirectory>();
        var files = new Dictionary<string, long>(StringComparer.Ordinal);
        var unreadable = new List<string>();
        var symbolicLinks = new List<string>();
        long totalBytes = 0;
        var truncated = false;

        var pending = new Stack<(string Full, string Rel)>();
        pending.Push((root, string.Empty));

        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var (dir, rel) = pending.Pop();

            if (dirs.Count >= MaxDirectories)
            {
                truncated = true;
                break;
            }
            dirs.Add(new VisitedDirectory(dir, rel));

            List<string> entries;
            try
            {
                entries = [.. Directory.EnumerateFileSystemEntries(dir)];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                unreadable.Add(rel);
                continue;
            }

            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);
                var entryRel = CombineRel(rel, name);

                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    unreadable.Add(entryRel);
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if ((attributes & FileAttributes.ReparsePoint) != 0 || IsIgnoredDirectory(name))
                    {
                        continue;
                    }

                    pending.Push((entry, entryRel));
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    symbolicLinks.Add(entryRel);
                    continue;
                }

                long length;
                try
                {
                    length = new FileInfo(entry).Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    unreadable.Add(entryRel);
                    continue;
                }

                totalBytes += length;
                if (totalBytes > MaxTotalFileBytes)
                {
                    truncated = true;
                    pending.Clear();
                    break;
                }

                files[entryRel] = length;
            }
        }

        return new WalkResult(dirs, files, unreadable, symbolicLinks, truncated);
    }

    private AgentPlatform? DetectPlatform(string skillFullPath)
    {
        foreach (var (platform, segments) in _platforms)
        {
            var ancestor = Directory.GetParent(skillFullPath);
            var match = true;
            for (var i = segments.Length - 1; i >= 0; i--)
            {
                if (ancestor is null ||
                    !string.Equals(ancestor.Name, segments[i], StringComparison.OrdinalIgnoreCase))
                {
                    match = false;
                    break;
                }

                ancestor = ancestor.Parent;
            }

            if (match)
            {
                return platform;
            }
        }

        return null;
    }

    private bool IsIgnoredDirectory(string name) =>
        IgnoredDirectoryNames.Contains(name) ||
        (name.StartsWith('.') && !IsPlatformDirectoryName(name));

    private bool IsPlatformDirectoryName(string name) =>
        _platforms.Any(p =>
            p.Segments.Length > 0 &&
            string.Equals(p.Segments[0], name, StringComparison.OrdinalIgnoreCase));

    private bool IsPlatformPrefix(string relPath)
    {
        var segments = relPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        foreach (var (_, platformSegments) in _platforms)
        {
            if (segments.Length > platformSegments.Length)
            {
                continue;
            }

            var isPrefix = true;
            for (var i = 0; i < segments.Length; i++)
            {
                if (!string.Equals(segments[i], platformSegments[i], StringComparison.OrdinalIgnoreCase))
                {
                    isPrefix = false;
                    break;
                }
            }

            if (isPrefix)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasGateFiles(IReadOnlyDictionary<string, long> files, string candidateRel)
    {
        var gatesRel = CombineRel(candidateRel, "gates");
        var guardsRel = CombineRel(candidateRel, "guards");
        return files.Keys.Any(f =>
            string.Equals(Path.GetExtension(f), ".md", StringComparison.OrdinalIgnoreCase) &&
            ParentRel(f) is { } parent &&
            (string.Equals(parent, gatesRel, StringComparison.Ordinal) ||
             string.Equals(parent, guardsRel, StringComparison.Ordinal)));
    }

    private static string CombineRel(string relPath, string name) =>
        relPath.Length == 0 ? name : relPath + Path.DirectorySeparatorChar + name;

    private static string? ParentRel(string relPath)
    {
        var separatorIndex = relPath.LastIndexOf(Path.DirectorySeparatorChar);
        return separatorIndex < 0 ? string.Empty : relPath[..separatorIndex];
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    private sealed record ExistingKit(Guid Id, string Name, string? SourcePath);

    private sealed record SkillDirectory(string FullPath, string RelPath);

    private sealed record VisitedDirectory(string FullPath, string RelPath);

    private sealed record WalkResult(
        List<VisitedDirectory> Dirs,
        Dictionary<string, long> Files,
        List<string> UnreadablePaths,
        List<string> SymbolicLinkPaths,
        bool Truncated);
}
