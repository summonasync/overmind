using System.Text.Json;
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

public sealed class BundledSeedServiceTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly string _bundle = Path.Combine(Path.GetTempPath(), $"overmind-bundled-{Guid.NewGuid():N}");

    public BundledSeedServiceTests()
    {
        _context = _db.CreateContext();
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
        if (Directory.Exists(_bundle))
        {
            Directory.Delete(_bundle, recursive: true);
        }
    }

    private BundledSeedService CreateService(IGlobalKitDeployer? globalDeployer = null) =>
        CreateService(_bundle, globalDeployer);

    private BundledSeedService CreateService(string bundleDirectory, IGlobalKitDeployer? globalDeployer = null)
    {
        var platformMap = PlatformDirectoryMap.Load();
        var materializer = new SkillMaterializer(_context, platformMap);
        return new BundledSeedService(
            _context,
            new KitImporter(_context, new KitScanner(_context, platformMap), platformMap),
            new AppSettingsService(_context),
            NullLogger<BundledSeedService>.Instance,
            bundleDirectory,
            new LegacyKitMigrationService(
                _context,
                new KitActivationService(_context, materializer),
                materializer,
                NullLogger<LegacyKitMigrationService>.Instance),
            globalDeployer);
    }

    private static string SkillYaml(string name, string description = "Does a thing.") =>
        $"---\nname: {name}\ndescription: {description}\n---\n\nBody.\n";

    private void WriteKitFolder(string kitName, string skillName, string? content = null)
    {
        var dir = Path.Combine(_bundle, kitName, skillName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), content ?? SkillYaml(skillName));
    }

    private void WriteProseOnlyKitFolder(string kitName)
    {
        var dir = Path.Combine(_bundle, kitName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SOCIAL_DICTIONARY.md"), "# Tone rules.\n");
    }

    private void WriteManifest(int version, params string[] kits)
    {
        Directory.CreateDirectory(_bundle);
        File.WriteAllText(
            Path.Combine(_bundle, "manifest.json"),
            JsonSerializer.Serialize(new { bundleVersion = version, kits }));
    }

    private void WriteManifestFile(string content)
    {
        Directory.CreateDirectory(_bundle);
        File.WriteAllText(Path.Combine(_bundle, "manifest.json"), content);
    }

    private void WriteObjectManifest(int version, (string Name, bool Mandatory)[] kits, string[]? legacy = null) =>
        WriteManifestFile(JsonSerializer.Serialize(new
        {
            bundleVersion = version,
            kits = kits.Select(kit => new { name = kit.Name, mandatory = kit.Mandatory }).ToArray(),
            legacy = legacy ?? Array.Empty<string>()
        }));

    private async Task<string> ReadVersionAsync()
    {
        using var verify = _db.CreateContext();
        return await verify.AppSettings
            .Where(s => s.Key == BundledSeedService.VersionSettingKey)
            .Select(s => s.Value)
            .SingleAsync();
    }

    [Fact]
    public async Task Run_EmptyLibrary_SeedsAllKitsAsBundledAndStampsVersion()
    {
        WriteKitFolder("kit-a", "alpha");
        WriteKitFolder("kit-b", "beta");
        WriteManifest(3, "kit-a", "kit-b");

        var result = await CreateService().RunAsync();

        result.Outcome.ShouldBe(BundledSeedOutcome.Seeded);
        result.BundleVersion.ShouldBe(3);
        result.KitsImported.ShouldBe(2);
        result.KitsFailed.ShouldBe(0);
        (await ReadVersionAsync()).ShouldBe("3");

        using var verify = _db.CreateContext();
        var kits = await verify.SkillKits.Include(k => k.SkillKitEntries).ToListAsync();
        kits.Count.ShouldBe(2);
        kits.ShouldAllBe(k => k.SourceKind == KitSourceKind.Bundled);
        kits.ShouldAllBe(k => k.ImportedAtUtc == null);
        kits.ShouldAllBe(k => k.SourcePath == Path.GetFullPath(Path.Combine(_bundle, k.Name)));
        kits.ShouldAllBe(k => k.SkillKitEntries.Count == 1);
        kits.ShouldAllBe(k => k.SkillKitEntries.All(l => l.ImportedContentHash != null));
    }

    [Fact]
    public async Task Run_NonEmptyLibrary_SkipsSeedingWithoutStamping()
    {
        _context.SkillKits.Add(SkillKit.Create("mine", 1, string.Empty));
        await _context.SaveChangesAsync();
        WriteKitFolder("kit-a", "alpha");
        WriteManifest(1, "kit-a");

        var result = await CreateService().RunAsync();

        result.Outcome.ShouldBe(BundledSeedOutcome.LibraryInUse);
        using var verify = _db.CreateContext();
        (await verify.SkillKits.CountAsync()).ShouldBe(1);
        (await verify.SkillKits.SingleAsync()).Name.ShouldBe("mine");
        (await verify.AppSettings.AnyAsync(s => s.Key == BundledSeedService.VersionSettingKey)).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_AfterUserDeletedAllKits_DoesNotReseed()
    {
        WriteKitFolder("kit-a", "alpha");
        WriteManifest(1, "kit-a");
        await CreateService().RunAsync();

        await _context.Database.ExecuteSqlAsync($"DELETE FROM \"SkillKits\"");

        var result = await CreateService().RunAsync();

        result.Outcome.ShouldBe(BundledSeedOutcome.UpToDate);
        using var verify = _db.CreateContext();
        (await verify.SkillKits.CountAsync()).ShouldBe(0);
        (await ReadVersionAsync()).ShouldBe("1");
    }

    [Fact]
    public async Task Run_NewerManifest_AddsNewKit()
    {
        WriteKitFolder("kit-a", "alpha");
        WriteManifest(1, "kit-a");
        await CreateService().RunAsync();

        WriteKitFolder("kit-b", "beta");
        WriteManifest(2, "kit-a", "kit-b");

        var result = await CreateService().RunAsync();

        result.Outcome.ShouldBe(BundledSeedOutcome.Upgraded);
        result.KitsImported.ShouldBe(1);
        result.KitsUpdated.ShouldBe(1);
        (await ReadVersionAsync()).ShouldBe("2");

        using var verify = _db.CreateContext();
        var names = await verify.SkillKits.Select(k => k.Name).OrderBy(n => n).ToListAsync();
        names.ShouldBe(["kit-a", "kit-b"]);
        (await verify.SkillKits.SingleAsync(k => k.Name == "kit-b")).SourceKind
            .ShouldBe(KitSourceKind.Bundled);
    }

    [Fact]
    public async Task Run_NewerManifest_UpdatesUntouchedBundledKitViaDiff()
    {
        WriteKitFolder("kit-a", "alpha");
        WriteManifest(1, "kit-a");
        await CreateService().RunAsync();

        var updated = SkillYaml("alpha", "Upstream rewrite.");
        WriteKitFolder("kit-a", "alpha", updated);
        WriteManifest(2, "kit-a");

        var result = await CreateService().RunAsync();

        result.Outcome.ShouldBe(BundledSeedOutcome.Upgraded);
        result.KitsUpdated.ShouldBe(1);
        result.KitsImported.ShouldBe(0);

        using var verify = _db.CreateContext();
        var kit = await verify.SkillKits
            .Include(k => k.SkillKitEntries)
            .ThenInclude(l => l.Entry)
            .SingleAsync(k => k.Name == "kit-a");
        kit.SourceKind.ShouldBe(KitSourceKind.Bundled);
        kit.ImportedAtUtc.ShouldBeNull();
        kit.SourcePath.ShouldBe(Path.GetFullPath(Path.Combine(_bundle, "kit-a")));
        kit.SkillKitEntries.ShouldHaveSingleItem().Entry.Content.ShouldBe(updated);
        kit.SkillKitEntries.ShouldHaveSingleItem().ImportedContentHash
            .ShouldBe(ContentHash.Sha256(updated));
    }

    [Fact]
    public async Task Run_NewerManifest_KeepsLocalEditsThroughDiff()
    {
        WriteKitFolder("kit-a", "alpha");
        WriteManifest(1, "kit-a");
        await CreateService().RunAsync();

        var kit = await _context.SkillKits
            .Include(k => k.SkillKitEntries)
            .ThenInclude(l => l.Entry)
            .SingleAsync(k => k.Name == "kit-a");
        kit.SkillKitEntries.ShouldHaveSingleItem().Entry.UpdateContent("local rewrite\n");
        await _context.SaveChangesAsync();

        WriteKitFolder("kit-a", "alpha", SkillYaml("alpha", "Upstream rewrite."));
        WriteManifest(2, "kit-a");

        var result = await CreateService().RunAsync();

        result.KitsUpdated.ShouldBe(1);

        using var verify = _db.CreateContext();
        var reloaded = await verify.SkillKits
            .Include(k => k.SkillKitEntries)
            .ThenInclude(l => l.Entry)
            .SingleAsync(k => k.Name == "kit-a");
        reloaded.SourceKind.ShouldBe(KitSourceKind.Bundled);
        reloaded.SkillKitEntries.ShouldHaveSingleItem().Entry.Content.ShouldBe("local rewrite\n");
    }

    [Fact]
    public async Task Run_NewerManifest_LeavesManualKitWithCollidingNameUntouched()
    {
        WriteKitFolder("kit-a", "alpha");
        WriteManifest(1, "kit-a");
        await CreateService().RunAsync();

        await _context.Database.ExecuteSqlAsync($"DELETE FROM \"SkillKits\"");
        var manual = SkillKit.Create("kit-a", 1, "user-made");
        _context.SkillKits.Add(manual);
        await _context.SaveChangesAsync();

        WriteKitFolder("kit-a", "alpha", SkillYaml("alpha", "Upstream rewrite."));
        WriteManifest(2, "kit-a");

        var result = await CreateService().RunAsync();

        result.Outcome.ShouldBe(BundledSeedOutcome.Upgraded);
        result.KitsSkipped.ShouldBe(1);
        result.KitsImported.ShouldBe(0);

        using var verify = _db.CreateContext();
        var kit = await verify.SkillKits.SingleAsync();
        kit.Name.ShouldBe("kit-a");
        kit.SourceKind.ShouldBe(KitSourceKind.Manual);
        kit.SourcePath.ShouldBeNull();
        kit.SkillKitEntries.ShouldBeEmpty();
        (await ReadVersionAsync()).ShouldBe("2");
    }

    [Fact]
    public async Task Run_MissingBundle_IsNoOp()
    {
        var result = await CreateService().RunAsync();

        result.Outcome.ShouldBe(BundledSeedOutcome.NoBundle);
        using var verify = _db.CreateContext();
        (await verify.SkillKits.CountAsync()).ShouldBe(0);
        (await verify.AppSettings.AnyAsync()).ShouldBeFalse();
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{\"bundleVersion\":2}")]
    [InlineData("{\"bundleVersion\":0,\"kits\":[\"kit-a\"]}")]
    [InlineData("{\"bundleVersion\":2,\"kits\":[\"  \"]}")]
    [InlineData("{\"bundleVersion\":2,\"kits\":[{\"name\":\"  \",\"mandatory\":true}]}")]
    [InlineData("{\"bundleVersion\":2,\"kits\":[{\"mandatory\":true}]}")]
    [InlineData("{\"bundleVersion\":2,\"kits\":[{\"name\":\"kit-a\",\"mandatory\":\"yes\"}]}")]
    [InlineData("{\"bundleVersion\":2,\"kits\":[{\"name\":\"kit-a\",\"mandatory\":1}]}")]
    [InlineData("{\"bundleVersion\":2,\"kits\":[{\"name\":\"kit-a\",\"mandatory\":null}]}")]
    [InlineData("{\"bundleVersion\":2,\"kits\":[\"kit-a\"],\"legacy\":[\"  \"]}")]
    [InlineData("{\"bundleVersion\":2,\"kits\":[\"kit-a\"],\"legacy\":\"crew\"}")]
    [InlineData("{\"bundleVersion\":2,\"kits\":[\"kit-a\"],\"legacy\":[7]}")]
    public async Task Run_InvalidManifest_IsNoOp(string manifestContent)
    {
        WriteKitFolder("kit-a", "alpha");
        File.WriteAllText(Path.Combine(_bundle, "manifest.json"), manifestContent);

        var result = await CreateService().RunAsync();

        result.Outcome.ShouldBe(BundledSeedOutcome.InvalidManifest);
        using var verify = _db.CreateContext();
        (await verify.SkillKits.CountAsync()).ShouldBe(0);
        (await verify.AppSettings.AnyAsync()).ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Run_ManifestKitObjectForm_MarksMandatoryFromManifest(bool mandatory)
    {
        WriteKitFolder("kit-a", "alpha");
        WriteObjectManifest(2, [("kit-a", mandatory)]);

        var result = await CreateService().RunAsync();

        result.Outcome.ShouldBe(BundledSeedOutcome.Seeded);
        using var verify = _db.CreateContext();
        var kit = (await verify.SkillKits.ToListAsync()).Single();
        kit.SourceKind.ShouldBe(KitSourceKind.Bundled);
        kit.Mandatory.ShouldBe(mandatory);
        kit.Legacy.ShouldBeFalse();
    }

    [Fact]
    public async Task Run_ManualKitCollidingWithMandatoryName_KeepsMandatoryFalse()
    {
        WriteKitFolder("kit-a", "alpha");
        WriteManifest(1, "kit-a");
        await CreateService().RunAsync();

        await _context.Database.ExecuteSqlAsync($"DELETE FROM \"SkillKits\"");
        _context.SkillKits.Add(SkillKit.Create("kit-a", 1, "user-made"));
        await _context.SaveChangesAsync();

        WriteObjectManifest(2, [("kit-a", true)]);

        var result = await CreateService().RunAsync();

        result.KitsSkipped.ShouldBe(1);
        using var verify = _db.CreateContext();
        var kit = (await verify.SkillKits.ToListAsync()).Single();
        kit.SourceKind.ShouldBe(KitSourceKind.Manual);
        kit.Mandatory.ShouldBeFalse();
    }

    [Fact]
    public async Task Run_SplitBundle_FreshLibrarySeedsAllSixKitsIncludingTheProseOnlyOne()
    {
        WriteKitFolder("overmind-ceremony", "overmind-ceremony");
        WriteKitFolder("dotnet-crew", "dotnet-coder");
        WriteKitFolder("rust-crew", "rust-coder");
        WriteKitFolder("js-crew", "web-coder");
        WriteKitFolder("crew-extras", "go-coder");
        WriteProseOnlyKitFolder("sociality");
        WriteObjectManifest(4,
        [
            ("overmind-ceremony", true),
            ("dotnet-crew", false),
            ("rust-crew", false),
            ("js-crew", false),
            ("sociality", false),
            ("crew-extras", false)
        ]);

        var result = await CreateService().RunAsync();

        result.Outcome.ShouldBe(BundledSeedOutcome.Seeded);
        result.KitsImported.ShouldBe(6);
        result.KitsFailed.ShouldBe(0);
        (await ReadVersionAsync()).ShouldBe("4");

        using var verify = _db.CreateContext();
        var kits = await verify.SkillKits
            .Include(k => k.SkillKitEntries)
            .ToListAsync();
        kits.Count.ShouldBe(6);
        kits.ShouldAllBe(k => k.SourceKind == KitSourceKind.Bundled);
        kits.Single(k => k.Name == "overmind-ceremony").Mandatory.ShouldBeTrue();
        kits.Where(k => k.Name != "overmind-ceremony").ShouldAllBe(k => !k.Mandatory);
        kits.Single(k => k.Name == "sociality").SkillKitEntries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_KitNameSharingAnotherBootstrapSlug_ImportsOneAndReportsTheOtherFailed()
    {
        WriteKitFolder("kit-a", "alpha");
        WriteKitFolder("kit a", "beta");
        WriteObjectManifest(1, [("kit-a", false), ("kit a", false)]);

        var result = await CreateService().RunAsync();

        result.Outcome.ShouldBe(BundledSeedOutcome.Seeded);
        result.KitsImported.ShouldBe(1);
        result.KitsFailed.ShouldBe(1);
        using var verify = _db.CreateContext();
        (await verify.SkillKits.SingleAsync()).Name.ShouldBe("kit-a");
    }

    [Fact]
    public async Task Run_MandatoryKitWithoutGlobalDoor_StillSeedsAndLeavesTheLibraryMandatory()
    {
        WriteKitFolder("overmind-ceremony", "overmind-ceremony");
        WriteObjectManifest(1, [("overmind-ceremony", true)]);

        var result = await CreateService().RunAsync();

        result.Outcome.ShouldBe(BundledSeedOutcome.Seeded);
        result.KitsImported.ShouldBe(1);
        using var verify = _db.CreateContext();
        (await verify.SkillKits.SingleAsync()).Mandatory.ShouldBeTrue();
        (await verify.ProjectKits.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_MandatoryKitGlobalDoorWired_ReceivesTheMandatoryKitOnly()
    {
        WriteKitFolder("overmind-ceremony", "overmind-ceremony");
        WriteKitFolder("dotnet-crew", "dotnet-coder");
        WriteObjectManifest(1, [("overmind-ceremony", true), ("dotnet-crew", false)]);
        var deployer = Substitute.For<IGlobalKitDeployer>();

        await CreateService(deployer).RunAsync();

        await deployer.Received(1).DeployAsync(
            Arg.Is<SkillKit>(kit => kit.Name == "overmind-ceremony"), Arg.Any<CancellationToken>());
        await deployer.DidNotReceive().DeployAsync(
            Arg.Is<SkillKit>(kit => kit.Name == "dotnet-crew"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_GlobalDoorThrows_ImportedKitsRemainAndVersionIsStamped()
    {
        WriteKitFolder("overmind-ceremony", "overmind-ceremony");
        WriteObjectManifest(1, [("overmind-ceremony", true)]);
        var deployer = Substitute.For<IGlobalKitDeployer>();
        deployer.DeployAsync(Arg.Any<SkillKit>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("home door is down")));

        var result = await CreateService(deployer).RunAsync();

        result.Outcome.ShouldBe(BundledSeedOutcome.Seeded);
        result.KitsImported.ShouldBe(1);
        (await ReadVersionAsync()).ShouldBe("1");
    }

    [Fact]
    public async Task Run_FailingKitImport_ContinuesRemainingKits()
    {
        WriteKitFolder("kit-a", "alpha");
        WriteManifest(1, "kit-a", "kit-b");

        var result = await CreateService().RunAsync();

        result.Outcome.ShouldBe(BundledSeedOutcome.Seeded);
        result.KitsImported.ShouldBe(1);
        result.KitsFailed.ShouldBe(1);
        (await ReadVersionAsync()).ShouldBe("1");

        using var verify = _db.CreateContext();
        var kit = await verify.SkillKits.SingleAsync();
        kit.Name.ShouldBe("kit-a");
        kit.SourceKind.ShouldBe(KitSourceKind.Bundled);
    }

    [Fact]
    public async Task Run_ReloadedBundleLocation_RepointsBundledSourceBeforeSync()
    {
        WriteKitFolder("kit-a", "alpha");
        WriteManifest(1, "kit-a");
        await CreateService().RunAsync();

        var relocated = Path.Combine(Path.GetTempPath(), $"overmind-bundled-{Guid.NewGuid():N}");
        var updated = SkillYaml("alpha", "Relocated rewrite.");
        try
        {
            Directory.CreateDirectory(Path.Combine(relocated, "kit-a", "alpha"));
            await File.WriteAllTextAsync(
                Path.Combine(relocated, "kit-a", "alpha", "SKILL.md"), updated);
            File.WriteAllText(
                Path.Combine(relocated, "manifest.json"),
                JsonSerializer.Serialize(new { bundleVersion = 2, kits = new[] { "kit-a" } }));

            var service = CreateService(relocated);
            var result = await service.RunAsync();

            result.Outcome.ShouldBe(BundledSeedOutcome.Upgraded);
            result.KitsUpdated.ShouldBe(1);

            using var verify = _db.CreateContext();
            var kit = await verify.SkillKits
                .Include(k => k.SkillKitEntries)
                .ThenInclude(l => l.Entry)
                .SingleAsync(k => k.Name == "kit-a");
            kit.SourcePath.ShouldBe(Path.GetFullPath(Path.Combine(relocated, "kit-a")));
            kit.SkillKitEntries.ShouldHaveSingleItem().Entry.Content.ShouldBe(updated);
        }
        finally
        {
            if (Directory.Exists(relocated))
            {
                Directory.Delete(relocated, recursive: true);
            }
        }
    }
}
