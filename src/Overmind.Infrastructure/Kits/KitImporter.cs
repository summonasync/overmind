using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Kits;

public sealed class KitImporter
{
    internal const string LibraryProjectName = "Kit Library";

    private const string SkillFileName = "SKILL.md";

    private readonly OvermindDbContext _db;
    private readonly KitScanner _scanner;
    private readonly PlatformDirectoryMap _platformMap;

    public KitImporter(OvermindDbContext db, KitScanner scanner, PlatformDirectoryMap platformMap)
    {
        _db = db;
        _scanner = scanner;
        _platformMap = platformMap;
    }

    public async Task<KitImportReport> CommitAsync(
        string scanPath, IReadOnlyList<KitImportSelection> selections, bool bundled = false,
        CancellationToken ct = default)
    {
        var scan = await _scanner.ScanAsync(scanPath, ct);
        var libraryProjectId = await EnsureLibraryProjectAsync(ct);

        var report = new KitImportReport { ScannedPath = scan.ScannedPath };
        foreach (var selection in selections)
        {
            ct.ThrowIfCancellationRequested();
            report.Kits.Add(await CommitKitAsync(scan, selection, libraryProjectId, bundled, ct));
        }

        return report;
    }

    public async Task<SkillKit> ClearSourceAsync(Guid kitId, CancellationToken ct = default)
    {
        var kit = await _db.SkillKits.FirstOrDefaultAsync(k => k.Id == kitId, ct)
            ?? throw KitImportException.KitNotFound(kitId);

        kit.ClearSource();
        await _db.SaveChangesAsync(ct);
        return kit;
    }

    public async Task<KitReimportReport> ReimportAsync(
        Guid kitId, bool bundled = false, CancellationToken ct = default)
    {
        var kit = await _db.SkillKits
            .Include(k => k.SkillKitEntries)
            .ThenInclude(e => e.Entry)
            .ThenInclude(e => e.SkillFiles)
            .FirstOrDefaultAsync(k => k.Id == kitId, ct)
            ?? throw KitImportException.KitNotFound(kitId);

        if (kit.SourceKind == KitSourceKind.Bundled && !bundled)
        {
            throw KitImportException.BundledReimportBlocked(kitId);
        }

        var sourcePath = kit.SourcePath ?? throw KitImportException.NoSource(kitId);
        if (!Directory.Exists(sourcePath))
        {
            throw KitImportException.SourceMissing(sourcePath);
        }

        var scan = await _scanner.ScanAsync(sourcePath, ct);
        var source = await BuildSourceIndexAsync(scan, ct);

        var report = new KitReimportReport { KitId = kit.Id, ScannedPath = scan.ScannedPath };

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var libraryProjectId = await EnsureLibraryProjectAsync(ct);
            var importedLinks = kit.SkillKitEntries
                .Where(l => l.ImportedContentHash is not null)
                .ToList();

            await DiffSkillsAsync(kit, importedLinks, source.Skills, report, libraryProjectId, scan.ScannedPath, ct);
            await DiffRuleFilesAsync(kit, importedLinks, source.Gates, EntryType.Gate, report, libraryProjectId, ct);
            await DiffRuleFilesAsync(kit, importedLinks, source.Guards, EntryType.Guard, report, libraryProjectId, ct);

            if (bundled)
            {
                kit.MarkBundled(sourcePath);
            }
            else
            {
                kit.MarkImported(sourcePath);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return report;
    }

    private async Task<KitImportKitResult> CommitKitAsync(
        KitScanResult scan, KitImportSelection selection, Guid libraryProjectId, bool bundled, CancellationToken ct)
    {
        var name = selection.Name.Trim();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var candidate = scan.Candidates.FirstOrDefault(c =>
                string.Equals(c.RelativePath, selection.RelativePath, StringComparison.Ordinal))
                ?? throw KitImportException.CandidateNotFound(selection.RelativePath);

            if (name.Length == 0)
            {
                name = candidate.Name;
            }

            if (candidate.ReImportMatchKitId is { } reImportKitId)
            {
                throw KitImportException.ReImportRequired(selection.RelativePath, reImportKitId);
            }

            var takenNames = await _db.SkillKits.Select(k => k.Name).ToListAsync(ct);
            if (takenNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw KitImportException.DuplicateName(name);
            }

            var kit = SkillKit.Create(name, 1, string.Empty);
            if (bundled)
            {
                kit.MarkBundled(candidate.Path);
            }
            else
            {
                kit.MarkImported(candidate.Path);
            }

            var skillEntries = new List<(ContextEntry Entry, string RelativePath)>();
            foreach (var skill in candidate.Skills)
            {
                skillEntries.Add((await BuildSkillEntryAsync(scan.ScannedPath, skill, libraryProjectId, ct),
                    KitRelativeKey(candidate.RelativePath, skill.RelativePath)));
            }

            var ruleEntries = new List<ContextEntry>();
            ruleEntries.AddRange(await BuildRuleEntriesAsync(
                scan.ScannedPath, candidate.Gates, EntryType.Gate, libraryProjectId, ct));
            ruleEntries.AddRange(await BuildRuleEntriesAsync(
                scan.ScannedPath, candidate.Guards, EntryType.Guard, libraryProjectId, ct));

            _db.SkillKits.Add(kit);
            foreach (var (entry, relativePath) in skillEntries)
            {
                kit.AddEntry(entry.Id, ContentHash.Sha256(entry.Content), relativePath);
                _db.ContextEntries.Add(entry);
            }

            foreach (var entry in ruleEntries)
            {
                kit.AddEntry(entry.Id, ContentHash.Sha256(entry.Content));
                _db.ContextEntries.Add(entry);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return KitImportKitResult.Imported(name, kit.Id);
        }
        catch (KitImportException ex)
        {
            await tx.RollbackAsync(ct);
            return KitImportKitResult.Failed(name, ex.Kind, ex.Message);
        }
        catch (DbUpdateException ex) when (UniqueViolation.IsOn(ex, "SkillKits.Name"))
        {
            await tx.RollbackAsync(ct);
            DetachAdded();
            return KitImportKitResult.Failed(name, KitImportFailureKind.DuplicateName,
                $"Kit name '{name}' is already taken.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await tx.RollbackAsync(ct);
            DetachAdded();
            return KitImportKitResult.Failed(name, null, ex.Message);
        }
    }

    private void DetachAdded()
    {
        foreach (var tracked in _db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
        {
            tracked.State = EntityState.Detached;
        }
    }

    private async Task<ContextEntry> BuildSkillEntryAsync(
        string root, KitScanSkill skill, Guid projectId, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(Path.Combine(root, skill.RelativePath, SkillFileName), ct);
        var files = await BuildSkillFilesAsync(root, skill, content, ct);

        return ContextEntry.CreateSkill(projectId, skill.Name, content, files);
    }

    private static async Task<List<SkillFile>> BuildSkillFilesAsync(
        string root, KitScanSkill skill, string skillContent, CancellationToken ct)
    {
        var skillDir = Path.Combine(root, skill.RelativePath);
        var files = new List<SkillFile> { SkillFile.Create(skill.Platform, SkillFileName, skillContent) };

        var extras = Directory.EnumerateFiles(skillDir, "*", SearchOption.AllDirectories)
            .Select(path => (Path: path, Rel: Path.GetRelativePath(skillDir, path)))
            .OrderBy(t => t.Rel, StringComparer.Ordinal);
        foreach (var (path, rel) in extras)
        {
            if (string.Equals(rel, SkillFileName, StringComparison.Ordinal) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            files.Add(SkillFile.Create(skill.Platform, rel, await File.ReadAllTextAsync(path, ct)));
        }

        return files;
    }

    private static async Task<IReadOnlyList<ContextEntry>> BuildRuleEntriesAsync(
        string root, IReadOnlyList<KitScanGate> gates, EntryType type, Guid projectId, CancellationToken ct)
    {
        var entries = new List<ContextEntry>();
        foreach (var gate in gates)
        {
            var path = Path.Combine(root, gate.RelativePath);
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(path, ct);
            entries.Add(ContextEntry.Create(projectId, type, gate.Title, content));
        }

        return entries;
    }

    private async Task<SourceIndex> BuildSourceIndexAsync(KitScanResult scan, CancellationToken ct)
    {
        var rootCandidate = scan.Candidates.FirstOrDefault(c => c.RelativePath.Length == 0);
        IReadOnlyList<KitScanCandidate> candidates = rootCandidate is null
            ? scan.Candidates
            : [rootCandidate];

        var skills = new Dictionary<string, SourceSkill>(StringComparer.OrdinalIgnoreCase);
        var gates = new List<SourceFile>();
        var guards = new List<SourceFile>();

        foreach (var candidate in candidates)
        {
            foreach (var skill in candidate.Skills)
            {
                var key = NormalizeKey(skill.RelativePath);
                var content = await File.ReadAllTextAsync(
                    Path.Combine(scan.ScannedPath, skill.RelativePath, SkillFileName), ct);
                skills[key] = new SourceSkill(key, skill, content);
            }

            foreach (var gate in candidate.Gates)
            {
                gates.Add(await ReadSourceFileAsync(scan.ScannedPath, gate, EntryType.Gate, ct));
            }

            foreach (var guard in candidate.Guards)
            {
                guards.Add(await ReadSourceFileAsync(scan.ScannedPath, guard, EntryType.Guard, ct));
            }
        }

        return new SourceIndex(skills, gates, guards);
    }

    private static async Task<SourceFile> ReadSourceFileAsync(
        string root, KitScanGate gate, EntryType type, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(Path.Combine(root, gate.RelativePath), ct);
        return new SourceFile(NormalizeKey(gate.RelativePath), gate.Title, content, type);
    }

    private async Task DiffSkillsAsync(
        SkillKit kit,
        IReadOnlyList<SkillKitEntry> importedLinks,
        IReadOnlyDictionary<string, SourceSkill> sourceSkills,
        KitReimportReport report,
        Guid libraryProjectId,
        string root,
        CancellationToken ct)
    {
        var entriesByKey = importedLinks
            .Where(l => l.Entry.Type == EntryType.Skill)
            .GroupBy(l => LinkSourceKey(l), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => new Queue<SkillKitEntry>(g.OrderBy(l => l.EntryId)), StringComparer.OrdinalIgnoreCase);

        foreach (var (key, source) in sourceSkills.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!entriesByKey.TryGetValue(key, out var entries) || entries.Count == 0)
            {
                await AddSourceSkillAsync(kit, source, report, libraryProjectId, root, ct);
                continue;
            }

            await DiffMatchedSkillAsync(entries.Dequeue(), source, report, root, ct);
        }

        foreach (var (_, entries) in entriesByKey.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            while (entries.Count > 0)
            {
                await RemoveEntryAsync(kit, entries.Dequeue(), report, ct);
            }
        }
    }

    private async Task DiffMatchedSkillAsync(
        SkillKitEntry link, SourceSkill source, KitReimportReport report, string root, CancellationToken ct)
    {
        var entry = link.Entry;

        if (string.IsNullOrWhiteSpace(source.Content))
        {
            report.Entries.Add(ConflictedEntry(source.Key, EntryType.Skill, entry.Title, link));
            return;
        }

        var sourceHash = ContentHash.Sha256(source.Content);
        if (string.Equals(sourceHash, link.ImportedContentHash, StringComparison.Ordinal))
        {
            report.Entries.Add(new KitReimportEntryResult
            {
                Outcome = KitReimportOutcome.KeptLocal,
                EntryType = EntryType.Skill,
                Title = entry.Title,
                RelativePath = source.Key,
                EntryId = entry.Id,
                ImportedContentHash = link.ImportedContentHash
            });
            return;
        }

        if (!string.Equals(ContentHash.Sha256(entry.Content), link.ImportedContentHash, StringComparison.Ordinal))
        {
            report.Entries.Add(ConflictedEntry(source.Key, EntryType.Skill, entry.Title, link));
            return;
        }

        entry.UpdateContent(source.Content);
        ResyncSkillFiles(entry, await BuildSkillFilesAsync(root, source.Skill, source.Content, ct));
        link.UpdateImportedContentHash(sourceHash);
        report.Entries.Add(new KitReimportEntryResult
        {
            Outcome = KitReimportOutcome.Updated,
            EntryType = EntryType.Skill,
            Title = entry.Title,
            RelativePath = source.Key,
            EntryId = entry.Id,
            ImportedContentHash = sourceHash
        });
    }

    private async Task AddSourceSkillAsync(
        SkillKit kit, SourceSkill source, KitReimportReport report, Guid libraryProjectId, string root, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(source.Content))
        {
            report.Entries.Add(RefusedAdd(source.Key, EntryType.Skill, source.Skill.Name));
            return;
        }

        var files = await BuildSkillFilesAsync(root, source.Skill, source.Content, ct);
        var entry = ContextEntry.CreateSkill(libraryProjectId, source.Skill.Name, source.Content, files);
        var sourceHash = ContentHash.Sha256(source.Content);

        kit.AddEntry(entry.Id, sourceHash, source.Key);
        _db.ContextEntries.Add(entry);

        report.Entries.Add(new KitReimportEntryResult
        {
            Outcome = KitReimportOutcome.Added,
            EntryType = EntryType.Skill,
            Title = entry.Title,
            RelativePath = source.Key,
            EntryId = entry.Id,
            ImportedContentHash = sourceHash
        });
    }

    private async Task DiffRuleFilesAsync(
        SkillKit kit,
        IReadOnlyList<SkillKitEntry> importedLinks,
        IReadOnlyList<SourceFile> sourceFiles,
        EntryType type,
        KitReimportReport report,
        Guid libraryProjectId,
        CancellationToken ct)
    {
        var sourceByTitle = sourceFiles
            .GroupBy(f => f.Title, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => new Queue<SourceFile>(g.OrderBy(f => f.RelativePath, StringComparer.Ordinal)), StringComparer.Ordinal);

        var entriesByTitle = importedLinks
            .Where(l => l.Entry.Type == type)
            .GroupBy(l => l.Entry.Title, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => new Queue<SkillKitEntry>(g.OrderBy(l => l.EntryId)), StringComparer.Ordinal);

        foreach (var (title, queue) in sourceByTitle.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            while (queue.Count > 0)
            {
                var file = queue.Dequeue();
                if (entriesByTitle.TryGetValue(title, out var entries) && entries.Count > 0)
                {
                    DiffMatchedRuleFile(entries.Dequeue(), file, report);
                    continue;
                }

                AddRuleFile(kit, file, report, libraryProjectId);
            }
        }

        foreach (var (_, entries) in entriesByTitle.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            while (entries.Count > 0)
            {
                await RemoveEntryAsync(kit, entries.Dequeue(), report, ct);
            }
        }
    }

    private static void DiffMatchedRuleFile(SkillKitEntry link, SourceFile file, KitReimportReport report)
    {
        var entry = link.Entry;

        if (string.IsNullOrWhiteSpace(file.Content))
        {
            report.Entries.Add(ConflictedEntry(file.RelativePath, file.Type, entry.Title, link));
            return;
        }

        var sourceHash = ContentHash.Sha256(file.Content);
        if (string.Equals(sourceHash, link.ImportedContentHash, StringComparison.Ordinal))
        {
            report.Entries.Add(new KitReimportEntryResult
            {
                Outcome = KitReimportOutcome.KeptLocal,
                EntryType = file.Type,
                Title = entry.Title,
                RelativePath = file.RelativePath,
                EntryId = entry.Id,
                ImportedContentHash = link.ImportedContentHash
            });
            return;
        }

        if (!string.Equals(ContentHash.Sha256(entry.Content), link.ImportedContentHash, StringComparison.Ordinal))
        {
            report.Entries.Add(ConflictedEntry(file.RelativePath, file.Type, entry.Title, link));
            return;
        }

        entry.UpdateContent(file.Content);
        link.UpdateImportedContentHash(sourceHash);
        report.Entries.Add(new KitReimportEntryResult
        {
            Outcome = KitReimportOutcome.Updated,
            EntryType = file.Type,
            Title = entry.Title,
            RelativePath = file.RelativePath,
            EntryId = entry.Id,
            ImportedContentHash = sourceHash
        });
    }

    private void AddRuleFile(SkillKit kit, SourceFile file, KitReimportReport report, Guid libraryProjectId)
    {
        if (string.IsNullOrWhiteSpace(file.Content))
        {
            report.Entries.Add(RefusedAdd(file.RelativePath, file.Type, file.Title));
            return;
        }

        var entry = ContextEntry.Create(libraryProjectId, file.Type, file.Title, file.Content);
        var sourceHash = ContentHash.Sha256(file.Content);

        kit.AddEntry(entry.Id, sourceHash);
        _db.ContextEntries.Add(entry);

        report.Entries.Add(new KitReimportEntryResult
        {
            Outcome = KitReimportOutcome.Added,
            EntryType = file.Type,
            Title = entry.Title,
            RelativePath = file.RelativePath,
            EntryId = entry.Id,
            ImportedContentHash = sourceHash
        });
    }

    private async Task RemoveEntryAsync(SkillKit kit, SkillKitEntry link, KitReimportReport report, CancellationToken ct)
    {
        var entry = link.Entry;

        _db.SkillKitEntries.Remove(link);

        var shared = await _db.SkillKitEntries.AnyAsync(l => l.EntryId == entry.Id && l.KitId != kit.Id, ct);
        if (!shared)
        {
            _db.ContextEntries.Remove(entry);
        }

        report.Entries.Add(new KitReimportEntryResult
        {
            Outcome = KitReimportOutcome.Removed,
            EntryType = entry.Type,
            Title = entry.Title,
            RelativePath = entry.Type == EntryType.Skill ? LinkSourceKey(link) : null,
            EntryId = entry.Id,
            EntryRowRetained = shared
        });
    }

    private void ResyncSkillFiles(ContextEntry entry, List<SkillFile> files)
    {
        _db.SkillFiles.RemoveRange(entry.SkillFiles);
        entry.SkillFiles.Clear();
        foreach (var file in files)
        {
            _db.SkillFiles.Add(file);
            entry.SkillFiles.Add(file);
        }
    }

    private string LinkSourceKey(SkillKitEntry link) =>
        link.RelativePath is { Length: > 0 } relativePath
            ? NormalizeKey(relativePath)
            : SkillSourceKey(link.Entry);

    private string SkillSourceKey(ContextEntry entry)
    {
        var platform = entry.SkillFiles.Select(f => f.Platform).FirstOrDefault(p => p.HasValue);
        var dir = platform.HasValue
            ? $"{_platformMap.GetSkillsDir(platform.Value)}/{entry.Title}"
            : entry.Title;
        return NormalizeKey(dir);
    }

    private static string KitRelativeKey(string candidateRelPath, string skillRelPath)
    {
        var prefix = candidateRelPath.Length == 0 ? string.Empty : candidateRelPath + Path.DirectorySeparatorChar;
        var stripped = prefix.Length > 0 && skillRelPath.StartsWith(prefix, StringComparison.Ordinal)
            ? skillRelPath[prefix.Length..]
            : skillRelPath;
        return NormalizeKey(stripped);
    }

    private static string NormalizeKey(string path) => path.Replace('\\', '/');

    private static KitReimportEntryResult ConflictedEntry(
        string relativePath, EntryType type, string title, SkillKitEntry link) =>
        new()
        {
            Outcome = KitReimportOutcome.Conflicted,
            EntryType = type,
            Title = title,
            RelativePath = relativePath,
            EntryId = link.Entry.Id,
            ImportedContentHash = link.ImportedContentHash
        };

    private static KitReimportEntryResult RefusedAdd(string relativePath, EntryType type, string title) =>
        new()
        {
            Outcome = KitReimportOutcome.Conflicted,
            EntryType = type,
            Title = title,
            RelativePath = relativePath
        };

    private sealed record SourceSkill(string Key, KitScanSkill Skill, string Content);

    private sealed record SourceFile(string RelativePath, string Title, string Content, EntryType Type);

    private sealed record SourceIndex(
        IReadOnlyDictionary<string, SourceSkill> Skills,
        IReadOnlyList<SourceFile> Gates,
        IReadOnlyList<SourceFile> Guards);

    private async Task<Guid> EnsureLibraryProjectAsync(CancellationToken ct)
    {
        var existingId = await FindLibraryProjectIdAsync(ct);
        if (existingId is { } libraryId)
        {
            return libraryId;
        }

        var dataSource = _db.Database.GetDbConnection().DataSource;
        var diskPath = Path.GetDirectoryName(dataSource);
        if (string.IsNullOrWhiteSpace(diskPath))
        {
            diskPath = dataSource;
        }

        var project = Project.Create(LibraryProjectName, diskPath);
        _db.Projects.Add(project);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.Entry(project).State = EntityState.Detached;
            var racedId = await FindLibraryProjectIdAsync(ct);
            if (racedId is { } winnerId)
            {
                return winnerId;
            }

            throw;
        }

        return project.Id;
    }

    private async Task<Guid?> FindLibraryProjectIdAsync(CancellationToken ct)
    {
        var normalized = Project.NormalizeName(LibraryProjectName);
        return await _db.Projects
            .Where(p => p.NameNormalized == normalized)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
    }
}
