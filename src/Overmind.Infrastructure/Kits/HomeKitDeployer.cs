using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Kits;

// Feature 251: the home-deploy door behind IGlobalKitDeployer. Mirrors a kit's
// SourcePath into detected agents' home skills directories, tracks every written
// file in GlobalMaterializedFile, weaves prose kits into agent entry files inside
// managed marker blocks (GlobalMarkerDeployment), and never whole-directory-deletes
// anything under a home — dematerialize removes tracked rows only, and a missing
// home directory is skipped, never created.
public sealed class HomeKitDeployer : IGlobalKitDeployer
{
    private static readonly string[] InfraProseNames =
    [
        "README.md",
        "SYNC-STRUCTURE.md",
        "KIT-MEMBERSHIP.md",
        "BACKLOG.md",
    ];

    private readonly OvermindDbContext _db;
    private readonly PlatformDirectoryMap _platforms;
    private readonly IReadOnlyList<AgentHomeSpec> _agentHomes;
    private readonly ILogger<HomeKitDeployer> _logger;
    private readonly string _homeRoot;
    private readonly IReadOnlyDictionary<string, string>? _envOverride;

    public HomeKitDeployer(
        OvermindDbContext db,
        PlatformDirectoryMap platforms,
        IReadOnlyList<AgentHomeSpec> agentHomes,
        ILogger<HomeKitDeployer> logger,
        string? homeRootOverride = null,
        IReadOnlyDictionary<string, string>? envOverride = null)
    {
        _db = db;
        _platforms = platforms;
        _agentHomes = agentHomes;
        _logger = logger;
        _homeRoot = homeRootOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _envOverride = envOverride;
    }

    // Boot-seed contract: the mandatory kit goes to every detected agent.
    async Task IGlobalKitDeployer.DeployAsync(SkillKit kit, CancellationToken ct)
    {
        var installed = ResolveInstalled().ToList();
        if (installed.Count == 0)
        {
            _logger.LogInformation("No installed agents detected; global deploy of kit {KitId} skipped", kit.Id);
            return;
        }

        foreach (var home in installed)
        {
            await DeployCoreAsync(kit, home, force: true, ct);
        }
    }

    public async Task<GlobalDeployReport> DeployAsync(Guid kitId, string agent, bool force, CancellationToken ct = default)
    {
        var kit = await LoadKitAsync(kitId, ct) ?? throw MaterializationException.MissingKit(kitId);
        var home = ResolveAgent(agent) ?? throw MaterializationException.InvalidTarget(agent, $"agent '{agent}' is not installed");

        if (kit.SourceKind == KitSourceKind.Manual || kit.SourcePath is null)
        {
            throw MaterializationException.InvalidTarget($"kit:{kit.Name}", "manual kits carry no deploy source");
        }

        var report = await DeployCoreAsync(kit, home, force, ct);
        await RecordRunAsync(report, success: report.Success, ct);
        return report;
    }

    public async Task<GlobalDeployReport> DematerializeAsync(Guid kitId, string agent, CancellationToken ct = default)
    {
        var kit = await LoadKitAsync(kitId, ct) ?? throw MaterializationException.MissingKit(kitId);
        if (kit.Mandatory)
        {
            throw KitActivationException.MandatoryGlobalKit(kit.Id, kit.Name);
        }

        var home = ResolveAgent(agent) ?? throw MaterializationException.InvalidTarget(agent, $"agent '{agent}' is not installed");

        var started = DateTime.UtcNow;
        var entries = new List<GlobalDeployEntry>();
        var warnings = new List<string>();

        var rows = await _db.GlobalMaterializedFiles
            .Where(m => m.KitId == kitId && m.Platform == home.Platform)
            .ToListAsync(ct);
        var markerRows = await _db.GlobalMarkerDeployments
            .Where(m => m.KitId == kitId && m.Platform == home.Platform)
            .ToListAsync(ct);

        if (!Directory.Exists(home.SkillsDir) && rows.Count == 0 && markerRows.Count == 0)
        {
            return Finish(kitId, agent, started, entries, warnings);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in rows)
            {
                var absolute = PathGuard.ResolveUnderRoot(home.SkillsDir, row.RelativePath);
                if (File.Exists(absolute))
                {
                    File.Delete(absolute);
                    DirectoryPruner.PruneEmptyDirectories(absolute, home.SkillsDir);
                }

                _db.GlobalMaterializedFiles.Remove(row);
                entries.Add(new GlobalDeployEntry(GlobalDeployEntryKind.File, home.Platform, absolute, GlobalDeployOutcome.Written));
            }

            foreach (var markerRow in markerRows)
            {
                var entryPath = PathGuard.ResolveAbsoluteUnderRoot(home.ConfigDir, markerRow.EntryFilePath);
                if (File.Exists(entryPath))
                {
                    var text = await File.ReadAllTextAsync(entryPath, ct);
                    var (newText, removed) = MarkerBlockWriter.Cut(text, kit.Name);
                    if (removed)
                    {
                        if (string.IsNullOrWhiteSpace(newText))
                        {
                            File.Delete(entryPath);
                        }
                        else
                        {
                            await File.WriteAllTextAsync(entryPath, newText, ContentHash.Utf8NoBom, ct);
                        }

                        entries.Add(new GlobalDeployEntry(
                            GlobalDeployEntryKind.MarkerBlock, home.Platform, entryPath, GlobalDeployOutcome.Written));
                    }
                }

                _db.GlobalMarkerDeployments.Remove(markerRow);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await tx.RollbackAsync(ct);
            throw MaterializationException.IoFailure("home-dematerialize", ex);
        }

        var report = Finish(kitId, agent, started, entries, warnings);
        await RecordRunAsync(report, success: report.Success, ct);
        return report;
    }

    public async Task<IReadOnlyList<GlobalDriftItem>> DetectDriftAsync(CancellationToken ct = default)
    {
        var rows = await _db.GlobalMaterializedFiles.ToListAsync(ct);
        var markerRows = await _db.GlobalMarkerDeployments.ToListAsync(ct);
        var homes = ResolveInstalled().ToDictionary(h => h.Platform, h => h);

        var items = new List<GlobalDriftItem>();
        foreach (var row in rows)
        {
            if (!homes.TryGetValue(row.Platform, out var home))
            {
                items.Add(new GlobalDriftItem(row.KitId, row.Platform, row.RelativePath, GlobalDriftKind.Missing));
                continue;
            }

            var absolute = PathGuard.ResolveUnderRoot(home.SkillsDir, row.RelativePath);
            if (!File.Exists(absolute))
            {
                items.Add(new GlobalDriftItem(row.KitId, row.Platform, row.RelativePath, GlobalDriftKind.Missing));
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(absolute, ct);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            if (!string.Equals(hash, row.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new GlobalDriftItem(row.KitId, row.Platform, row.RelativePath, GlobalDriftKind.Modified));
            }
        }

        foreach (var markerRow in markerRows)
        {
            AgentHomeResolution.ResolvedHome? markerHome = null;
            if (homes.TryGetValue(markerRow.Platform, out var candidate))
            {
                try
                {
                    markerHome = candidate;
                    var entryPath = PathGuard.ResolveAbsoluteUnderRoot(markerHome.ConfigDir, markerRow.EntryFilePath);
                    if (!File.Exists(entryPath))
                    {
                        items.Add(new GlobalDriftItem(markerRow.KitId, markerRow.Platform, entryPath, GlobalDriftKind.MarkerMissing));
                        continue;
                    }

                    var text = await File.ReadAllTextAsync(entryPath, ct);
                    var blockHash = ExtractBlockHash(text, markerRow.KitId, entryPath);
                    if (blockHash is null)
                    {
                        items.Add(new GlobalDriftItem(markerRow.KitId, markerRow.Platform, entryPath, GlobalDriftKind.MarkerMissing));
                    }
                    else if (!string.Equals(blockHash, markerRow.ContentSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        items.Add(new GlobalDriftItem(markerRow.KitId, markerRow.Platform, entryPath, GlobalDriftKind.MarkerModified));
                    }
                }
                catch (MaterializationException)
                {
                    items.Add(new GlobalDriftItem(markerRow.KitId, markerRow.Platform, markerRow.EntryFilePath, GlobalDriftKind.MarkerMissing));
                }

                continue;
            }

            items.Add(new GlobalDriftItem(markerRow.KitId, markerRow.Platform, markerRow.EntryFilePath, GlobalDriftKind.MarkerMissing));
        }

        return items;
    }

    private async Task<GlobalDeployReport> DeployCoreAsync(SkillKit kit, AgentHomeResolution.ResolvedHome home, bool force, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var entries = new List<GlobalDeployEntry>();
        var warnings = new List<string>();

        var sourcePath = kit.SourcePath;
        if (kit.SourceKind == KitSourceKind.Manual || sourcePath is null)
        {
            entries.Add(new GlobalDeployEntry(
                GlobalDeployEntryKind.File, home.Platform, sourcePath ?? "(no source)", GlobalDeployOutcome.Skipped,
                "manual kits carry no deploy source"));
            return Finish(kit.Id, home.Platform.ToString(), started, entries, warnings);
        }

        if (!Directory.Exists(sourcePath))
        {
            entries.Add(new GlobalDeployEntry(
                GlobalDeployEntryKind.File, home.Platform, sourcePath, GlobalDeployOutcome.Skipped, "source path missing"));
            return Finish(kit.Id, home.Platform.ToString(), started, entries, warnings);
        }

        var skillDirs = Directory.EnumerateDirectories(sourcePath)
            .Where(d => File.Exists(Path.Combine(d, "SKILL.md")))
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();
        var proseFiles = Directory.EnumerateFiles(sourcePath, "*.md")
            .Select(p => Path.GetFileName(p))
            .Where(n => !InfraProseNames.Contains(n, StringComparer.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        if (skillDirs.Count == 0 && proseFiles.Count == 0)
        {
            entries.Add(new GlobalDeployEntry(
                GlobalDeployEntryKind.File, home.Platform, sourcePath, GlobalDeployOutcome.Skipped, "kit carries no deployable content"));
            return Finish(kit.Id, home.Platform.ToString(), started, entries, warnings);
        }

        // The mirror half needs the home skills dir to exist (skip, never create); the
        // prose-only weave half is independent — it writes the agent's entry file, which
        // lives under the detected config dir, so prose-only kits deploy without any
        // skills directory at all.
        var mirrorSkipped = false;
        if (skillDirs.Count > 0 && !Directory.Exists(home.SkillsDir))
        {
            mirrorSkipped = true;
            entries.Add(new GlobalDeployEntry(
                GlobalDeployEntryKind.File, home.Platform, home.SkillsDir, GlobalDeployOutcome.Skipped, "home skills dir missing"));
        }

        var plans = new List<HomeFilePlan>();
        var tracked = new List<GlobalMaterializedFile>();
        if (!mirrorSkipped)
        {
            foreach (var dir in skillDirs)
            {
                var dirName = Path.GetFileName(dir);
                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    var relative = $"{dirName}/{Path.GetRelativePath(dir, file).Replace(Path.DirectorySeparatorChar, '/')}";
                    plans.Add(new HomeFilePlan(Path.Combine(home.SkillsDir, relative.Replace('/', Path.DirectorySeparatorChar)), relative, await File.ReadAllTextAsync(file, ct)));
                }
            }

            tracked = await _db.GlobalMaterializedFiles
                .Where(m => m.KitId == kit.Id && m.Platform == home.Platform)
                .ToListAsync(ct);
        }

        var refused = new List<string>();
        var preImages = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var plan in plans)
        {
            var row = tracked.FirstOrDefault(m => PathGuard.PathsEqual(
                PathGuard.ResolveUnderRoot(home.SkillsDir, m.RelativePath), plan.AbsolutePath));

            if (row is null && File.Exists(plan.AbsolutePath))
            {
                if (!force)
                {
                    refused.Add(plan.AbsolutePath);
                    entries.Add(new GlobalDeployEntry(
                        GlobalDeployEntryKind.File, home.Platform, plan.AbsolutePath, GlobalDeployOutcome.Refused, "untracked file in the way"));
                    continue;
                }

                preImages[plan.AbsolutePath] = await File.ReadAllBytesAsync(plan.AbsolutePath, ct);
                warnings.Add($"Overwrote untracked file: {plan.AbsolutePath}");
            }

            if (row is not null && File.Exists(plan.AbsolutePath))
            {
                var bytes = await File.ReadAllBytesAsync(plan.AbsolutePath, ct);
                var diskHash = Convert.ToHexString(SHA256.HashData(bytes));
                var sourceHash = ContentHash.Sha256(plan.Content);
                if (!string.Equals(diskHash, row.Sha256, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(diskHash, sourceHash, StringComparison.OrdinalIgnoreCase))
                {
                    if (!force)
                    {
                        refused.Add(plan.AbsolutePath);
                        entries.Add(new GlobalDeployEntry(
                            GlobalDeployEntryKind.File, home.Platform, plan.AbsolutePath, GlobalDeployOutcome.Refused, "locally modified"));
                        continue;
                    }

                    preImages[plan.AbsolutePath] = bytes;
                    warnings.Add($"Overwrote locally modified file: {plan.AbsolutePath}");
                }
            }
        }

        if (refused.Count > 0)
        {
            var report = Finish(kit.Id, home.Platform.ToString(), started, entries, warnings);
            await RecordRunAsync(report, success: false, ct);
            throw MaterializationException.Refused(
                refused.Select(r => new PlanningMaterializationSkip(r, "home deploy refused")).ToList());
        }

        var written = new List<string>();
        var weaveUndos = new List<(string Path, string? Original)>();
        string? failedPath = null;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            if (!mirrorSkipped)
            {
                foreach (var plan in plans.Where(p => !refused.Contains(p.AbsolutePath)))
                {
                    failedPath = plan.AbsolutePath;
                    Directory.CreateDirectory(Path.GetDirectoryName(plan.AbsolutePath)!);
                    // Row hashes are computed over ContentHash-normalized content (LF), so the
                    // disk bytes must match that shape or every later guard call treats the
                    // door's own file as locally modified (251 hard review F1).
                    await File.WriteAllTextAsync(plan.AbsolutePath, ContentHash.Normalize(plan.Content), ContentHash.Utf8NoBom, ct);
                    written.Add(plan.AbsolutePath);

                var hash = ContentHash.Sha256(plan.Content);
                var row = tracked.FirstOrDefault(m => PathGuard.PathsEqual(
                    PathGuard.ResolveUnderRoot(home.SkillsDir, m.RelativePath), plan.AbsolutePath));
                if (row is not null)
                {
                    row.Refresh(hash);
                }
                else
                {
                    _db.GlobalMaterializedFiles.Add(GlobalMaterializedFile.Create(kit.Id, home.Platform, plan.RelativePath, hash));
                }

                entries.Add(new GlobalDeployEntry(GlobalDeployEntryKind.File, home.Platform, plan.AbsolutePath, GlobalDeployOutcome.Written));
            }

            PruneStaleRows(tracked, plans, home, warnings, entries);
            }

            // Prose-only gate (251 review B1): the weave surface belongs to zero-entry kits
            // alone; kits carrying entries (overmind-ceremony and every language crew) reach
            // homes' entry docs through install.sh, never through this door.
            var entryCount = await _db.SkillKitEntries.CountAsync(e => e.KitId == kit.Id, ct);
            if (entryCount == 0 && proseFiles.Count > 0)
            {
                var content = string.Join("\n\n",
                    proseFiles.Select(n => File.ReadAllText(Path.Combine(sourcePath, n))).Select(c => c.TrimEnd('\n')));
                weaveUndos.Add(await WeaveAsync(kit, home, content, entries, ct));
            }
            else
            {
                if (entryCount > 0 && proseFiles.Count > 0)
                {
                    entries.Add(new GlobalDeployEntry(
                        GlobalDeployEntryKind.MarkerBlock, home.Platform, "(entry files)", GlobalDeployOutcome.Skipped,
                        "kit prose ships through install.sh — weave is prose-only-kits only"));
                }

                weaveUndos.Add(await UnweaveAsync(kit, home, entries, ct));
            }

            failedPath = null;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is not MaterializationException and not OperationCanceledException)
        {
            await tx.RollbackAsync(ct);
            foreach (var (path, original) in weaveUndos)
            {
                if (original is null)
                {
                    DirectoryPruner.TryDelete(path);
                }
                else
                {
                    await File.WriteAllTextAsync(path, original, ContentHash.Utf8NoBom);
                }
            }

            foreach (var path in written)
            {
                if (preImages.TryGetValue(path, out var preImage))
                {
                    await File.WriteAllBytesAsync(path, preImage);
                }
                else
                {
                    DirectoryPruner.TryDelete(path);
                }
            }

            throw MaterializationException.IoFailure(failedPath ?? "database", ex);
        }

        return Finish(kit.Id, home.Platform.ToString(), started, entries, warnings);
    }

    private async Task<(string Path, string? Original)> WeaveAsync(
        SkillKit kit, AgentHomeResolution.ResolvedHome home, string content, List<GlobalDeployEntry> entries, CancellationToken ct)
    {
        var entryFile = home.EntryFile;
        string? originalText = null;
        if (File.Exists(entryFile))
        {
            originalText = await File.ReadAllTextAsync(entryFile, ct);
        }

        var (newText, changed, blockHash) = MarkerBlockWriter.Upsert(originalText ?? string.Empty, kit.Name, content);
        if (changed)
        {
            await File.WriteAllTextAsync(entryFile, newText, ContentHash.Utf8NoBom, ct);
        }

        var row = await _db.GlobalMarkerDeployments
            .FirstOrDefaultAsync(m => m.KitId == kit.Id && m.Platform == home.Platform, ct);
        if (row is not null)
        {
            row.Refresh(blockHash);
        }
        else
        {
            _db.GlobalMarkerDeployments.Add(GlobalMarkerDeployment.Create(kit.Id, home.Platform, entryFile, blockHash));
        }

        entries.Add(new GlobalDeployEntry(GlobalDeployEntryKind.MarkerBlock, home.Platform, entryFile, GlobalDeployOutcome.Written));
        return (entryFile, originalText);
    }

    private async Task<(string Path, string? Original)> UnweaveAsync(
        SkillKit kit, AgentHomeResolution.ResolvedHome home, List<GlobalDeployEntry> entries, CancellationToken ct)
    {
        var markerRow = await _db.GlobalMarkerDeployments
            .FirstOrDefaultAsync(m => m.KitId == kit.Id && m.Platform == home.Platform, ct);
        if (markerRow is null)
        {
            return (string.Empty, null);
        }

        var entryPath = PathGuard.ResolveAbsoluteUnderRoot(home.ConfigDir, markerRow.EntryFilePath);
        string? text = null;
        if (File.Exists(entryPath))
        {
            text = await File.ReadAllTextAsync(entryPath, ct);
            var (newText, removed) = MarkerBlockWriter.Cut(text, kit.Name);
            if (removed)
            {
                if (string.IsNullOrWhiteSpace(newText))
                {
                    File.Delete(entryPath);
                }
                else
                {
                    await File.WriteAllTextAsync(entryPath, newText, ContentHash.Utf8NoBom, ct);
                }

                entries.Add(new GlobalDeployEntry(GlobalDeployEntryKind.MarkerBlock, home.Platform, entryPath, GlobalDeployOutcome.Written));
            }
        }

        _db.GlobalMarkerDeployments.Remove(markerRow);
        return (entryPath, text);
    }

    private void PruneStaleRows(
        List<GlobalMaterializedFile> tracked, List<HomeFilePlan> plans, AgentHomeResolution.ResolvedHome home,
        List<string> warnings, List<GlobalDeployEntry> entries)
    {
        var planned = new HashSet<string>(plans.Select(p => p.AbsolutePath), PathGuard.PathComparer);
        var stale = tracked
            .Where(m => !planned.Contains(PathGuard.ResolveUnderRoot(home.SkillsDir, m.RelativePath)))
            .ToArray();

        foreach (var row in stale)
        {
            var absolute = PathGuard.ResolveUnderRoot(home.SkillsDir, row.RelativePath);
            if (File.Exists(absolute))
            {
                File.Delete(absolute);
                DirectoryPruner.PruneEmptyDirectories(absolute, home.SkillsDir);
                warnings.Add($"Removed stale tracked file: {absolute}");
            }

            _db.GlobalMaterializedFiles.Remove(row);
            entries.Add(new GlobalDeployEntry(GlobalDeployEntryKind.File, home.Platform, absolute, GlobalDeployOutcome.Written, "stale row removed"));
        }
    }

    private string? ExtractBlockHash(string entryText, Guid kitId, string entryFilePath)
    {
        var kit = _db.SkillKits
            .Where(k => k.Id == kitId)
            .Select(k => k.Name)
            .FirstOrDefault();
        if (kit is null)
        {
            _logger.LogWarning("Marker drift check skipped: kit {KitId} missing for {EntryFile}", kitId, entryFilePath);
            return null;
        }

        var (start, end) = MarkerBlockWriter.Markers(kit);
        var startIndex = entryText.IndexOf(start, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            return null;
        }

        var endIndex = entryText.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            return null;
        }

        return MarkerBlockWriter.ContentSha256(entryText[startIndex..(endIndex + end.Length)]);
    }

    private IReadOnlyDictionary<string, string> EnvSnapshot() =>
        _envOverride ?? Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(d => (string)d.Key, d => (string)(d.Value ?? string.Empty));

    private IEnumerable<AgentHomeResolution.ResolvedHome> ResolveInstalled() =>
        _agentHomes
            .Select(spec => AgentHomeResolution.Resolve(spec, _platforms, _homeRoot, EnvSnapshot()))
            .Where(r => r is not null)
            .Select(r => r!);

    private AgentHomeResolution.ResolvedHome ResolveAgent(string agent)
    {
        var spec = _agentHomes.FirstOrDefault(s =>
            string.Equals(s.AgentName, agent, StringComparison.OrdinalIgnoreCase))
            ?? throw MaterializationException.InvalidTarget(agent, $"unknown agent '{agent}'");

        return AgentHomeResolution.Resolve(spec, _platforms, _homeRoot, EnvSnapshot())
            ?? throw MaterializationException.InvalidTarget(agent, $"agent '{agent}' is not installed");
    }

    private async Task<SkillKit?> LoadKitAsync(Guid kitId, CancellationToken ct) =>
        await _db.SkillKits.FirstOrDefaultAsync(k => k.Id == kitId, ct);

    private static GlobalDeployReport Finish(
        Guid kitId, string agent, DateTime started, List<GlobalDeployEntry> entries, List<string> warnings) =>
        new(kitId, agent, started, DateTime.UtcNow, entries.ToArray(), warnings.ToArray());

    private async Task RecordRunAsync(GlobalDeployReport report, bool success, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(new
        {
            report.KitId,
            report.Agent,
            report.StartedAt,
            report.FinishedAt,
            report.Warnings,
            written = report.Entries.Where(e => e.Outcome == GlobalDeployOutcome.Written)
                .Select(e => new { e.Path, kind = e.Kind.ToString() }).ToArray(),
            skipped = report.Entries.Where(e => e.Outcome == GlobalDeployOutcome.Skipped)
                .Select(e => new { e.Path, reason = e.Reason }).ToArray(),
            refused = report.Entries.Where(e => e.Outcome == GlobalDeployOutcome.Refused)
                .Select(e => new { e.Path, reason = e.Reason }).ToArray(),
        });

        await ReplaceRunAsync(GlobalRunKind.Attempt, json, ct);
        if (success)
        {
            await ReplaceRunAsync(GlobalRunKind.Success, json, ct);
        }
    }

    private async Task ReplaceRunAsync(GlobalRunKind kind, string json, CancellationToken ct)
    {
        var existing = await _db.GlobalMaterializationRuns
            .Where(r => r.Kind == kind)
            .ToListAsync(ct);
        _db.GlobalMaterializationRuns.RemoveRange(existing);
        _db.GlobalMaterializationRuns.Add(GlobalMaterializationRun.Record(kind, json));
        await _db.SaveChangesAsync(ct);
    }

    private sealed record HomeFilePlan(string AbsolutePath, string RelativePath, string Content);
}
