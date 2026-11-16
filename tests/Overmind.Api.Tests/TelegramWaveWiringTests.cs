using System.Linq;
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

    public void Dispose() => _factory.Dispose();
}
