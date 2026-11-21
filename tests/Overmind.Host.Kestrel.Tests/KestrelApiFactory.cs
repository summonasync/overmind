using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Overmind.Core;
using Overmind.Host.Loopback;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Kestrel.Tests;

public sealed class KestrelApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath;
    private readonly string _portFilePath;
    private readonly string _webRoot;
    private readonly bool _ownsWebRoot;

    private static readonly object ModelSeedLock = new();
    private static readonly object WebRootGate = new();

    static KestrelApiFactory()
    {
        var modelsRoot = Path.Combine(Path.GetTempPath(), "overmind-kestrel-test-models");
        lock (ModelSeedLock)
        {
            var modelDirectory = Path.Combine(modelsRoot, "all-MiniLM-L6-v2");
            Directory.CreateDirectory(modelDirectory);
            File.WriteAllText(Path.Combine(modelDirectory, "model.onnx"), "fake model for tests");
            File.WriteAllText(Path.Combine(modelDirectory, "vocab.txt"), "[PAD]\n[UNK]\n");
        }

        Environment.SetEnvironmentVariable("OVERMIND_MODELS_DIR", modelsRoot);
    }

    public KestrelApiFactory()
        : this(CreateTemporaryWebRoot(), ownsWebRoot: true)
    {
    }

    internal KestrelApiFactory(string webRoot)
        : this(webRoot, ownsWebRoot: false)
    {
    }

    private KestrelApiFactory(string webRoot, bool ownsWebRoot)
    {
        UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        _databasePath = Path.Combine(Path.GetTempPath(), $"overmind-kestrel-{Guid.NewGuid():N}.db");
        _portFilePath = Path.Combine(Path.GetTempPath(), $"overmind-kestrel-{Guid.NewGuid():N}.port");
        _webRoot = webRoot;
        _ownsWebRoot = ownsWebRoot;
        lock (WebRootGate)
        {
            Environment.SetEnvironmentVariable("OVERMIND_WEB_ROOT", _webRoot);
            _ = Services;
        }
    }

    private static string CreateTemporaryWebRoot()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), $"overmind-kestrel-wwwroot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(webRoot);
        File.WriteAllText(
            Path.Combine(webRoot, "index.html"),
            "<!doctype html><html><body>overmind kestrel test shell</body></html>");
        return webRoot;
    }

    public string BaseUrl
    {
        get
        {
            var server = Services.GetRequiredService<IServer>();
            return server.Features.Get<IServerAddressesFeature>()!.Addresses
                .Last(address => address.StartsWith("http://127.0.0.1", StringComparison.Ordinal));
        }
    }

    public HttpClient CreateWireClient() => new(new SocketsHttpHandler { UseProxy = false })
    {
        BaseAddress = new Uri(BaseUrl)
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        SeedIsolationSettings(_databasePath);

        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Overmind:DatabasePath"] = _databasePath,
                ["Overmind:BindUrl"] = string.Empty,
                ["Overmind:PortFilePath"] = _portFilePath,
            }));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<KestrelTurnStreamGate>();
            services.AddSingleton<KestrelResponseDelayGate>();
            services.AddSingleton<IStartupFilter, KestrelResponseDelayStartupFilter>();
            services.RemoveAll<IConversationStreamSource>();
            services.AddSingleton<IConversationStreamSource>(sp =>
                new StubKestrelConversationStreamSource(sp.GetRequiredService<KestrelTurnStreamGate>()));
            services.AddSingleton<HttpMessageHandler>(sp => new EmbeddedAgentSessionHandler(
                sp.GetRequiredService<IServer>() is TestServer testServer
                    ? testServer.CreateHandler()
                    : new SocketsHttpHandler { UseProxy = false },
                sp.GetRequiredService<EmbeddedAgentSessionState>()));
        });
    }

    private static void SeedIsolationSettings(string databasePath)
    {
        var options = new DbContextOptionsBuilder<OvermindDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        using var db = new OvermindDbContext(options);
        db.Database.Migrate();
        db.AppSettings.Add(AppSetting.Create(SettingKeys.TelegramEnabled, "false"));
        db.AppSettings.Add(AppSetting.Create(SettingKeys.PlanningMaterializationDefault, "off"));
        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var file = _databasePath + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        if (File.Exists(_portFilePath))
        {
            File.Delete(_portFilePath);
        }

        if (_ownsWebRoot && Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
    }
}
