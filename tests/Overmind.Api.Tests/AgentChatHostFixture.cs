using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Host;
using Overmind.Host.Api;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Persistence;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentChatHostFixture : IAsyncLifetime
{
    private WebApplication _app = null!;
    private string _databasePath = string.Empty;

    public string BaseUrl { get; private set; } = string.Empty;
    public TurnStreamGate Gate { get; private set; } = new();

    public async Task InitializeAsync()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"overmind-turn-lifecycle-{Guid.NewGuid():N}.db");
        var port = ReserveFreePort();

        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = "Testing";
        builder.Configuration["Overmind:DatabasePath"] = _databasePath;
        builder.Logging.ClearProviders();
        builder.Services.AddOvermind();
        builder.Services.AddSingleton<TurnStreamGate>();
        builder.Services.RemoveAll<IConversationStreamSource>();
        builder.Services.AddSingleton<IConversationStreamSource>(sp =>
            new StubConversationStreamSource(sp.GetRequiredService<TurnStreamGate>()));
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

        _app = builder.Build();
        _app.MapAgentChatApi();

        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        await _app.StartAsync();
        BaseUrl = $"http://127.0.0.1:{port}";
        Gate = _app.Services.GetRequiredService<TurnStreamGate>();
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var file = _databasePath + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    public HttpClient CreateClient() => new() { BaseAddress = new Uri(BaseUrl) };

    public IServiceProvider Services => _app.Services;

    private static int ReserveFreePort()
    {
        var listener = System.Net.Sockets.TcpListener.Create(0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
