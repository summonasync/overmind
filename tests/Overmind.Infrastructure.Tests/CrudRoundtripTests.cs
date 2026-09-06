using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Search;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public class CrudRoundtripTests
{
    [Fact]
    public async Task SaveAndReload_RoundtripsAllEntities()
    {
        using var db = new TempDatabase();
        Guid projectId;
        Guid noteId;
        Guid skillId;
        Guid kitId;

        using (var ctx = db.CreateContext())
        {
            var project = Project.Create("Overmind", "/disk/overmind", "context keeper");
            ctx.Projects.Add(project);

            var note = ContextEntry.Create(project.Id, EntryType.Decision, "Use SQLite", "SQLite is the only metadata store");
            var tag = Tag.Create("architecture");
            ctx.Tags.Add(tag);
            note.AddTag(tag);
            ctx.ContextEntries.Add(note);

            var skill = ContextEntry.CreateSkill(
                project.Id,
                "reviewer",
                "reviews code",
                [SkillFile.Create(AgentPlatform.Claude, "reviewer/SKILL.md", "# Reviewer")]);
            ctx.ContextEntries.Add(skill);

            var kit = SkillKit.Create("review-pack", 1, "review skills");
            kit.AddEntry(skill.Id);
            ctx.SkillKits.Add(kit);

            ctx.ProjectKits.Add(ProjectKit.Create(project.Id, kit.Id));

            await ctx.SaveChangesAsync();

            projectId = project.Id;
            noteId = note.Id;
            skillId = skill.Id;
            kitId = kit.Id;
        }

        using var verify = db.CreateContext();
        var loaded = await verify.ContextEntries
            .Include(e => e.SkillFiles)
            .Include(e => e.EntryTags)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        loaded.Count.ShouldBe(2);

        var loadedNote = loaded.Single(e => e.Id == noteId);
        loadedNote.Type.ShouldBe(EntryType.Decision);
        loadedNote.ProjectId.ShouldBe(projectId);
        loadedNote.Content.ShouldBe("SQLite is the only metadata store");
        loadedNote.EntryTags.Count.ShouldBe(1);

        var loadedSkill = loaded.Single(e => e.Id == skillId);
        loadedSkill.Type.ShouldBe(EntryType.Skill);
        loadedSkill.SkillFiles.Count.ShouldBe(1);
        loadedSkill.SkillFiles[0].RelativePath.ShouldBe("reviewer/SKILL.md");
        loadedSkill.SkillFiles[0].Platform.ShouldBe(AgentPlatform.Claude);

        var loadedKit = await verify.SkillKits.Include(k => k.SkillKitEntries).SingleAsync(k => k.Id == kitId);
        loadedKit.SkillKitEntries.Select(e => e.EntryId).ShouldBe([skillId]);

        var loadedLink = await verify.ProjectKits.SingleAsync();
        loadedLink.ProjectId.ShouldBe(projectId);
        loadedLink.KitId.ShouldBe(kitId);
        loadedLink.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateContent_AppendsVersionRowToDatabase()
    {
        using var db = new TempDatabase();
        Guid entryId;

        using (var ctx = db.CreateContext())
        {
            var project = Project.Create("p", "/p");
            ctx.Projects.Add(project);
            var entry = ContextEntry.Create(project.Id, EntryType.Note, "n", "first draft");
            ctx.ContextEntries.Add(entry);
            await ctx.SaveChangesAsync();
            entryId = entry.Id;
        }

        using (var ctx = db.CreateContext())
        {
            var entry = await ctx.ContextEntries.SingleAsync(e => e.Id == entryId);
            entry.UpdateContent("second draft");
            entry.UpdateContent("final draft");
            await ctx.SaveChangesAsync();
        }

        using var verify = db.CreateContext();
        var versions = await verify.EntryVersions
            .Where(v => v.EntryId == entryId)
            .OrderBy(v => v.Version)
            .ToListAsync();

        versions.Count.ShouldBe(2);
        versions[0].Version.ShouldBe(1);
        versions[0].Content.ShouldBe("first draft");
        versions[1].Version.ShouldBe(2);
        versions[1].Content.ShouldBe("second draft");

        var finalEntry = await verify.ContextEntries.SingleAsync(e => e.Id == entryId);
        finalEntry.Content.ShouldBe("final draft");
    }

    [Fact]
    public async Task UpdateContent_AcrossSeparateContexts_UsesPersistedVersionCounter()
    {
        using var db = new TempDatabase();
        Guid entryId;

        using (var ctx = db.CreateContext())
        {
            var project = Project.Create("p", "/p");
            ctx.Projects.Add(project);
            var entry = ContextEntry.Create(project.Id, EntryType.Note, "n", "draft one");
            ctx.ContextEntries.Add(entry);
            await ctx.SaveChangesAsync();
            entryId = entry.Id;
        }

        using (var ctx = db.CreateContext())
        {
            var entry = await ctx.ContextEntries.SingleAsync(e => e.Id == entryId);
            entry.UpdateContent("draft two");
            await ctx.SaveChangesAsync();
        }

        using (var ctx = db.CreateContext())
        {
            var entry = await ctx.ContextEntries.SingleAsync(e => e.Id == entryId);
            entry.UpdateContent("draft three");
            await ctx.SaveChangesAsync();
        }

        using var verify = db.CreateContext();
        var versions = await verify.EntryVersions
            .Where(v => v.EntryId == entryId)
            .OrderBy(v => v.Version)
            .ToListAsync();

        versions.Count.ShouldBe(2);
        versions.Select(v => v.Version).ShouldBe([1, 2]);
        versions[0].Content.ShouldBe("draft one");
        versions[1].Content.ShouldBe("draft two");

        var finalEntry = await verify.ContextEntries.SingleAsync(e => e.Id == entryId);
        finalEntry.Content.ShouldBe("draft three");
        finalEntry.CurrentVersion.ShouldBe(2);
    }

    [Fact]
    public async Task JournalMode_IsWal()
    {
        using var db = new TempDatabase();
        await using var ctx = db.CreateContext();
        await ctx.Database.OpenConnectionAsync();

        await using var command = ctx.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        var mode = await command.ExecuteScalarAsync();

        mode.ShouldBe("wal");
    }

    [Fact]
    public async Task Migration_CreatesFtsTableAndThreeTriggers()
    {
        using var db = new TempDatabase();
        await using var ctx = db.CreateContext();
        await ctx.Database.OpenConnectionAsync();

        await using var command = ctx.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            """
            SELECT name FROM sqlite_master
            WHERE (type = 'table' AND name = 'entry_fts')
               OR (type = 'trigger' AND name LIKE 'trg_entry_fts_%')
            """;

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        names.ShouldContain("entry_fts");
        names.ShouldContain("trg_entry_fts_insert");
        names.ShouldContain("trg_entry_fts_update");
        names.ShouldContain("trg_entry_fts_delete");
        names.Count.ShouldBe(4);
    }
}
