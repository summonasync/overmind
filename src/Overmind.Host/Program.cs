using Microsoft.EntityFrameworkCore;
using Overmind.Host;
using Overmind.Host.Api;
using Overmind.Infrastructure.Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddOvermind();

    var configuredBindUrl = builder.Configuration["Overmind:BindUrl"];
    if (configuredBindUrl is null)
    {
        var port = OvermindPorts.ResolvePreferredPort(OvermindPorts.DefaultPort);
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
    }
    else if (configuredBindUrl.Length > 0)
    {
        builder.WebHost.UseUrls(configuredBindUrl);
    }

    var app = builder.Build();

    app.MapProjectsApi();
    app.MapEntriesApi();
    app.MapSearchApi();
    app.MapKitsApi();
    app.MapSettingsApi();
    app.MapHealthApi();

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        await db.Database.MigrateAsync();
    }

    await app.StartAsync();

    var portFilePath = app.Configuration["Overmind:PortFilePath"];
    if (portFilePath is not null || !app.Environment.IsEnvironment("Testing"))
    {
        var targetPath = portFilePath ?? OvermindPaths.PortFilePath;
        PortFile.Write(app, targetPath);
        Log.Information("Port file written to {PortFilePath}", targetPath);
    }

    await app.WaitForShutdownAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
