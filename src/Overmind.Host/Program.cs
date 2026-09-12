using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
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
    Directory.CreateDirectory(OvermindPaths.DataDirectory);

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    var isTesting = builder.Environment.IsEnvironment("Testing");
    var noTray = args.Contains("--no-tray", StringComparer.OrdinalIgnoreCase);

    if (!isTesting)
    {
        var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            var uri = SingleInstance.TryGetRunningInstanceUri();
            if (uri is not null)
            {
                var browserUri = BrowserUri.FromLoopback(uri);
                Log.Information("Overmind is already running at {Uri}; opening it in the browser", browserUri);
                SingleInstance.OpenInDefaultBrowser(browserUri);
            }
            else
            {
                Log.Warning(
                    "Overmind single-instance lock is held, but no live port was found in {PortFile}",
                    OvermindPaths.PortFilePath);
            }

            return 0;
        }

        using (instance)
        {
            await RunAsync(builder, noTray || isTesting);
        }

        return 0;
    }

    await RunAsync(builder, noTray || isTesting);
    return 0;
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

static async Task RunAsync(WebApplicationBuilder builder, bool headless)
{
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

    var webRootOverride = Environment.GetEnvironmentVariable("OVERMIND_WEB_ROOT");
    var webRoot = webRootOverride is { Length: > 0 }
        ? webRootOverride
        : Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "web", "out"));

    if (Directory.Exists(webRoot))
    {
        var webFileProvider = new PhysicalFileProvider(webRoot);

        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            {
                var path = context.Request.Path.Value;
                if (path is { Length: > 1 }
                    && !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                    && !Path.HasExtension(path))
                {
                    var htmlFile = webFileProvider.GetFileInfo(path.TrimStart('/') + ".html");
                    if (htmlFile.Exists)
                    {
                        context.Request.Path = path + ".html";
                    }
                }
            }

            await next();
        });

        app.UseRouting();
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = webFileProvider });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = webFileProvider });
        app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = webFileProvider });
        Log.Information("Serving web UI from {WebRoot}", webRoot);
    }
    else
    {
        Log.Warning("Web UI not found at {WebRoot}; UI will not be served", webRoot);
    }

    app.MapProjectsApi();
    app.MapEntriesApi();
    app.MapSearchApi();
    app.MapKitsApi();
    app.MapSettingsApi();
    app.MapHealthApi();
    app.MapSystemApi();
    app.MapTreeRootsApi();
    app.MapPlanningFeaturesApi();
    app.MapPlanningArtifactsApi();
    app.MapPlanningWorkflowApi();

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

    if (headless)
    {
        Log.Information("UI ready at {OpenUri}", app.Urls.FirstOrDefault() ?? "unknown address");
        await app.WaitForShutdownAsync();
        return;
    }

    var openUri = BrowserUri.FromLoopback(new Uri(app.Urls.First()));
    using var trayShutdown = new CancellationTokenSource();
    using var stoppingRegistration = app.Lifetime.ApplicationStopping.Register(trayShutdown.Cancel);

    Log.Information("Tray icon active; UI ready at {OpenUri}", openUri);

    var trayExited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var uiThread = new Thread(() =>
    {
        try
        {
            TrayApplication.Run(openUri, () =>
            {
                app.Lifetime.StopApplication();
                return Task.CompletedTask;
            }, trayShutdown.Token);
        }
        catch (TaskCanceledException)
        {
            // Expected on Linux: disposing the tray icon throws from DBusTrayIconImpl.WatchAsync
            // (avalonia#21979). Shutdown is already in progress, so this is benign.
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Tray UI loop crashed");
        }
        finally
        {
            trayExited.TrySetResult();
        }
    })
    { Name = "Overmind Tray UI" };
    if (OperatingSystem.IsWindows())
    {
        uiThread.SetApartmentState(ApartmentState.STA);
    }

    uiThread.Start();

    await trayExited.Task;
    await app.StopAsync();
}

public partial class Program;
