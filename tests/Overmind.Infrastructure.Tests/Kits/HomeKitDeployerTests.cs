using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Kits;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class HomeKitDeployerTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly string _homeRoot;
    private readonly string _sources;
    private readonly PlatformDirectoryMap _platforms = PlatformDirectoryMap.Load();

    public HomeKitDeployerTests()
    {
        var root = Path.Combine(Path.GetTempPath(), $"overmind-homedeploy-{Guid.NewGuid():N}");
        _homeRoot = Path.Combine(root, "home");
        _sources = Path.Combine(root, "sources");
        Directory.CreateDirectory(_homeRoot);
        Directory.CreateDirectory(_sources);
        Directory.CreateDirectory(Path.Combine(_homeRoot, "claude-cfg"));
        Directory.CreateDirectory(Path.Combine(_homeRoot, "qwen-cfg"));
        _context = _db.CreateContext();
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
        var root = Path.GetDirectoryName(_homeRoot);
        if (root is not null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private HomeKitDeployer CreateDeployer(params AgentHomeSpec[] specs) =>
        new(_context, _platforms, specs, NullLogger<HomeKitDeployer>.Instance, _homeRoot);

    private AgentHomeSpec ClaudeSpec { get; } = new("claude", AgentPlatform.Claude, ["~/claude-cfg"], "CLAUDE.md");

    private AgentHomeSpec QwenSpec { get; } = new("qwen", AgentPlatform.Qwen, ["~/qwen-cfg"], "QWEN.md");

    private SkillKit SeedKit(string name, string sourceDir, bool mandatory = false)
    {
        var kit = SkillKit.Create(name, 1, $"{name} kit");
        kit.MarkBundled(sourceDir);
        if (mandatory)
        {
            kit.SetMandatory(true);
        }

        _context.SkillKits.Add(kit);
        _context.SaveChanges();
        return kit;
    }

    private string WriteProseOnlySource(string kitName)
    {
        var dir = Path.Combine(_sources, kitName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SOCIAL_DICTIONARY.md"), "# Tone rules.\nBe decent.\n");
        return dir;
    }

    private string WriteSkillSource(string kitName, string skillName)
    {
        var dir = Path.Combine(_sources, kitName, skillName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), $"---\nname: {skillName}\ndescription: Does a thing.\n---\n\nBody.\n");
        return Path.Combine(_sources, kitName);
    }

    private string ClaudeHomeSkills => Path.Combine(_homeRoot, ".claude", "skills");

    [Fact]
    public async Task Deploy_writes_skill_files_and_tracks_them()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kit = SeedKit("dotnet-crew", source);
        Directory.CreateDirectory(ClaudeHomeSkills);
        var deployer = CreateDeployer(ClaudeSpec);

        var report = await deployer.DeployAsync(kit.Id, "claude", force: false);

        report.Success.ShouldBeTrue();
        var skillFile = Path.Combine(ClaudeHomeSkills, "dotnet-coder", "SKILL.md");
        File.Exists(skillFile).ShouldBeTrue();
        var row = await _context.GlobalMaterializedFiles.SingleAsync(f => f.KitId == kit.Id);
        row.RelativePath.ShouldBe("dotnet-coder/SKILL.md");
        row.Platform.ShouldBe(AgentPlatform.Claude);
    }

    [Fact]
    public async Task Deploy_skips_when_home_skills_dir_is_missing_and_never_creates_it()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kit = SeedKit("dotnet-crew", source);
        var deployer = CreateDeployer(ClaudeSpec);

        var report = await deployer.DeployAsync(kit.Id, "claude", force: false);

        Directory.Exists(ClaudeHomeSkills).ShouldBeFalse();
        report.Entries.ShouldContain(e => e.Outcome == GlobalDeployOutcome.Skipped && e.Reason == "home skills dir missing");
    }

    [Fact]
    public async Task Deploy_refuses_an_untracked_file_in_the_way_without_force()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kit = SeedKit("dotnet-crew", source);
        var neighbor = Path.Combine(ClaudeHomeSkills, "dotnet-coder", "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(neighbor)!);
        File.WriteAllText(neighbor, "third-party content");
        var deployer = CreateDeployer(ClaudeSpec);

        await Should.ThrowAsync<MaterializationException>(() => deployer.DeployAsync(kit.Id, "claude", force: false));
        File.ReadAllText(neighbor).ShouldBe("third-party content");

        var forced = await deployer.DeployAsync(kit.Id, "claude", force: true);
        forced.Warnings.ShouldContain(w => w.Contains("Overwrote untracked file"));
        File.Exists(Path.Combine(ClaudeHomeSkills, "dotnet-coder", "SKILL.md")).ShouldBeTrue();
    }

    [Fact]
    public async Task Deploy_refuses_locally_modified_tracked_files_and_redeploys_untouched_ones()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kit = SeedKit("dotnet-crew", source);
        Directory.CreateDirectory(ClaudeHomeSkills);
        var deployer = CreateDeployer(ClaudeSpec);
        await deployer.DeployAsync(kit.Id, "claude", force: false);

        var skillFile = Path.Combine(ClaudeHomeSkills, "dotnet-coder", "SKILL.md");
        File.WriteAllText(skillFile, "---\nname: dotnet-coder\ndescription: Hand edited.\n---\n\nBody.\n");

        await Should.ThrowAsync<MaterializationException>(() => deployer.DeployAsync(kit.Id, "claude", force: false));

        var forced = await deployer.DeployAsync(kit.Id, "claude", force: true);
        forced.Warnings.ShouldContain(w => w.Contains("Overwrote locally modified file"));
        File.ReadAllText(skillFile).ShouldContain("Does a thing.");
    }

    [Fact]
    public async Task Dematerialize_removes_only_tracked_files_and_survives_a_neighbor()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kit = SeedKit("dotnet-crew", source);
        Directory.CreateDirectory(ClaudeHomeSkills);
        var deployer = CreateDeployer(ClaudeSpec);
        await deployer.DeployAsync(kit.Id, "claude", force: false);
        var neighbor = Path.Combine(ClaudeHomeSkills, "third-party-skill", "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(neighbor)!);
        File.WriteAllText(neighbor, "keep me");

        await deployer.DematerializeAsync(kit.Id, "claude");

        File.Exists(Path.Combine(ClaudeHomeSkills, "dotnet-coder", "SKILL.md")).ShouldBeFalse();
        File.ReadAllText(neighbor).ShouldBe("keep me");
        (await _context.GlobalMaterializedFiles.ToListAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Dematerialize_refuses_the_mandatory_kit()
    {
        var source = WriteSkillSource("overmind-ceremony", "overmind-ceremony");
        var kit = SeedKit("overmind-ceremony", source, mandatory: true);
        var deployer = CreateDeployer(ClaudeSpec);

        await Should.ThrowAsync<KitActivationException>(() => deployer.DematerializeAsync(kit.Id, "claude"));
    }

    [Fact]
    public async Task Deploy_weaves_prose_only_kits_into_the_entry_file()
    {
        var source = WriteProseOnlySource("sociality");
        var kit = SeedKit("sociality", source);
        var configDir = Path.Combine(_homeRoot, "claude-cfg");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "CLAUDE.md"), "my own notes\n");
        var deployer = CreateDeployer(ClaudeSpec);

        await deployer.DeployAsync(kit.Id, "claude", force: false);

        var entry = File.ReadAllText(Path.Combine(configDir, "CLAUDE.md"));
        entry.ShouldStartWith("my own notes\n");
        entry.ShouldContain("===OVERMIND SOCIALITY KIT START===\n# Tone rules.\nBe decent.\n===OVERMIND SOCIALITY KIT END===");
        var markerRow = await _context.GlobalMarkerDeployments.SingleAsync(m => m.KitId == kit.Id);
        markerRow.EntryFilePath.ShouldBe(Path.Combine(configDir, "CLAUDE.md"));
    }

    [Fact]
    public async Task Redeploy_replaces_the_block_and_keeps_outside_content_intact()
    {
        var source = WriteProseOnlySource("sociality");
        var kit = SeedKit("sociality", source);
        var configDir = Path.Combine(_homeRoot, "claude-cfg");
        Directory.CreateDirectory(configDir);
        var deployer = CreateDeployer(ClaudeSpec);
        await deployer.DeployAsync(kit.Id, "claude", force: false);

        File.WriteAllText(Path.Combine(source, "SOCIAL_DICTIONARY.md"), "# Tone rules v2.\n");
        await deployer.DeployAsync(kit.Id, "claude", force: false);

        var entry = File.ReadAllText(Path.Combine(configDir, "CLAUDE.md"));
        entry.ShouldContain("Tone rules v2");
        entry.ShouldNotContain("Be decent.");
    }

    [Fact]
    public async Task Dematerialize_cuts_the_marker_block()
    {
        var source = WriteProseOnlySource("sociality");
        var kit = SeedKit("sociality", source);
        var configDir = Path.Combine(_homeRoot, "claude-cfg");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "CLAUDE.md"), "my own notes\n");
        var deployer = CreateDeployer(ClaudeSpec);
        await deployer.DeployAsync(kit.Id, "claude", force: false);

        await deployer.DematerializeAsync(kit.Id, "claude");

        var entry = File.ReadAllText(Path.Combine(configDir, "CLAUDE.md"));
        entry.ShouldBe("my own notes\n");
        (await _context.GlobalMarkerDeployments.ToListAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Kits_with_entries_never_weave_even_when_prose_exists()
    {
        var source = WriteProseOnlySource("overmind-ceremony");
        var kit = SeedKit("overmind-ceremony", source, mandatory: true);
        var project = Project.Create("library", Path.Combine(Path.GetTempPath(), $"lib-{Guid.NewGuid():N}"));
        _context.Projects.Add(project);
        var entry = ContextEntry.Create(project.Id, EntryType.Gate, "gate", "content");
        _context.ContextEntries.Add(entry);
        kit.AddEntry(entry.Id);
        _context.SaveChanges();
        var configDir = Path.Combine(_homeRoot, "claude-cfg");
        Directory.CreateDirectory(configDir);
        var deployer = CreateDeployer(ClaudeSpec);

        var report = await deployer.DeployAsync(kit.Id, "claude", force: false);

        report.Success.ShouldBeTrue();
        report.Entries.ShouldContain(e =>
            e.Kind == GlobalDeployEntryKind.MarkerBlock
            && e.Outcome == GlobalDeployOutcome.Skipped
            && e.Reason!.Contains("prose-only"));
        File.Exists(Path.Combine(configDir, "CLAUDE.md")).ShouldBeFalse();
        (await _context.GlobalMarkerDeployments.ToListAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Drift_detects_missing_modified_and_marker_states()
    {
        var skillSource = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var skillKit = SeedKit("dotnet-crew", skillSource);
        var proseSource = WriteProseOnlySource("sociality");
        var proseKit = SeedKit("sociality", proseSource);
        var configDir = Path.Combine(_homeRoot, "claude-cfg");
        Directory.CreateDirectory(configDir);
        var deployer = CreateDeployer(ClaudeSpec);
        Directory.CreateDirectory(ClaudeHomeSkills);
        await deployer.DeployAsync(skillKit.Id, "claude", force: false);
        await deployer.DeployAsync(proseKit.Id, "claude", force: false);

        File.WriteAllText(Path.Combine(ClaudeHomeSkills, "dotnet-coder", "SKILL.md"), "edited");
        var entry = Path.Combine(configDir, "CLAUDE.md");
        File.WriteAllText(entry, "my own notes\n");

        var items = await deployer.DetectDriftAsync();

        items.ShouldContain(i => i.KitId == skillKit.Id && i.Kind == GlobalDriftKind.Modified);
        items.ShouldContain(i => i.KitId == proseKit.Id && i.Kind == GlobalDriftKind.MarkerMissing);
    }

    [Fact]
    public async Task Every_deploy_records_attempt_and_success_runs()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kit = SeedKit("dotnet-crew", source);
        Directory.CreateDirectory(ClaudeHomeSkills);
        var deployer = CreateDeployer(ClaudeSpec);

        await deployer.DeployAsync(kit.Id, "claude", force: false);

        var kinds = await _context.GlobalMaterializationRuns.Select(r => r.Kind).ToListAsync();
        kinds.ShouldContain(GlobalRunKind.Attempt);
        kinds.ShouldContain(GlobalRunKind.Success);
    }

    [Fact]
    public async Task Deploy_reports_an_unknown_agent_and_a_manual_kit()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kit = SeedKit("dotnet-crew", source);
        kit.ForkToManual();
        _context.SaveChanges();
        var deployer = CreateDeployer(ClaudeSpec);

        await Should.ThrowAsync<MaterializationException>(() => deployer.DeployAsync(kit.Id, "nonexistent-agent", force: false));
        await Should.ThrowAsync<MaterializationException>(() => deployer.DeployAsync(kit.Id, "claude", force: false));
    }

    [Fact]
    public void AgentHomeResolution_prefers_env_candidates_and_joins_the_skills_tail()
    {
        var envRoot = Path.Combine(Path.GetTempPath(), $"codexhome-{Guid.NewGuid():N}");
        Directory.CreateDirectory(envRoot);
        try
        {
            var spec = new AgentHomeSpec("codex", AgentPlatform.Codex, ["$CODEX_TEST_HOME", "~/missing-cfg"], "AGENTS.md");
            var resolved = AgentHomeResolution.Resolve(
                spec, _platforms, _homeRoot, new Dictionary<string, string> { ["CODEX_TEST_HOME"] = envRoot });

            resolved.ShouldNotBeNull();
            resolved.ConfigDir.ShouldBe(envRoot);
            resolved.SkillsDir.ShouldBe(Path.Combine(envRoot, "skills"));
            resolved.EntryFile.ShouldBe(Path.Combine(envRoot, "AGENTS.md"));
        }
        finally
        {
            Directory.Delete(envRoot, recursive: true);
        }
    }

    [Fact]
    public void AgentHomeResolution_skips_missing_candidates_and_returns_null_when_all_missing()
    {
        var spec = new AgentHomeSpec("claude", AgentPlatform.Claude, ["~/definitely-missing-cfg"], "CLAUDE.md");
        var resolved = AgentHomeResolution.Resolve(spec, _platforms, _homeRoot, new Dictionary<string, string>());

        resolved.ShouldBeNull();
    }

    [Fact]
    public async Task Crlf_source_deploys_without_false_local_edit_detection()
    {
        var dir = Path.Combine(_sources, "dotnet-crew", "dotnet-coder");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), "---\r\nname: dotnet-coder\r\ndescription: CRLF authored.\r\n---\r\n\r\nBody.\r\n");
        var kit = SeedKit("dotnet-crew", Path.Combine(_sources, "dotnet-crew"));
        Directory.CreateDirectory(ClaudeHomeSkills);
        var deployer = CreateDeployer(ClaudeSpec);

        await deployer.DeployAsync(kit.Id, "claude", force: false);
        var redploy = await deployer.DeployAsync(kit.Id, "claude", force: false);
        redploy.Success.ShouldBeTrue();
        redploy.Entries.ShouldNotContain(e => e.Outcome == GlobalDeployOutcome.Refused);

        var drift = await deployer.DetectDriftAsync();
        drift.ShouldNotContain(i => i.KitId == kit.Id);
    }

    [Fact]
    public async Task Deploying_to_one_agent_leaves_another_agents_home_untouched()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kit = SeedKit("dotnet-crew", source);
        Directory.CreateDirectory(ClaudeHomeSkills);
        Directory.CreateDirectory(Path.Combine(_homeRoot, "qwen-cfg"));
        var deployer = CreateDeployer(ClaudeSpec, QwenSpec);

        await deployer.DeployAsync(kit.Id, "claude", force: false);

        Directory.Exists(Path.Combine(_homeRoot, ".qwen", "skills", "dotnet-coder")).ShouldBeFalse();
        (await _context.GlobalMaterializedFiles.ToListAsync()).ShouldNotContain(f => f.Platform == AgentPlatform.Qwen);
    }

    [Fact]
    public async Task Seed_contract_deploys_the_mandatory_kit_to_every_detected_agent()
    {
        var source = WriteSkillSource("overmind-ceremony", "overmind-ceremony");
        var kit = SeedKit("overmind-ceremony", source, mandatory: true);
        Directory.CreateDirectory(ClaudeHomeSkills);
        Directory.CreateDirectory(Path.Combine(_homeRoot, ".qwen", "skills"));
        IGlobalKitDeployer door = CreateDeployer(ClaudeSpec, QwenSpec);

        await door.DeployAsync(kit, CancellationToken.None);

        File.Exists(Path.Combine(ClaudeHomeSkills, "overmind-ceremony", "SKILL.md")).ShouldBeTrue();
        File.Exists(Path.Combine(_homeRoot, ".qwen", "skills", "overmind-ceremony", "SKILL.md")).ShouldBeTrue();
        var platforms = await _context.GlobalMaterializedFiles.Select(f => f.Platform).ToListAsync();
        platforms.ShouldContain(AgentPlatform.Claude);
        platforms.ShouldContain(AgentPlatform.Qwen);
    }

    [Fact]
    public async Task Drift_reports_rows_from_platforms_no_longer_installed()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kit = SeedKit("dotnet-crew", source);
        Directory.CreateDirectory(ClaudeHomeSkills);
        var deployer = CreateDeployer(ClaudeSpec);
        await deployer.DeployAsync(kit.Id, "claude", force: false);

        var qwenRow = GlobalMaterializedFile.Create(kit.Id, AgentPlatform.Qwen, "dotnet-coder/SKILL.md", "deadhash");
        _context.GlobalMaterializedFiles.Add(qwenRow);
        await _context.SaveChangesAsync();

        var drift = await deployer.DetectDriftAsync();

        drift.ShouldContain(i => i.Platform == AgentPlatform.Qwen && i.Kind == GlobalDriftKind.Missing);
    }
}
