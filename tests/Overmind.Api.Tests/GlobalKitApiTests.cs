using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class GlobalKitApiTests : IDisposable
{
    private readonly string _homeRoot;
    private readonly string _sources;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public GlobalKitApiTests()
    {
        var root = Path.Combine(Path.GetTempPath(), $"overmind-globalapi-{Guid.NewGuid():N}");
        _homeRoot = Path.Combine(root, "home");
        _sources = Path.Combine(root, "sources");
        Directory.CreateDirectory(_homeRoot);
        Directory.CreateDirectory(_sources);
        Directory.CreateDirectory(Path.Combine(_homeRoot, ".claude", "skills"));
        Directory.CreateDirectory(Path.Combine(_homeRoot, ".qwen", "skills"));

        _factory = _factoryWithHome();
        _client = _factory.CreateClient();
    }

    private WebApplicationFactory<Program> _factoryWithHome()
    {
        var home = _homeRoot;
        return new ApiFactory().WithWebHostBuilder(builder => builder.UseSetting("Kits:GlobalHomeRoot", home));
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        var root = Path.GetDirectoryName(_homeRoot);
        if (root is not null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private string WriteSkillSource(string kitName, string skillName)
    {
        var dir = Path.Combine(_sources, kitName, skillName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), $"---\nname: {skillName}\ndescription: Does a thing.\n---\n\nBody.\n");
        return Path.Combine(_sources, kitName);
    }

    private async Task<Guid> SeedBundledKitAsync(string name, string sourcePath)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var kit = SkillKit.Create(name, 1, string.Empty);
        kit.MarkBundled(sourcePath);
        db.SkillKits.Add(kit);
        await db.SaveChangesAsync();
        return kit.Id;
    }

    [Fact]
    public async Task Deploy_removes_and_state_round_trip_on_the_global_door()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kitId = await SeedBundledKitAsync("dotnet-crew", source);
        var skillFile = Path.Combine(_homeRoot, ".claude", "skills", "dotnet-coder", "SKILL.md");

        var deploy = await _client.PostAsync($"/api/kits/{kitId}/global/claude?force=false", null);
        deploy.StatusCode.ShouldBe(HttpStatusCode.OK);
        File.Exists(skillFile).ShouldBeTrue();

        var state = await _client.GetAsync("/api/kits/global/state");
        state.StatusCode.ShouldBe(HttpStatusCode.OK);
        var snapshot = await state.Content.ReadFromJsonAsync<GlobalStateItemJson[]>();
        snapshot.ShouldNotBeNull();
        var pair = snapshot.Single(s => s.KitId == kitId && s.Platform == "claude");
        pair.State.ShouldBe("deployed");
        snapshot.Single(s => s.KitId == kitId && s.Platform == "qwen").State.ShouldBe("undeployed");

        var remove = await _client.DeleteAsync($"/api/kits/{kitId}/global/claude");
        remove.StatusCode.ShouldBe(HttpStatusCode.OK);
        File.Exists(skillFile).ShouldBeFalse();

        var stateAfter = await _client.GetAsync("/api/kits/global/state");
        var snapshotAfter = await stateAfter.Content.ReadFromJsonAsync<GlobalStateItemJson[]>();
        snapshotAfter!.Single(s => s.KitId == kitId && s.Platform == "claude").State.ShouldBe("undeployed");
    }

    [Fact]
    public async Task Delete_of_a_deployed_kit_requires_the_files_choice()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kitId = await SeedBundledKitAsync("dotnet-crew", source);
        var skillFile = Path.Combine(_homeRoot, ".claude", "skills", "dotnet-coder", "SKILL.md");
        (await _client.PostAsync($"/api/kits/{kitId}/global/claude?force=false", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var noChoice = await _client.DeleteAsync($"/api/kits/{kitId}");
        noChoice.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        File.Exists(skillFile).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_keep_leaves_files_and_removes_the_kit()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kitId = await SeedBundledKitAsync("dotnet-crew", source);
        var skillFile = Path.Combine(_homeRoot, ".claude", "skills", "dotnet-coder", "SKILL.md");
        (await _client.PostAsync($"/api/kits/{kitId}/global/claude?force=false", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var delete = await _client.DeleteAsync($"/api/kits/{kitId}?deployedFiles=keep");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        File.Exists(skillFile).ShouldBeTrue();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        (await db.GlobalMaterializedFiles.AnyAsync(f => f.KitId == kitId)).ShouldBeFalse();
        (await db.SkillKits.AnyAsync(k => k.Id == kitId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_remove_deletes_files_and_the_kit()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kitId = await SeedBundledKitAsync("dotnet-crew", source);
        var skillFile = Path.Combine(_homeRoot, ".claude", "skills", "dotnet-coder", "SKILL.md");
        (await _client.PostAsync($"/api/kits/{kitId}/global/claude?force=false", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var delete = await _client.DeleteAsync($"/api/kits/{kitId}?deployedFiles=remove");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        File.Exists(skillFile).ShouldBeFalse();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        (await db.GlobalMaterializedFiles.AnyAsync(f => f.KitId == kitId)).ShouldBeFalse();
        (await db.SkillKits.AnyAsync(k => k.Id == kitId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_remove_cuts_woven_marker_blocks()
    {
        var dir = Path.Combine(_sources, "sociality");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SOCIAL_DICTIONARY.md"), "# Tone rules.\n");
        var kitId = await SeedBundledKitAsync("sociality", dir);
        var entry = Path.Combine(_homeRoot, ".claude", "CLAUDE.md");
        File.WriteAllText(entry, "# My agent context\n\nOwner content stays.\n");
        (await _client.PostAsync($"/api/kits/{kitId}/global/claude", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        File.ReadAllText(entry).ShouldContain("===OVERMIND");

        var delete = await _client.DeleteAsync($"/api/kits/{kitId}?deployedFiles=remove");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        File.Exists(entry).ShouldBeTrue();
        var entryBody = File.ReadAllText(entry);
        entryBody.ShouldNotContain("===OVERMIND");
        entryBody.ShouldContain("Owner content stays.");
    }

    [Fact]
    public async Task Delete_rejects_an_invalid_deployed_files_flag()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kitId = await SeedBundledKitAsync("dotnet-crew", source);
        (await _client.PostAsync($"/api/kits/{kitId}/global/claude?force=false", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var delete = await _client.DeleteAsync($"/api/kits/{kitId}?deployedFiles=whatever");
        delete.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_refuses_a_deployed_mandatory_kit()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kitId = await SeedBundledKitAsync("dotnet-crew", source);
        (await _client.PostAsync($"/api/kits/{kitId}/global/claude?force=false", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            var kit = await db.SkillKits.FindAsync(kitId);
            kit!.SetMandatory(true);
            await db.SaveChangesAsync();
        }

        var delete = await _client.DeleteAsync($"/api/kits/{kitId}?deployedFiles=keep");
        delete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_remove_dematerializes_kimi_code_by_its_registry_name()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kitId = await SeedBundledKitAsync("dotnet-crew", source);
        var skillFile = Path.Combine(_homeRoot, ".kimi-code", "skills", "dotnet-coder", "SKILL.md");
        Directory.CreateDirectory(Path.Combine(_homeRoot, ".kimi-code", "skills"));
        var kimiDeploy = await _client.PostAsync($"/api/kits/{kitId}/global/kimi-code?force=false", null);
        if (kimiDeploy.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception("KIMI DEPLOY: " + await kimiDeploy.Content.ReadAsStringAsync());
        }
        File.Exists(skillFile).ShouldBeTrue();

        var delete = await _client.DeleteAsync($"/api/kits/{kitId}?deployedFiles=remove");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        File.Exists(skillFile).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_of_an_undeployed_kit_ignores_the_flag()
    {
        var kitId = await SeedBundledKitAsync("dotnet-crew", WriteSkillSource("dotnet-crew", "dotnet-coder"));

        var delete = await _client.DeleteAsync($"/api/kits/{kitId}?deployedFiles=remove");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_active_links_guard_precedes_the_files_choice()
    {
        var source = WriteSkillSource("dotnet-crew", "dotnet-coder");
        var kitId = await SeedBundledKitAsync("dotnet-crew", source);
        (await _client.PostAsync($"/api/kits/{kitId}/global/claude?force=false", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        var project = await SeedProjectAsync();

        var create = await _client.PostAsync($"/api/projects/{project}/kits/{kitId}/activate", null);
        create.StatusCode.ShouldBe(HttpStatusCode.OK);

        var delete = await _client.DeleteAsync($"/api/kits/{kitId}?deployedFiles=keep");
        delete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private async Task<Guid> SeedProjectAsync()
    {
        var disk = Path.Combine(Path.GetTempPath(), $"delete-guard-proj-{Guid.NewGuid():N}");
        Directory.CreateDirectory(disk);
        var create = await _client.PostAsJsonAsync(
            "/api/projects", new { name = "delete-guard-proj", diskPath = disk, description = "" });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await create.Content.ReadFromJsonAsync<ProjectJson>();
        return project!.Id;
    }

    [Fact]
    public async Task Deploy_refuses_a_manual_kit_and_an_unknown_agent()
    {
        var create = await _client.PostAsJsonAsync("/api/kits", new { name = "manual-kit", version = 1, description = "" });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var kit = await create.Content.ReadFromJsonAsync<KitJson>();

        var deploy = await _client.PostAsync($"/api/kits/{kit!.Id}/global/claude", null);
        deploy.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var bundledId = await SeedBundledKitAsync("js-crew", WriteSkillSource("js-crew", "web-coder"));
        var unknown = await _client.PostAsync($"/api/kits/{bundledId}/global/nonexistent-agent", null);
        unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deploy_weaves_a_prose_only_kit_into_the_entry_file()
    {
        var dir = Path.Combine(_sources, "sociality");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SOCIAL_DICTIONARY.md"), "# Tone rules.\n");
        var kitId = await SeedBundledKitAsync("sociality", dir);
        var entry = Path.Combine(_homeRoot, ".claude", "CLAUDE.md");

        var deploy = await _client.PostAsync($"/api/kits/{kitId}/global/claude", null);
        deploy.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reportBody = await deploy.Content.ReadAsStringAsync();

        File.Exists(entry).ShouldBeTrue(reportBody);
        File.ReadAllText(entry).ShouldContain("===OVERMIND SOCIALITY KIT START===\n# Tone rules.\n===OVERMIND SOCIALITY KIT END===");
    }

    [Fact]
    public async Task Remove_refuses_the_mandatory_kit()    {
        var source = WriteSkillSource("overmind-ceremony", "overmind-ceremony");
        var kitId = await SeedBundledKitAsync("overmind-ceremony", source);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var kit = await db.SkillKits.FindAsync(kitId);
        kit!.SetMandatory(true);
        await db.SaveChangesAsync();

        var remove = await _client.DeleteAsync($"/api/kits/{kitId}/global/claude");
        remove.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    public sealed record GlobalStateItemJson(
        Guid KitId,
        string KitName,
        bool Mandatory,
        string Platform,
        string State,
        string[] Paths,
        GlobalDriftJson[] Drifts);

    public sealed record GlobalDriftJson(Guid KitId, string Platform, string Path, string Kind);

    public sealed record KitJson(Guid Id, string Name, int Version);

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath);
}
