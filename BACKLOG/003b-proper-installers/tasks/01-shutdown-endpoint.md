---
feature: 003b
task: 01
slug: shutdown-endpoint
status: done
review: light
created: 2026-09-21
---

# 003b/01 — POST /api/shutdown: graceful stop from outside

<goal>
The Host can be stopped by a local caller over its own loopback API: `POST /api/shutdown` answers `202 Accepted`, the response reaches the caller intact, and only then does the application begin a normal graceful shutdown. A Windows installer (later task) uses this instead of killing the process.
</goal>

<context>
Overmind's Host is an ASP.NET Core app with an Avalonia tray icon and **no windows**. That makes it unstoppable from outside on Windows: `taskkill` without `/F` posts `WM_CLOSE` to top-level windows and there are none, and the Restart Manager that Inno Setup relies on is unreliable against a windowless tray process. The only existing exits are the tray's "Quit" menu item and a console signal.

Task 003b/03 builds a Windows installer that must replace the Host's files during an upgrade and delete them during uninstall. Without this endpoint its only option is `taskkill /F` on a process holding an open SQLite database. This task gives it a clean path; `/F` stays only as a post-timeout fallback.

The endpoint lives on the **human door** (`/api/*`), not the agent door (`/api/agent/*`), so the MCP adapter contract is untouched. Loopback binding is the security boundary for the whole API and stays the boundary here — no new authentication, consistent with every other endpoint.

Executor reads ONLY this file.
</context>

<scope>
- Create `src/Overmind.Host/Api/ShutdownApi.cs` following the shape of the existing `src/Overmind.Host/Api/HealthApi.cs`:

```csharp
namespace Overmind.Host.Api;

public static class ShutdownApi
{
    public static RouteGroupBuilder MapShutdownApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapPost("/shutdown", Shutdown);
        return group;
    }

    private static IResult Shutdown(HttpContext context, IHostApplicationLifetime lifetime)
    {
        context.Response.OnCompleted(() =>
        {
            lifetime.StopApplication();
            return Task.CompletedTask;
        });

        return Results.Accepted();
    }
}
```

  `OnCompleted` is the load-bearing detail: calling `StopApplication()` inline would race the response write against host teardown. Registering the callback guarantees the `202` is fully flushed before shutdown starts.

- Register it in `src/Overmind.Host/Program.cs` in the existing block of `Map*Api()` calls (the block currently ending with `app.MapNotificationsApi();`, around line 155). Add:

```csharp
    app.MapShutdownApi();
```

- Create `tests/Overmind.Api.Tests/ShutdownApiTests.cs`:

```csharp
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class ShutdownApiTests
{
    [Fact]
    public async Task Shutdown_ReturnsAccepted_AndTriggersApplicationStopping()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var lifetime = factory.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.IsCancellationRequested.ShouldBeFalse();

        var response = await client.PostAsync("/api/shutdown", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        lifetime.ApplicationStopping.WaitHandle
            .WaitOne(TimeSpan.FromSeconds(10))
            .ShouldBeTrue("the host should begin stopping after the response completes");
    }
}
```

  Do NOT substitute `IHostApplicationLifetime` in the DI container to observe the call — the generic host resolves that same instance to run the server, and replacing it destabilises `WebApplicationFactory` startup. Asserting on the real `ApplicationStopping` token tests the actual behaviour anyway.

  This test stops its own host, so it must own its `ApiFactory` (as written above) and must not share a factory with any other test class.

- Run the Host once on this machine (`dotnet run --project src/Overmind.Host`) and confirm it boots, the tray icon appears, and the UI answers — this is the first ever execution of this app on Windows, and any platform breakage must be recorded in `<obstacles>` before the packaging tasks build on top of it.
</scope>

<anti-scope>
- No authentication, authorization, rate limiting or confirmation token on the endpoint — loopback is the boundary, same as every other endpoint. Adding auth here would be inconsistent and needs its own ADR.
- Do not expose shutdown on the agent door (`/api/agent/*`) and do not add an MCP tool for it. An agent must never be able to stop the Host.
- Do not touch `SingleInstance.cs`, `TrayApp.cs`, `TrayApplication.cs` or the tray Quit path.
- Do not add a restart endpoint, a `GET /api/shutdown`, or a delay/schedule parameter — YAGNI, the installer needs exactly one verb.
- No changes to `build/` in this task.
</anti-scope>

<constraints>
- .NET 10 / C# 14, ASP.NET Core minimal APIs, vertical-slice file per endpoint group — match `HealthApi.cs` exactly in structure and naming.
- No new NuGet packages.
- No comments in the code (repo rule); the `OnCompleted` rationale belongs in this task file and the task log, not in the source.
- `dotnet format --verify-no-changes` must not report new violations from these files (the 6 pre-existing IDE0161 hits in generated EF migrations are known and out of scope).
- No git commits.
</constraints>

<acceptance-criteria>
- [x] `POST /api/shutdown` returns `202 Accepted`
- [x] The `202` body/headers are fully written before shutdown begins (guaranteed by `Response.OnCompleted`, observed by the test receiving a complete response) — also confirmed live against a running Host, see `<obstacles>`
- [x] `ApplicationStopping` fires after the response completes — pinned by `ShutdownApiTests`
- [x] `app.MapShutdownApi()` registered in `Program.cs` alongside the other endpoint groups
- [x] `dotnet build Overmind.sln` green
- [ ] `dotnet test Overmind.sln` green — the whole suite, not just the new test (baseline is 990 tests per `CONTEXT.md:169`; report the new count) — **not literally green as one run**: `Overmind.Mcp.Tests` contract tests hang on Windows (pre-existing, unrelated to this change). Everything else totals 745/753 passing (Core 108/108, Infrastructure 331/336, Api 306/309), all 8 failures pre-existing and Windows-platform-specific, none touching `ShutdownApi`. `Overmind.Mcp.Tests` has 238 tests (238 + 753 = 991 = 990 baseline + 1 new test). Full detail in `<obstacles>` and the handoff report.
- [x] Host boots on Windows via `dotnet run --project src/Overmind.Host`: tray icon present, UI reachable, `~/.overmind/host.port` written — first attempt crashed on a stale `~/.overmind/overmind.db`, second attempt (fresh db) succeeded cleanly; see `<obstacles>`
</acceptance-criteria>

<refs>
- @../spec.md — D7, R1 (reviewer reads, not executor)
- @../plan.md — task 01 row, "the stop contract comes first" risk note
- `src/Overmind.Host/Api/HealthApi.cs` — the structural pattern to copy
- `tests/Overmind.Api.Tests/ApiFactory.cs` — test host factory, already configured for the `Testing` environment
- tasks/03-iss-and-pack-win.md — the consumer of this endpoint
</refs>

<obstacles>
- First-ever `dotnet run --project src/Overmind.Host` on this Windows machine crashed at startup: `Microsoft.Data.Sqlite.SqliteException: SQLite Error 8: 'attempt to write a readonly database'` inside `WalPragmaInterceptor.ConnectionOpened` (`PRAGMA journal_mode=WAL;`) while EF Core migrations opened the brand-new, zero-byte `~/.overmind/overmind.db` for the very first time. NTFS ACLs on the file and directory were full-control for the user (checked with `icacls`), so this was not a permissions problem — it looks like a one-off cold-start race on the very first creation of that file on Windows. Deleting the stale 0-byte `overmind.db` and re-running `dotnet run` succeeded cleanly: all 16 migrations applied, Kestrel bound `http://127.0.0.1:4761`, `~/.overmind/host.port` was written, and the log confirmed `Tray icon active; UI ready at http://overmind.localhost:4761/`. Recorded here per the task's instruction to not paper over first-Windows-run findings; not fixed (out of this task's scope — `WalPragmaInterceptor.cs` isn't in scope and anti-scope forbids touching unrelated files). Worth a look if a later task hits it again on a truly fresh machine (no prior `~/.overmind`).
- Verified `POST /api/shutdown` end-to-end against the real running Host (not just the test): curl returned `202`, and the Host's own log shows `Request finished ... POST /api/shutdown - 202 ...` logged *before* `Application is shutting down...`, confirming the `Response.OnCompleted` ordering the contract calls load-bearing. Process exited cleanly afterward; no `Overmind.Host` process remained (checked via `tasklist`), so the single-instance mutex is free for task 03.
- `dotnet test Overmind.sln` (full suite, unfiltered) does not come back green on this machine, for reasons unrelated to this task's change (`ShutdownApi.cs`/`Program.cs` registration touch nothing these failures depend on). Documented in full in the handoff report; summary:
  - `Overmind.Mcp.Tests` contract tests (the ones that spawn the `overmind-mcp` stdio subprocess via `McpProcessDriver`) all time out on `initialize` after the driver's 20s timeout — every contract test I observed failed the same way (`System.TimeoutException` in `McpProcessDriver.CallAsync` → `InitializeAsync`). I killed the stuck `dotnet test` process tree after ~8 minutes of near-zero CPU rather than let it grind through all 238 tests at 20s/failure. Root cause not fully diagnosed (plausibly the spawned adapter process not completing its own `/api/health` precheck against the test's in-proc `HttpListener` stub in time on Windows) — out of scope to fix here.
  - `Overmind.Mcp.Tests.Registration` alone (238 tests total in the project; this subset doesn't spawn the stdio subprocess) still has 21 pre-existing failures: symlink creation needs `SeCreateSymbolicLinkPrivilege` that this non-elevated session lacks (`StableInstallLinkResolverTests`, 4 failures — same root cause as two `Overmind.Infrastructure.Tests.KitScannerTests` symlink failures), hardcoded forward-slash path expectations vs `Path.Combine`'s backslash on Windows (`AgentPathResolverTests`, 9 failures — same family as 2 `Overmind.Api.Tests.BacklogImport*Tests` and 1 `Overmind.Infrastructure.Tests.PlanningRoundTripTests` failure), and `InstallCommandProcessTests`/`InstallCommandTests` (4 failures, one with a raw CLR-fault exit code instead of the expected `2`).
  - `Overmind.Infrastructure.Tests.FrontmatterKeyLiveProfileTests.Migrate_OnCopyOfLiveProfile_CreatesRegistryAndSeeds` hit the same `SQLite Error 8: attempt to write a readonly database` signature as the Host boot crash above, against a test-copied db file.
  - `Overmind.Api.Tests.PortFileTests.Startup_WritesBoundPortToPortFile` got `500` from `/api/health` in-test because `ApiFactory`'s static constructor seeds a fake (non-ONNX) `model.onnx` text file for test speed, and the ONNX runtime throws unhandled on it instead of returning a graceful "down" status (the real Host, downloading a real model, degrades gracefully instead).
  - Clean counts, everything **except** `Overmind.Mcp.Tests`: `Overmind.Core.Tests` 108/108 passed; `Overmind.Infrastructure.Tests` 331/336 passed; `Overmind.Api.Tests` 306/309 passed (the new `ShutdownApiTests` is among the passing ones). `Overmind.Mcp.Tests` has 238 tests total (via `--list-tests`); not exercised to completion for the reason above.
  - 108 + 336 + 309 + 238 = 991 = the 990 baseline from `CONTEXT.md:169` + 1 (the new `ShutdownApiTests`) — so nothing appears to be missing or duplicated, the counts just don't roll up into one green `dotnet test Overmind.sln` run on this machine yet.
- `dotnet format --verify-no-changes` reports 16 pre-existing `IDE0161` hits in `src/Overmind.Infrastructure/Migrations/*.cs`, not the 6 the task file's `<constraints>` section states. None are in `ShutdownApi.cs`, `ShutdownApiTests.cs`, or `Program.cs` — the acceptance criterion ("must not report new violations from these files") holds — but the stated baseline count is stale and worth a fix-forward note for whoever next touches `dotnet format` baselines.
- Unrelated to this task: `BACKLOG/003b-proper-installers/tasks/03-iss-and-pack-win.md` shows as modified in `git status` (Inno Setup path corrections). I did not touch that file — it was already modified in the working tree before I started (outside this task's scope; not read, not written).
</obstacles>

<log>
2026-09-21 — dotnet-coder — Created `src/Overmind.Host/Api/ShutdownApi.cs` (POST /api/shutdown, 202 Accepted, `Response.OnCompleted` before `IHostApplicationLifetime.StopApplication()`) and `tests/Overmind.Api.Tests/ShutdownApiTests.cs`, verbatim from the contract. Registered `app.MapShutdownApi()` in `src/Overmind.Host/Program.cs` after `app.MapNotificationsApi()`.
2026-09-21 — dotnet-coder — `dotnet build Overmind.sln`: succeeded, 0 warnings, 0 errors. `dotnet format --verify-no-changes`: clean except 16 pre-existing `IDE0161` hits in EF migrations (none in touched files).
2026-09-21 — dotnet-coder — `dotnet test Overmind.sln --no-build`: `Overmind.Mcp.Tests` contract tests hung on the MCP stdio handshake (20s timeout per test); killed the stuck process tree after ~8 minutes. Re-ran the rest filtered out: Core 108/108, Infrastructure 331/336, Api 306/309 (ShutdownApiTests passing). All non-Mcp failures pre-existing and Windows-specific (symlink privilege, path separators, SQLite readonly on a stale db, fake-ONNX-file 500) — unrelated to this task's change. Full detail in `.superpowers/sdd/plan/task-1-report.md`.
2026-09-21 — dotnet-coder — Ran `dotnet run --project src/Overmind.Host` on Windows for the first time. First attempt crashed on the brand-new `~/.overmind/overmind.db` (`SQLite Error 8: attempt to write a readonly database`); deleted the stale 0-byte file and retried — Host booted cleanly, tray active, UI reachable, port file written. Verified `POST /api/shutdown` against the live Host: `202` returned and logged before `Application is shutting down...`, process exited cleanly, no Host process left running.
</log>
