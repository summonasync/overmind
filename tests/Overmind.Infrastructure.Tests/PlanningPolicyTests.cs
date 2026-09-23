using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningPolicyTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly AppSettingsService _settings;
    private readonly PlanningPolicy _policy;
    private readonly Project _project;

    public PlanningPolicyTests()
    {
        _context = _db.CreateContext();
        _settings = new AppSettingsService(_context);
        _policy = new PlanningPolicy(_context, _settings);

        _project = Project.Create($"test-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(_project);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task ValidationGate_ProjectOn_ReturnsTrue()
    {
        _project.SetPlanningWorkflow(PlanningMaterialization.Inherit, HumanValidationOverride.On);
        await _context.SaveChangesAsync();

        (await _policy.ResolveValidationGateAsync(_project.Id, default)).ShouldBeTrue();
    }

    [Fact]
    public async Task ValidationGate_ProjectOff_BeatsGlobalOn()
    {
        await _settings.SetAsync(
            SettingKeys.PlanningRequireHumanValidationDefault, "on", default);
        _project.SetPlanningWorkflow(PlanningMaterialization.Inherit, HumanValidationOverride.Off);
        await _context.SaveChangesAsync();

        (await _policy.ResolveValidationGateAsync(_project.Id, default)).ShouldBeFalse();
    }

    [Fact]
    public async Task ValidationGate_ProjectUnset_FollowsGlobalDefault()
    {
        (await _policy.ResolveValidationGateAsync(_project.Id, default)).ShouldBeFalse();

        await _settings.SetAsync(
            SettingKeys.PlanningRequireHumanValidationDefault, "on", default);

        (await _policy.ResolveValidationGateAsync(_project.Id, default)).ShouldBeTrue();
    }

    [Fact]
    public async Task Materialization_ProjectOverride_Wins()
    {
        await _settings.SetAsync(SettingKeys.PlanningMaterializationDefault, "archive", default);
        _project.SetPlanningWorkflow(PlanningMaterialization.On, HumanValidationOverride.Unset);
        await _context.SaveChangesAsync();

        (await _policy.ResolveMaterializationAsync(_project.Id, default))
            .ShouldBe(PlanningMaterialization.On);
    }

    [Fact]
    public async Task Materialization_ProjectInherit_FollowsGlobalDefault()
    {
        (await _policy.ResolveMaterializationAsync(_project.Id, default))
            .ShouldBe(PlanningMaterialization.On);

        await _settings.SetAsync(SettingKeys.PlanningMaterializationDefault, "archive", default);

        (await _policy.ResolveMaterializationAsync(_project.Id, default))
            .ShouldBe(PlanningMaterialization.Archive);
    }

    [Fact]
    public async Task Materialization_GlobalDefaultNeverInherit()
    {
        await _settings.SetAsync(SettingKeys.PlanningMaterializationDefault, "inherit", default);

        (await _policy.ResolveMaterializationAsync(_project.Id, default))
            .ShouldBe(PlanningMaterialization.Off);
    }

    [Fact]
    public async Task Resolve_MissingProject_Throws()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => _policy.ResolveValidationGateAsync(Guid.NewGuid(), default));
        await Should.ThrowAsync<InvalidOperationException>(
            () => _policy.ResolveMaterializationAsync(Guid.NewGuid(), default));
    }
}
