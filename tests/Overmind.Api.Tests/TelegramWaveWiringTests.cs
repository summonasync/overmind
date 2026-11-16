using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Infrastructure.Telegram;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class TelegramWaveWiringTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    [Fact]
    public void CallbackDoor_ResolvesTheRouterAsTheOnlyHandler()
    {
        _factory.Services.GetServices<ITelegramCallbackHandler>().Count().ShouldBe(1);

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITelegramCallbackHandler>()
            .ShouldBeOfType<TelegramCallbackRouter>();
    }

    [Fact]
    public void CallbackRouter_CarriesBothDoors()
    {
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        provider.GetRequiredService<TelegramNotificationCallbackHandler>().ShouldNotBeNull();
        provider.GetRequiredService<TelegramWaveCallbackHandler>().ShouldNotBeNull();
    }

    [Fact]
    public void WaveCommandDoor_IsRegisteredForTheMessageHandler()
    {
        using var scope = _factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITelegramWaveCommandService>()
            .ShouldBeOfType<TelegramWaveCommandService>();
    }

    [Fact]
    public void SnapshotDoor_IsItsOwnFlowClassAndGrowsNoMembersOnTheSendDoor()
    {
        using var scope = _factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITelegramWaveSnapshotService>()
            .ShouldBeOfType<TelegramWaveSnapshotService>();

        var sendDoorMembers = typeof(TelegramWaveCommandService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .Concat(typeof(ITelegramWaveCommandService).GetMethods().Select(method => method.Name));
        sendDoorMembers.ShouldNotContain(name => name.Contains("Snapshot", StringComparison.Ordinal));
    }

    [Fact]
    public void SnapshotDoor_CarriesNeitherTheDraftStoreNorTheSendFlowAsADependency()
    {
        var constructor = typeof(TelegramWaveSnapshotService).GetConstructors().Single();

        constructor.GetParameters()
            .ShouldNotContain(parameter => parameter.ParameterType == typeof(TelegramWaveDraftStore));
        constructor.GetParameters()
            .ShouldNotContain(parameter => parameter.ParameterType == typeof(ITelegramWaveCommandService));
    }

    [Fact]
    public void WaveDraftStore_IsOneInstanceSharedAcrossScopes()
    {
        var root = _factory.Services.GetRequiredService<TelegramWaveDraftStore>();

        using var scopeA = _factory.Services.CreateScope();
        using var scopeB = _factory.Services.CreateScope();

        scopeA.ServiceProvider.GetRequiredService<TelegramWaveDraftStore>().ShouldBeSameAs(root);
        scopeB.ServiceProvider.GetRequiredService<TelegramWaveDraftStore>().ShouldBeSameAs(root);
    }

    [Fact]
    public void WaveDoors_ShareTheDraftStoreOfTheirOwnScopeGraph()
    {
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var store = provider.GetRequiredService<TelegramWaveDraftStore>();

        provider.GetRequiredService<ITelegramWaveCommandService>().ShouldBeOfType<TelegramWaveCommandService>();
        provider.GetRequiredService<TelegramWaveCallbackHandler>().ShouldNotBeNull();
        provider.GetRequiredService<ITelegramMessageHandler>().ShouldBeOfType<TelegramAgentChatHandler>();
        provider.GetRequiredService<TelegramWaveDraftStore>().ShouldBeSameAs(store);
    }

    public void Dispose() => _factory.Dispose();
}
