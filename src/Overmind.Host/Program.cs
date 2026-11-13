using System.Text.Encodings.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Overmind.Host;
using Overmind.Host.Api;
using Overmind.Infrastructure.Kits;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Serilog;

if (OperatingSystem.IsWindows())
{
    ConsoleAttach.TryAttachParent();
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try
{
    if (BacklogImportCli.IsRequested(args))
    {
        return await BacklogImportCli.RunAsync(args, CancellationToken.None);
    }

    if (PlanningCli.IsRequested(args))
    {
        return await PlanningCli.RunAsync(args, CancellationToken.None);
    }

    Directory.CreateDirectory(OvermindPaths.DataDirectory);

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory,
    });
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
            await RunAsync(builder, noTray || isTesting, args);
        }

        return 0;
    }

    await RunAsync(builder, noTray || isTesting, args);
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

static async Task RunAsync(WebApplicationBuilder builder, bool headless, string[] args)
{
    builder.Services.AddOvermind();

    // Default HTML-safe escaping bloats Cyrillic payloads ~4x (FEATURELIST/overmind-features-2026-09-23.md
    // OM-1); every consumer here is a fetch client or the MCP adapter, never HTML embedding.
    builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping);

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

    app.UseMiddleware<SessionTouchMiddleware>();
    app.UseMiddleware<RequestBufferingMiddleware>();
    app.UseMiddleware<OperationStorageMiddleware>();

    var webRootOverride = Environment.GetEnvironmentVariable("OVERMIND_WEB_ROOT");
    var webRoot = webRootOverride is { Length: > 0 } ? webRootOverride : ResolveWebRoot();

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
        var staticFileOptions = new StaticFileOptions { FileProvider = webFileProvider };
        staticFileOptions.OnPrepareResponse = context =>
        {
            if (context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                context.Context.Response.Headers.CacheControl = "no-cache";
            }
        };
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = webFileProvider });
        app.UseStaticFiles(staticFileOptions);
        app.MapFallbackToFile("index.html", staticFileOptions);
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
    app.MapKitImportApi();
    app.MapSettingsApi();
    app.MapUsageApi();
    app.MapGrimoireApi();
    app.MapHealthApi();
    app.MapSystemApi();
    app.MapSystemRootsApi();
    app.MapPlanningFeaturesApi();
    app.MapPlanningDependenciesApi();
    app.MapPlanningArtifactsApi();
    app.MapPlanningWorkflowApi();
    app.MapClaimSettingsApi();
    app.MapApprovalSettingsApi();
    app.MapLanesApi();
    app.MapPlanningDirectivesApi();
    app.MapWaveChatApi();
    app.MapWaveParticipantsApi();
    app.MapPlanningMaterializationApi();
    app.MapPlanningImportApi();
    app.MapPlanningExportApi();
    app.MapAgentPlanningApi();
    app.MapAgentSessionsApi();
    app.MapAgentUsageApi();
    app.MapAgentWakeApi();
    app.MapAgentIssuesApi();
    app.MapIssuesApi();
    app.MapCrossProjectIssuesApi();
    app.MapFrontmatterKeysApi();
    app.MapOnboardApi();
    app.MapTagsApi();
    app.MapNotificationsApi();
    app.MapHumanInboxApi();
    app.MapAgentChatApi();
    app.MapAgentRoutesApi();
    app.MapAttentionApi();
    app.MapShutdownApi();

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<BundledSeedService>()
            .RunAsync(app.Lifetime.ApplicationStopping);
        await BacklogImportCommand.TryRunAsync(scope.ServiceProvider, args, app.Lifetime.ApplicationStopping);
        await scope.ServiceProvider.GetRequiredService<PlanningStatusBackfill>()
            .RunAsync(app.Lifetime.ApplicationStopping);
    }

    await app.StartAsync();

    if (app.Urls.FirstOrDefault() is { } boundUrl && new Uri(boundUrl) is { Port: not 0 } bound)
    {
        app.Services.GetRequiredService<BrowserOrigin>()
            .Bind(BrowserUri.FromLoopback(bound));
    }

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

static string ResolveWebRoot()
{
    var packLayoutWebRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "web"));
    if (Directory.Exists(packLayoutWebRoot))
    {
        return packLayoutWebRoot;
    }

    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    for (var level = 0; level < 8 && directory.Parent is not null; level++)
    {
        directory = directory.Parent;
        var candidate = Path.Combine(directory.FullName, "web", "out");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }
    }

    return packLayoutWebRoot;
}

public partial class Program;
