using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class KitOriginPersistenceTests : IDisposable
{
    private readonly TempDatabase _db = new();

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task Roundtrip_OriginFieldsAndNullEntryHashSurviveReload()
    {
        Guid kitId;
        DateTime stamp;
        using (var ctx = _db.CreateContext())
        {
            var project = Project.Create("library", "/tmp/library");
            var kit = SkillKit.Create("origin-pack", 1, "d");
            kit.MarkImported("  /kits/origin-pack  ");
            var entry = ContextEntry.Create(project.Id, EntryType.Gate, "Gate", "Content.");
            kit.AddEntry(entry.Id);

            ctx.Projects.Add(project);
            ctx.SkillKits.Add(kit);
            ctx.ContextEntries.Add(entry);
            await ctx.SaveChangesAsync();

            kitId = kit.Id;
            stamp = kit.ImportedAtUtc!.Value;
        }

        using var verify = _db.CreateContext();
        var reloaded = await verify.SkillKits
            .Include(k => k.SkillKitEntries)
            .FirstAsync(k => k.Id == kitId);

        reloaded.SourceKind.ShouldBe(KitSourceKind.Imported);
        reloaded.SourcePath.ShouldBe("/kits/origin-pack");
        reloaded.ImportedAtUtc.ShouldNotBeNull();
        reloaded.ImportedAtUtc!.Value.Ticks.ShouldBe(stamp.Ticks);
        reloaded.SkillKitEntries.ShouldHaveSingleItem().ImportedContentHash.ShouldBeNull();
    }

    [Fact]
    public async Task SourceKind_PersistsAsInteger()
    {
        using (var ctx = _db.CreateContext())
        {
            var kit = SkillKit.Create("integer-pack", 1, "d");
            kit.MarkImported("/kits/integer-pack");
            ctx.SkillKits.Add(kit);
            await ctx.SaveChangesAsync();
        }

        using var verify = _db.CreateContext();
        var values = await verify.Database
            .SqlQuery<int>($"SELECT \"SourceKind\" AS \"Value\" FROM \"SkillKits\"")
            .ToListAsync();
        values.ShouldBe([(int)KitSourceKind.Imported]);

        var storageType = await verify.Database
            .SqlQuery<string>($"SELECT typeof(\"SourceKind\") AS \"Value\" FROM \"SkillKits\"")
            .SingleAsync();
        storageType.ShouldBe("integer");
    }
}
