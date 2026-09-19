using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using Overmind.Mcp;
using Overmind.Mcp.Registration;

if (args.Length > 0 && args[0] == "install")
{
    if (!InstallOptionsParser.TryParse(args[1..], out var installOptions, out var installError))
    {
        Console.Error.WriteLine(installError);
        Console.Error.WriteLine(InstallOptionsParser.UsageText);
        return 2;
    }

    var install = new InstallCommand(
        AgentPathEnvironment.Current(),
        Directory.GetCurrentDirectory(),
        Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0],
        Console.Out,
        Console.Error);

    return await install.RunAsync(installOptions, CancellationToken.None);
}

var baseUrl = ApiBaseUrl.Resolve();
Console.Error.WriteLine($"[overmind-mcp] overmind API base URL: {baseUrl}");

var http = OvermindApiClient.CreateHttpClient(baseUrl);

try
{
    await OvermindApiClient.CheckHealthAsync(http, CancellationToken.None);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[overmind-mcp] FATAL: overmind API at {baseUrl} is unreachable: {ex.Message}");
    Console.Error.WriteLine("[overmind-mcp] start the overmind host first or point OVERMIND_API_URL at a running instance.");
    return 2;
}

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Logging.ClearProviders();

builder.Services.AddSingleton(new OvermindApiClient(http));
builder.Services.AddSingleton<OvermindTools>();
builder.Services.AddSingleton<OvermindPlanningTools>();
builder.Services.AddSingleton<OvermindResources>();
builder.Services.AddSingleton<OvermindPlanningResources>();
builder.Services.AddSingleton<SessionBootstrapPrompt>();

var adapterVersion = typeof(ApiBaseUrl).Assembly
                         .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                     ?? "0.0.0";

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation { Name = "overmind", Title = "Overmind", Version = adapterVersion };
        options.ServerInstructions =
            "Overmind shared-context adapter. Read overmind://resources for project gates, guards, notes, decisions and skills. " +
            "Use tools for project/entry CRUD and fulltext or semantic search.";
    })
    .WithStdioServerTransport()
    .WithTools<OvermindTools>()
    .WithTools<OvermindPlanningTools>()
    .WithResources<OvermindResources>()
    .WithResources<OvermindPlanningResources>()
    .WithPrompts<SessionBootstrapPrompt>();

await builder.Build().RunAsync();
return 0;
