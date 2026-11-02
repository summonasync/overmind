using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Overmind.Core;
using Overmind.Host.Loopback;
using Overmind.Infrastructure.Agent;

namespace Overmind.Api.Tests;

public sealed class StubConversationStreamSource : IConversationStreamSource
{
    public async IAsyncEnumerable<ConversationStreamEvent> StreamAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        yield return ConversationStreamEvent.Delta("stub delta one ");
        yield return ConversationStreamEvent.ToolCall("planning_task_queue");
        yield return ConversationStreamEvent.Delta("stub delta two");
        yield return ConversationStreamEvent.Completed(321);
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath;
    private readonly bool _preserveDatabase;

    private static readonly object ModelSeedLock = new();

    static ApiFactory()
    {
        var modelsRoot = Path.Combine(Path.GetTempPath(), "overmind-api-test-models");
        lock (ModelSeedLock)
        {
            var modelDirectory = Path.Combine(modelsRoot, "all-MiniLM-L6-v2");
            Directory.CreateDirectory(modelDirectory);
            File.WriteAllText(Path.Combine(modelDirectory, "model.onnx"), "fake model for tests");
            File.WriteAllText(Path.Combine(modelDirectory, "vocab.txt"), "[PAD]\n[UNK]\n");
        }

        Environment.SetEnvironmentVariable("OVERMIND_MODELS_DIR", modelsRoot);
    }

    public ApiFactory(string? databasePath = null, bool preserveDatabase = false)
    {
        _databasePath = databasePath ?? Path.Combine(Path.GetTempPath(), $"overmind-api-{Guid.NewGuid():N}.db");
        _preserveDatabase = preserveDatabase;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Overmind:DatabasePath"] = _databasePath,
                ["Overmind:BindUrl"] = string.Empty,
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IConversationStreamSource>();
            services.AddSingleton<IConversationStreamSource, StubConversationStreamSource>();
            services.AddSingleton<HttpMessageHandler>(sp => new EmbeddedAgentSessionHandler(
                sp.GetRequiredService<IServer>() is TestServer testServer
                    ? testServer.CreateHandler()
                    : new SocketsHttpHandler { UseProxy = false },
                sp.GetRequiredService<EmbeddedAgentSessionState>()));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (_preserveDatabase)
        {
            return;
        }

        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var file = _databasePath + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
