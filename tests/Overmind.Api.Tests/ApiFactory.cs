using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Overmind.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath;

    public ApiFactory()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"overmind-api-{Guid.NewGuid():N}.db");
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
