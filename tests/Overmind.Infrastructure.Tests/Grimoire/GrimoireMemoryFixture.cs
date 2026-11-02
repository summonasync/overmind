using Microsoft.Extensions.DependencyInjection;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Grimoire;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Tests.Grimoire;

public sealed class GrimoireMemoryFixture : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly ServiceProvider _provider;

    public FakeGrimoireServer Server { get; } = new();

    public GrimoireMemoryClient Client { get; }

    public GrimoireMemoryFixture()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new AppSettingsService(_temp.CreateContext()));
        _provider = services.BuildServiceProvider();
        Client = new GrimoireMemoryClient(
            _provider.GetRequiredService<IServiceScopeFactory>(), new HttpClient(Server));
    }

    public async Task ConfigureOverrideUrlAsync(string url)
    {
        await using var context = _temp.CreateContext();
        await new AppSettingsService(context)
            .SetAsync(SettingKeys.AgentGrimoireBaseUrl, url, CancellationToken.None);
    }

    public void Dispose()
    {
        _provider.Dispose();
        Server.Dispose();
        _temp.Dispose();
    }
}
