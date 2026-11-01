using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wake;

namespace Overmind.Infrastructure.Tests;

public static class TestWakeFanout
{
    public static (WakeDropFanout Fanout, ServiceProvider Services) Create(
        TempDatabase db, string? root = null)
    {
        var services = new ServiceCollection()
            .AddScoped(_ => db.CreateContext())
            .AddScoped<AgentWakeService>()
            .AddSingleton(new WakeDropWriter(
                root ?? Path.Combine(Path.GetTempPath(), $"wake-{Guid.NewGuid():N}")))
            .AddSingleton<ILogger<WakeDropFanout>>(NullLogger<WakeDropFanout>.Instance)
            .AddSingleton<WakeDropFanout>()
            .BuildServiceProvider();

        return (services.GetRequiredService<WakeDropFanout>(), services);
    }
}
