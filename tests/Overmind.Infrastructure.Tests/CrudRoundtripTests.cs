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

    [Fact]
    public async Task Planning_FeatureArtifactsVersions_Roundtrip()
    {
        using var db = new TempDatabase();
        Guid projectId, featureId;

        using (var ctx = db.CreateContext())
        {
            var project = Project.Create("planning-proj", "/p/planning");
            ctx.Projects.Add(project);
            await ctx.SaveChangesAsync();
            projectId = project.Id;

            var feature = PlanningFeature.Create(projectId, "001-auth", "Auth flow", "spec body");
            ctx.PlanningFeatures.Add(feature);
            await ctx.SaveChangesAsync();
            featureId = feature.Id;

            var spec = PlanningArtifact.Create(featureId, PlanningArtifactKind.Spec, "spec content");
            spec.Validate();
            ctx.PlanningArtifacts.Add(spec);

            var task = PlanningArtifact.Create(featureId, PlanningArtifactKind.Task, "task content", order: 1);
            ctx.PlanningArtifacts.Add(task);
            await ctx.SaveChangesAsync();

            spec.UpdateBody("updated spec", "refined after review");
            await ctx.SaveChangesAsync();
        }

        using var verify = db.CreateContext();
        var loadedFeature = await verify.PlanningFeatures
            .Include(f => f.Artifacts)
            .ThenInclude(a => a.Versions)
            .SingleAsync(f => f.Id == featureId);

        loadedFeature.Slug.ShouldBe("001-auth");
        loadedFeature.ProjectId.ShouldBe(projectId);
        loadedFeature.Artifacts.Count.ShouldBe(2);

        var loadedSpec = loadedFeature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Spec);
        loadedSpec.Versions.Count.ShouldBe(1);
        loadedSpec.Versions[0].Body.ShouldBe("spec content");
        loadedSpec.Version.ShouldBe(2);
        loadedSpec.Status.ShouldBe(PlanningArtifactStatus.Draft);
        loadedSpec.Body.ShouldContain("refined after review");

        var loadedTask = loadedFeature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task);
        loadedTask.Status.ShouldBe(PlanningArtifactStatus.Todo);
        loadedTask.Order.ShouldBe(1);
    }

    [Fact]
    public async Task Planning_CascadeDelete_FromProject()
    {
        using var db = new TempDatabase();
        Guid projectId;

        using (var ctx = db.CreateContext())
        {
            var project = Project.Create("cascade-proj", "/p/cascade");
            ctx.Projects.Add(project);
            await ctx.SaveChangesAsync();
            projectId = project.Id;

            var feature = PlanningFeature.Create(projectId, "feat", "Feature", "body");
            ctx.PlanningFeatures.Add(feature);
            await ctx.SaveChangesAsync();

            var artifact = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Spec, "spec");
            ctx.PlanningArtifacts.Add(artifact);
            await ctx.SaveChangesAsync();

            artifact.Validate();
            artifact.UpdateBody("new spec", "change reason");
            await ctx.SaveChangesAsync();

            var matFile = PlanningMaterializedFile.Create(projectId, feature.Id, "spec.md", "abc123");
            ctx.PlanningMaterializedFiles.Add(matFile);
            await ctx.SaveChangesAsync();
        }

        using (var ctx = db.CreateContext())
        {
            var project = await ctx.Projects.SingleAsync(p => p.Id == projectId);
            ctx.Projects.Remove(project);
            await ctx.SaveChangesAsync();
        }

        using var verify = db.CreateContext();
        (await verify.PlanningFeatures.CountAsync()).ShouldBe(0);
        (await verify.PlanningArtifacts.CountAsync()).ShouldBe(0);
        (await verify.PlanningArtifactVersions.CountAsync()).ShouldBe(0);
        (await verify.PlanningMaterializedFiles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Planning_CascadeDelete_FromFeature()
    {
        using var db = new TempDatabase();
        Guid projectId, featureId;

        using (var ctx = db.CreateContext())
        {
            var project = Project.Create("feat-cascade", "/p/feat-cascade");
            ctx.Projects.Add(project);
            await ctx.SaveChangesAsync();
            projectId = project.Id;

            var feature = PlanningFeature.Create(projectId, "feat", "Feature", "body");
            ctx.PlanningFeatures.Add(feature);
            await ctx.SaveChangesAsync();
            featureId = feature.Id;

            var artifact = PlanningArtifact.Create(featureId, PlanningArtifactKind.Task, "task");
            ctx.PlanningArtifacts.Add(artifact);

            var matFile = PlanningMaterializedFile.Create(projectId, featureId, "task.md", "def456");
            ctx.PlanningMaterializedFiles.Add(matFile);
            await ctx.SaveChangesAsync();
        }

        using (var ctx = db.CreateContext())
        {
            var feature = await ctx.PlanningFeatures.SingleAsync(f => f.Id == featureId);
            ctx.PlanningFeatures.Remove(feature);
            await ctx.SaveChangesAsync();
        }

        using var verify = db.CreateContext();
        (await verify.PlanningArtifacts.CountAsync()).ShouldBe(0);
        (await verify.PlanningMaterializedFiles.CountAsync()).ShouldBe(0);
        (await verify.PlanningFeatures.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Planning_UniqueProjectSlug_Throws()
    {
        using var db = new TempDatabase();

        using var ctx = db.CreateContext();
        var project = Project.Create("unique-slug", "/p/unique");
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        ctx.PlanningFeatures.Add(PlanningFeature.Create(project.Id, "same-slug", "First", "body"));
        await ctx.SaveChangesAsync();

        ctx.PlanningFeatures.Add(PlanningFeature.Create(project.Id, "same-slug", "Second", "body"));

        await Should.ThrowAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Planning_UniqueFeatureKindOrder_Throws()
    {
        using var db = new TempDatabase();

        using var ctx = db.CreateContext();
        var project = Project.Create("unique-order", "/p/order");
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        var feature = PlanningFeature.Create(project.Id, "feat", "Feature", "body");
        ctx.PlanningFeatures.Add(feature);
        await ctx.SaveChangesAsync();

        ctx.PlanningArtifacts.Add(PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Task, "first", order: 0));
        await ctx.SaveChangesAsync();

        ctx.PlanningArtifacts.Add(PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Task, "duplicate", order: 0));

        await Should.ThrowAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Planning_SetPlanningWorkflow_PersistsBothValues()
    {
        using var db = new TempDatabase();
        Guid projectId;

        using (var ctx = db.CreateContext())
        {
            var project = Project.Create("workflow", "/p/workflow");
            ctx.Projects.Add(project);
            await ctx.SaveChangesAsync();
            projectId = project.Id;

            project.SetPlanningWorkflow(PlanningMaterialization.On, HumanValidationOverride.On);
            await ctx.SaveChangesAsync();
        }

        using var verify = db.CreateContext();
        var loaded = await verify.Projects.SingleAsync(p => p.Id == projectId);
        loaded.PlanningMaterialization.ShouldBe(PlanningMaterialization.On);
        loaded.RequireHumanValidation.ShouldBe(HumanValidationOverride.On);
    }

    [Fact]
    public async Task Planning_ProjectScopedMaterializedFile_RoundtripsAndSurvivesFeatureDelete()
    {
        using var db = new TempDatabase();
        Guid projectId, featureId;

        using (var ctx = db.CreateContext())
        {
            var project = Project.Create("proj-scoped", "/p/proj-scoped");
            ctx.Projects.Add(project);
            await ctx.SaveChangesAsync();
            projectId = project.Id;

            var feature = PlanningFeature.Create(projectId, "feat", "Feature", "body");
            ctx.PlanningFeatures.Add(feature);
            await ctx.SaveChangesAsync();
            featureId = feature.Id;

            var backlogIndex = PlanningMaterializedFile.Create(projectId, null, "BACKLOG.md", "sha-backlog");
            var featureFile = PlanningMaterializedFile.Create(projectId, featureId, "spec.md", "sha-spec");
            ctx.PlanningMaterializedFiles.Add(backlogIndex);
            ctx.PlanningMaterializedFiles.Add(featureFile);
            await ctx.SaveChangesAsync();
        }

        using (var ctx = db.CreateContext())
        {
            var feature = await ctx.PlanningFeatures.SingleAsync(f => f.Id == featureId);
            ctx.PlanningFeatures.Remove(feature);
            await ctx.SaveChangesAsync();
        }

        using var verify = db.CreateContext();
        var remaining = await verify.PlanningMaterializedFiles.ToListAsync();
        remaining.Count.ShouldBe(1);
        remaining[0].RelativePath.ShouldBe("BACKLOG.md");
        remaining[0].FeatureId.ShouldBeNull();
    }

    [Fact]
    public async Task Planning_UniqueArtifactVersion_Throws()
    {
        using var db = new TempDatabase();
        Guid artifactId;

        using (var ctx = db.CreateContext())
        {
            var project = Project.Create("unique-ver", "/p/unique-ver");
            ctx.Projects.Add(project);
            await ctx.SaveChangesAsync();

            var feature = PlanningFeature.Create(project.Id, "feat", "Feature", "body");
            ctx.PlanningFeatures.Add(feature);
            await ctx.SaveChangesAsync();

            var artifact = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Task, "v1");
            ctx.PlanningArtifacts.Add(artifact);
            await ctx.SaveChangesAsync();

            artifact.UpdateBody("v2");
            await ctx.SaveChangesAsync();
            artifactId = artifact.Id;
        }

        using var ctx2 = db.CreateContext();
        var duplicateId = Guid.NewGuid();
        var now = DateTime.UtcNow.ToString("o");

        await Should.ThrowAsync<Microsoft.Data.Sqlite.SqliteException>(async () =>
            await ctx2.Database.ExecuteSqlRawAsync(
                "INSERT INTO PlanningArtifactVersions (Id, ArtifactId, Version, Body, CreatedAt) VALUES ({0}, {1}, 1, 'duplicate', {2})",
                duplicateId, artifactId, now));
    }

    [Fact]
    public async Task Planning_UniqueProjectRelativePath_Throws()
    {
        using var db = new TempDatabase();

        using var ctx = db.CreateContext();
        var project = Project.Create("unique-path", "/p/unique-path");
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        ctx.PlanningMaterializedFiles.Add(
            PlanningMaterializedFile.Create(project.Id, null, "BACKLOG.md", "sha-first"));
        await ctx.SaveChangesAsync();

        ctx.PlanningMaterializedFiles.Add(
            PlanningMaterializedFile.Create(project.Id, null, "BACKLOG.md", "sha-second"));

        await Should.ThrowAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }
}
