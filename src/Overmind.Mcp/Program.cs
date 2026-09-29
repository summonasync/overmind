using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
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

// The SDK freezes McpJsonUtilities.DefaultOptions, and the JSON-RPC envelope goes through the
// source-generated JsonContext with its own options snapshot; the default HTML-safe encoder
// bloats Cyrillic payloads ~4x (FEATURELIST/overmind-features-2026-09-23.md OM-1). The read-only
// guard lives on the property setters only, so the encoder field is written directly on both
// instances; if SDK internals shift, we degrade to escaped output instead of failing to start.
try
{
    var encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    var encoderField = typeof(JsonSerializerOptions).GetField(
        "_encoder", BindingFlags.NonPublic | BindingFlags.Instance);

    encoderField?.SetValue(McpJsonUtilities.DefaultOptions, encoder);

    var contextType = typeof(McpJsonUtilities).GetNestedType("JsonContext", BindingFlags.NonPublic);
    var contextDefault = contextType?
        .GetProperty("Default", BindingFlags.Public | BindingFlags.Static)?
        .GetValue(null);
    if (contextDefault is JsonSerializerContext context)
    {
        encoderField?.SetValue(context.Options, encoder);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[overmind-mcp] WARN: relaxed JSON encoder not applied: {ex.Message}");
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
builder.Services.AddSingleton<OvermindOnboardingTools>();
builder.Services.AddSingleton<OvermindNotificationTools>();
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
            "Overmind shared-context adapter. Call overmind_onboard first — it resolves your project and returns the onboarding quickstart. " +
            "Project gates, guards, notes, decisions and skills live as entries: list them with list_entries (type=gate|guard|note|decision|skill) " +
            "and read them with read_entry — the onboard payload names the active project. " +
            "Use tools for project/entry CRUD and fulltext or semantic search. " +
            "When overmind_notify returns a url, quote the url in chat when asking for an approval or reporting a question so the human can click through.";
    })
    .WithStdioServerTransport()
    .WithTools<OvermindTools>()
    .WithTools<OvermindPlanningTools>()
    .WithTools<OvermindOnboardingTools>()
    .WithTools<OvermindNotificationTools>()
    .WithResources<OvermindResources>()
    .WithResources<OvermindPlanningResources>()
    .WithPrompts<SessionBootstrapPrompt>();

await builder.Build().RunAsync();
return 0;
