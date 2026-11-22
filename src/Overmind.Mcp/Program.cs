using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Overmind.Mcp;
using Overmind.Mcp.Planning;
using Overmind.Mcp.Registration;
using Overmind.Mcp.ToolBinding;

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

if (!McpProfileResolver.TryResolve(args, out var profile, out var profileError))
{
    Console.Error.WriteLine(profileError);
    return 2;
}

Console.Error.WriteLine($"[overmind-mcp] MCP profile: {McpProfileResolver.Describe(profile)}");

var baseUrl = ApiBaseUrl.Resolve();
Console.Error.WriteLine($"[overmind-mcp] overmind API base URL: {baseUrl}");

var http = OvermindApiClient.CreateHttpClient(baseUrl);

if (!await new HostReadinessGate(http, Console.Error)
        .WaitUntilReadyAsync(
            HostReadinessGate.DefaultBudget,
            HostReadinessGate.DefaultInterval,
            CancellationToken.None))
{
    Console.Error.WriteLine(
        $"[overmind-mcp] FATAL: overmind API at {baseUrl} is unreachable "
        + $"after {HostReadinessGate.DefaultBudget.TotalSeconds:0}s of retries.");
    Console.Error.WriteLine("[overmind-mcp] start the overmind host first or point OVERMIND_API_URL at a running instance.");
    return 2;
}

var workspaceIdentity = new WorkspaceIdentityFile();
var identityFile = new SessionIdentityFileWriter(new ParentProcessIdSource(), workspaceIdentity: workspaceIdentity);
var lifecycle = new AgentSessionLifecycle(http);

var prunedIdentityFiles = SessionIdentityFiles.PruneStaleIdentityFiles();
if (prunedIdentityFiles > 0)
{
    Console.Error.WriteLine($"[overmind-mcp] pruned {prunedIdentityFiles} stale identity file(s)");
}

// A reconnect speaks a fresh initialize on a surviving process; the refresher re-registers the
// session, preferring the runtime identity set via session_identity_set (AdapterIdentityState)
// and falling back to OVERMIND_ALIAS/OVERMIND_VENDOR (feature 179 spec D1, slot honoring 181).
// The stamped row resume keeps the session id stable across restarts, so a claim made before the
// process died stays bound to this session; the mutation gate re-adopts it with a board log line
// when the row reads stale (feature 258 T0/T1, superseding 235 D1's alias-string adopt).
var identityRefresher = new SessionIdentityRefresher(lifecycle, identityFile, workspaceIdentity);
Guid adapterSessionId = Guid.Empty;
var alias = AgentIdentity.ResolveAlias();
var vendor = AgentIdentity.ResolveVendor();
var stamp = workspaceIdentity.TryRead();
try
{
    // The workspace stamp names the row this workspace registered last run; passing it lets the
    // host resume that row (identity and session id survive the restart) instead of minting a
    // fresh placeholder row under which held claims strand.
    adapterSessionId = await lifecycle.RegisterAsync(alias, vendor, stamp?.SessionId, CancellationToken.None);
    AdapterSession.Id = adapterSessionId;
    Console.Error.WriteLine($"[overmind-mcp] session {adapterSessionId} registered");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[overmind-mcp] WARN: session registration failed, claims will be refused: {ex.Message}");
}

if (adapterSessionId != Guid.Empty && stamp is { } saved && adapterSessionId == saved.SessionId)
{
    // The host resumed the stamped row and its identity came back with it, so the restore leg has
    // nothing to negotiate here. Setting the slot would be worse than useless: an explicit alias
    // makes later reconnects mint fresh rows instead of resuming this one (review-258-01 B1).
    identityFile.Write(saved.Alias, saved.Vendor, adapterSessionId);
}
else if (adapterSessionId != Guid.Empty)
{
    await identityRefresher.RestoreAndStampAsync(adapterSessionId, alias, vendor, CancellationToken.None);
}

SessionHeartbeat? heartbeat = null;
if (adapterSessionId != Guid.Empty)
{
    var period = HeartbeatPeriod.Resolve();
    heartbeat = new SessionHeartbeat(http, period);
    heartbeat.Start();
    Console.Error.WriteLine($"[overmind-mcp] heartbeat every {period.TotalMinutes:0.#} min");
}

var wakeSlot = new WakeSignalSlot();

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
builder.Services.AddSingleton(identityFile);
builder.Services.AddSingleton(wakeSlot);
builder.Services.AddSingleton<IWakeTicker, PeriodicWakeTicker>();
builder.Services.AddSingleton<IWakeNotifier, McpWakeNotifier>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation { Name = "overmind", Title = "Overmind", Version = AdapterVersion.Current };
        options.ServerInstructions =
            "Overmind shared-context adapter. Call overmind_onboard first — it resolves your project and returns the onboarding quickstart. " +
            "Working a feature? Claim it first with planning_claim_feature — the onboarding quickstart explains the workflow. " +
            "Start of work: overmind_briefing returns the whole guardrail picture in one call — guards and gates with full bodies, decisions and the latest notes as ~200-character snippets, and the tombstone list. " +
            "Gates, guards, notes, decisions, skills and tombstones (graves of deleted entries) live as entries: " +
            "list_entries (type=gate|guard|note|decision|skill|tombstone) explores them and read_entry reads one in full — " +
            "check tombstones before proposing anything that smells like a resurrect; the onboard payload names the active project. " +
            "Use tools for project/entry CRUD and fulltext or semantic search. " +
            "When overmind_notify returns a url, quote the url in chat when asking for an approval or reporting a question so the human can click through.";
        options.Filters ??= new McpServerFilters();
        options.Filters.Message.IncomingFilters.Add(identityRefresher.CreateIncomingFilter());
        options.Filters.Request.CallToolFilters.Add(ToolBindingErrorFilter.Create());
        options.Filters.Request.CallToolFilters.Add(new WakeSlotFilter(wakeSlot).Create());
    })
    .WithStdioServerTransport()
    .WithProfileSurface(profile);

builder.Services.AddHostedService<AgentWakeWatcher>();

var app = builder.Build();
app.Services.GetRequiredService<IHostApplicationLifetime>()
    .ApplicationStopping.Register(() => lifecycle.MarkEnded(AdapterSession.Id));
// Registration callbacks run LIFO: the heartbeat stops before the session end call goes out.
if (heartbeat is not null)
{
    app.Services.GetRequiredService<IHostApplicationLifetime>()
        .ApplicationStopping.Register(heartbeat.Dispose);
}

await app.RunAsync();
return 0;
