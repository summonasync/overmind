using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Overmind.Api.Tests;

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
