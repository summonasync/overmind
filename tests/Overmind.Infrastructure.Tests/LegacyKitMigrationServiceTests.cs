using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Kits;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class LegacyKitMigrationServiceTests : IDisposable
{
    private const string SplitManifest =
        """
        {"bundleVersion":BUNDLE,"kits":[{"name":"overmind-ceremony","mandatory":true},{"name":"dotnet-crew","mandatory":false},{"name":"rust-crew","mandatory":false},{"name":"js-crew","mandatory":false},{"name":"sociality","mandatory":false},{"name":"crew-extras","mandatory":false}],"legacy":["crew"]}
        """;

    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"overmind-legacy-{Guid.NewGuid():N}");
    private readonly string _bundle;

    public LegacyKitMigrationServiceTests()
    {
        _context = _db.CreateContext();
        _bundle = Path.Combine(_root, "bundled");
        Directory.CreateDirectory(_bundle);
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Run_CrewActiveProject_TakesEveryOptionalKitAndRetiresCrew()
    {
        WriteCrewBundle();
        await CreateSeed().RunAsync();
        var crewId = (await _context.SkillKits.SingleAsync(k => k.Name == "crew")).Id;
        var projectId = await AddProjectAsync("demo");
        await ActivateCrewAsync(projectId, crewId);
        var crewSkill = ProjectFile("demo", ".claude", "skills", "crew-skill", "SKILL.md");
        var crewBootstrap = ProjectFile("demo", ".claude", "skills", "overmind-bootstrap-crew", "SKILL.md");
        File.Exists(crewSkill).ShouldBeTrue();
        File.Exists(crewBootstrap).ShouldBeTrue();

        WriteSplitBundle(2);
        var deployer = Substitute.For<IGlobalKitDeployer>();
        var result = await CreateSeed(deployer).RunAsync();

        result.Outcome.ShouldBe(BundledSeedOutcome.Upgraded);
        result.KitsMarkedLegacy.ShouldBe(1);

        using var verify = _db.CreateContext();
        var kits = await verify.SkillKits.ToListAsync();
        kits.Count.ShouldBe(7);
        var crew = kits.Single(k => k.Name == "crew");
        crew.Legacy.ShouldBeTrue();
        crew.SourceKind.ShouldBe(KitSourceKind.Bundled);

        var links = await verify.ProjectKits.ToListAsync();
        links.Count(l => l.ProjectId == projectId && l.IsActive).ShouldBe(5);
        links.Single(l => l.KitId == crew.Id).IsActive.ShouldBeFalse();
        links.Any(l => l.KitId == kits.Single(k => k.Name == "overmind-ceremony").Id).ShouldBeFalse();
        (await verify.MaterializedFiles.ToListAsync()).Count(m => m.KitId == crew.Id).ShouldBe(0);

        File.Exists(crewSkill).ShouldBeFalse();
        File.Exists(crewBootstrap).ShouldBeFalse();
        File.Exists(ProjectFile("demo", ".claude", "skills", "dotnet-coder", "SKILL.md")).ShouldBeTrue();
        File.Exists(ProjectFile("demo", ".claude", "skills", "web-coder", "SKILL.md")).ShouldBeTrue();

        deployer.Received(1).DeployAsync(
            Arg.Is<SkillKit>(kit => kit.Name == "overmind-ceremony"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_MigrationAlreadyDone_RepairsNothingAndStacksNoActivations()
    {
        var projectId = await MigratedProjectAsync();

        var second = await CreateSeed().RunAsync();

        second.Outcome.ShouldBe(BundledSeedOutcome.UpToDate);
        second.KitsMarkedLegacy.ShouldBe(0);
        using var verify = _db.CreateContext();
        var links = await verify.ProjectKits.ToListAsync();
        links.Count(l => l.ProjectId == projectId && l.IsActive).ShouldBe(5);
        (await verify.SkillKits.ToListAsync()).Single(k => k.Name == "crew").Legacy.ShouldBeTrue();
    }

    [Fact]
    public async Task Run_CrewContentEditedLocally_EditSurvivesRetirement()
    {
        WriteCrewBundle();
        await CreateSeed().RunAsync();
        var crew = await _context.SkillKits
            .Include(k => k.SkillKitEntries)
            .ThenInclude(l => l.Entry)
            .SingleAsync(k => k.Name == "crew");
        var entry = crew.SkillKitEntries.Single().Entry;
        entry.UpdateContent("---\nname: crew-skill\ndescription: Mine now.\n---\n\nMine.\n");
        await _context.SaveChangesAsync();
        var forks = new BundledKitForkService(_context, NullLogger<BundledKitForkService>.Instance);
        await forks.ForkExclusiveBundledOwnerAsync(entry.Id, CancellationToken.None);
        var projectId = await AddProjectAsync("demo");
        await ActivateCrewAsync(projectId, crew.Id);

        WriteSplitBundle(2);
        var result = await CreateSeed().RunAsync();

        result.KitsMarkedLegacy.ShouldBe(1);
        using var verify = _db.CreateContext();
        var migrated = await verify.SkillKits
            .Include(k => k.SkillKitEntries)
            .ThenInclude(l => l.Entry)
            .ToListAsync();
        var retired = migrated.Single(k => k.Name == "crew");
        retired.Legacy.ShouldBeTrue();
        retired.SourceKind.ShouldBe(KitSourceKind.Manual);
        retired.SkillKitEntries.ShouldHaveSingleItem().Entry
            .Content.ShouldBe("---\nname: crew-skill\ndescription: Mine now.\n---\n\nMine.\n");
        migrated.ShouldAllBe(k => k.Legacy || !string.Equals(k.Name, "crew", StringComparison.Ordinal));
        var links = await verify.ProjectKits.ToListAsync();
        links.Count(l => l.ProjectId == projectId && l.IsActive).ShouldBe(5);
    }

    [Fact]
    public async Task Run_KitLandingRefused_CrewStaysUnmarkedUntilTheProjectConverges()
    {
        WriteCrewBundle();
        await CreateSeed().RunAsync();
        var crewId = (await _context.SkillKits.SingleAsync(k => k.Name == "crew")).Id;
        var projectId = await AddProjectAsync("demo");
        await ActivateCrewAsync(projectId, crewId);

        WriteSplitBundle(2);
        Directory.CreateDirectory(
            Path.GetDirectoryName(ProjectFile("demo", ".claude", "skills", "rust-coder", "SKILL.md"))!);
        File.WriteAllText(ProjectFile("demo", ".claude", "skills", "rust-coder", "SKILL.md"), "mine\n");

        var result = await CreateSeed().RunAsync();

        result.KitsMarkedLegacy.ShouldBe(0);
        using (var verify = _db.CreateContext())
        {
            var links = await verify.ProjectKits.ToListAsync();
            var crew = (await verify.SkillKits.ToListAsync()).Single(k => k.Name == "crew");
            crew.Legacy.ShouldBeFalse();
            links.Single(l => l.KitId == crew.Id && l.ProjectId == projectId).IsActive.ShouldBeTrue();
            links.Count(l => l.ProjectId == projectId && l.IsActive && l.KitId != crew.Id).ShouldBe(4);
        }

        File.Delete(ProjectFile("demo", ".claude", "skills", "rust-coder", "SKILL.md"));
        var repaired = await CreateSeed().RunAsync();

        repaired.Outcome.ShouldBe(BundledSeedOutcome.UpToDate);
        repaired.KitsMarkedLegacy.ShouldBe(1);
        using var after = _db.CreateContext();
        var kit = (await after.SkillKits.ToListAsync()).Single(k => k.Name == "crew");
        kit.Legacy.ShouldBeTrue();
        var afterLinks = await after.ProjectKits.ToListAsync();
        afterLinks.Single(l => l.KitId == kit.Id).IsActive.ShouldBeFalse();
        afterLinks.Count(l => l.ProjectId == projectId && l.IsActive).ShouldBe(5);
    }

    [Fact]
    public async Task Run_ManyProjectsEveryOneLosesCrewAndTakesOptionals()
    {
        WriteCrewBundle();
        await CreateSeed().RunAsync();
        var crewId = (await _context.SkillKits.SingleAsync(k => k.Name == "crew")).Id;
        var first = await AddProjectAsync("one");
        var second = await AddProjectAsync("two");
        await ActivateCrewAsync(first, crewId);
        await ActivateCrewAsync(second, crewId);
        await AddProjectAsync("untouched");

        WriteSplitBundle(2);
        await CreateSeed().RunAsync();

        using var verify = _db.CreateContext();
        var links = await verify.ProjectKits.ToListAsync();
        links.Count(l => l.IsActive).ShouldBe(10);
        links.Any(l => l.KitId == crewId && l.IsActive).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_NoGlobalDoorWired_MigrationStillCompletesAndCrewRetires()
    {
        var projectId = await MigratedProjectAsync();

        using var verify = _db.CreateContext();
        var links = await verify.ProjectKits.ToListAsync();
        links.Count(l => l.ProjectId == projectId && l.IsActive).ShouldBe(5);
        (await verify.SkillKits.ToListAsync()).Single(k => k.Name == "crew").Legacy.ShouldBeTrue();
    }

    private async Task<Guid> MigratedProjectAsync()
    {
        WriteCrewBundle();
        await CreateSeed().RunAsync();
        var crewId = (await _context.SkillKits.SingleAsync(k => k.Name == "crew")).Id;
        var projectId = await AddProjectAsync("demo");
        await ActivateCrewAsync(projectId, crewId);
        WriteSplitBundle(2);
        await CreateSeed(Substitute.For<IGlobalKitDeployer>()).RunAsync();
        return projectId;
    }

    private async Task ActivateCrewAsync(Guid projectId, Guid crewId)
    {
        var materializer = new SkillMaterializer(_context, PlatformDirectoryMap.Load());
        await new KitActivationService(_context, materializer)
            .ActivateAsync(projectId, crewId, false, CancellationToken.None);
    }

    private async Task<Guid> AddProjectAsync(string name)
    {
        var project = Project.Create(name, ProjectDir(name));
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        return project.Id;
    }

    private string ProjectDir(string name) => Path.Combine(_root, "projects", name);

    private string ProjectFile(string project, params string[] parts) =>
        Path.Combine(ProjectDir(project), Path.Combine(parts));

    private void WriteCrewBundle()
    {
        WriteSkill("crew", "crew-skill");
        File.WriteAllText(
            Path.Combine(_bundle, "manifest.json"),
            """
            {"bundleVersion":1,"kits":["crew"]}
            """);
    }

    private void WriteSplitBundle(int version)
    {
        WriteSkill("overmind-ceremony", "overmind-ceremony");
        WriteSkill("dotnet-crew", "dotnet-coder");
        WriteSkill("rust-crew", "rust-coder");
        WriteSkill("js-crew", "web-coder");
        WriteSkill("crew-extras", "go-coder");
        var sociality = Path.Combine(_bundle, "sociality");
        Directory.CreateDirectory(sociality);
        File.WriteAllText(Path.Combine(sociality, "SOCIAL_DICTIONARY.md"), "# Tone rules.\n");

        File.WriteAllText(
            Path.Combine(_bundle, "manifest.json"),
            SplitManifest.Replace("BUNDLE", version.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    private void WriteSkill(string kitName, string skillName)
    {
        var dir = Path.Combine(_bundle, kitName, "skills", skillName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, "SKILL.md"),
            $"---\nname: {skillName}\ndescription: Does a thing.\n---\n\nBody.\n");
    }

    private BundledSeedService CreateSeed(IGlobalKitDeployer? deployer = null)
    {
        var platforms = PlatformDirectoryMap.Load();
        var materializer = new SkillMaterializer(_context, platforms);
        return new BundledSeedService(
            _context,
            new KitImporter(_context, new KitScanner(_context, platforms), platforms),
            new AppSettingsService(_context),
            NullLogger<BundledSeedService>.Instance,
            _bundle,
            new LegacyKitMigrationService(
                _context,
                new KitActivationService(_context, materializer),
                materializer,
                NullLogger<LegacyKitMigrationService>.Instance),
            deployer);
    }
}
