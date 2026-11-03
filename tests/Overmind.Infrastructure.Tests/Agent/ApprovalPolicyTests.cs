using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class ApprovalPolicyTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    [Theory]
    [InlineData(DelegateApprovalsOverride.Unset, "off", false)]
    [InlineData(DelegateApprovalsOverride.Unset, "on", true)]
    [InlineData(DelegateApprovalsOverride.On, "off", true)]
    [InlineData(DelegateApprovalsOverride.On, "on", true)]
    [InlineData(DelegateApprovalsOverride.Off, "off", false)]
    [InlineData(DelegateApprovalsOverride.Off, "on", false)]
    public async Task ResolveDelegateApprovalsAsync_Matrix_CombinesOverrideWithGlobalDefault(
        DelegateApprovalsOverride overrideMode, string globalDefault, bool expected)
    {
        var projectId = await CreateProjectAsync(overrideMode);
        await SetGlobalDefaultAsync(globalDefault);
        var policy = CreatePolicy();

        (await policy.ResolveDelegateApprovalsAsync(projectId, CancellationToken.None)).ShouldBe(expected);
    }

    [Fact]
    public async Task ResolveDelegateApprovalsAsync_UnknownProject_Throws()
    {
        var policy = CreatePolicy();

        await Should.ThrowAsync<InvalidOperationException>(
            () => policy.ResolveDelegateApprovalsAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private ApprovalPolicy CreatePolicy() => new(
        _temp.CreateContext(),
        new AppSettingsService(_temp.CreateContext()));

    private async Task<Guid> CreateProjectAsync(DelegateApprovalsOverride mode)
    {
        using var context = _temp.CreateContext();
        var name = $"approval-{Guid.NewGuid():N}";
        var project = Project.Create(name, $"/tmp/{name}");
        project.SetDelegateApprovals(mode);
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project.Id;
    }

    private async Task SetGlobalDefaultAsync(string value) =>
        await new AppSettingsService(_temp.CreateContext())
            .SetAsync(SettingKeys.AgentDelegateApprovalsDefault, value, CancellationToken.None);

    public void Dispose() => _temp.Dispose();
}
