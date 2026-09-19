---
feature: 015
slug: inotify-watch-storm
status: validated
created: 2026-09-19
validated: 2026-09-19
---

# Spec 015 — Bug: host + mcp adapter inotify-watch the whole cwd tree

<decisions>
- Root cause (proven live 2026-09-19): `Host.CreateApplicationBuilder` / `WebApplication.CreateBuilder` default the content root to the process cwd, and the .NET 8+ PhysicalFilesWatcher behind the optional reloadOnChange appsettings providers registers one inotify watch PER DIRECTORY under the whole content-root tree (even though no appsettings.json exists anywhere in the pack layout — it watches for the files appearing). The MCP adapter inherits the agent's cwd (arbitrary project trees: 637–10711 watches live), the packed host inherits the caller's cwd (observed: $HOME → 43312 watches, 2/3 of the machine's 65536 budget, Squeezr tests starved).
- Fix strategy: pin the content root of BOTH binaries to AppContext.BaseDirectory (small, controlled install directory) instead of disabling config reload — reloadOnChange semantics stay intact where config ever appears, and the watch storm disappears by construction. Neither binary ships an appsettings.json today, so behavior is unchanged.
- Host webRoot resolution gets re-based with the pin: OVERMIND_WEB_ROOT stays the primary override (run.sh keeps pinning it); fallbacks in order: `<BaseDirectory>/../web` (pack layout), then a bounded upward walk from BaseDirectory looking for `web/out` (dev `dotnet run` — covers bin/Debug/net10.0 → repo root). First existing wins.
- The `install` branch of the adapter exits before any host builder — untouched.
</decisions>

<open-questions>
- none
</open-questions>

<requirements>
- src/Overmind.Mcp/Program.cs: `Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory })`.
- src/Overmind.Host/Program.cs: `WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory })` + webRoot fallback per the decision above.
- Guard so this never regresses: Linux-only smoke check (build/smoke.sh or a test) that starts the adapter from a deep dummy directory tree and asserts the inotify watch count (via /proc/<pid>/fdinfo) stays below a small threshold (e.g. < 25), and the same for the host started from $HOME-equivalent deep tree.
- Docs: CONTEXT.md/run.sh note that the caller's cwd no longer affects the host; dogfood note that MCP adapters can be spawned from any repo without watch cost.
- No new NuGet packages. No behavior change to config, API, MCP contract.
</requirements>

<acceptance-ideas>
- Adapter started from a 1000-directory tree: inotify watch count ~0–5 (was: 1 per directory, 367 from the repo root, 2955 live for the kimi session).
- Host started from $HOME: watches only its install tree (dozens, not 43312); UI still served (pack layout) and dev `dotnet run --project src/Overmind.Host` still finds web/out.
- Live dogfood: total inotify pressure of the running fleet drops below ~5k (was ~64k/65536 used) — Squeezr test starvation gone without the sysctl bump.
- dotnet build + dotnet test green; smoke guard green on Linux, skipped elsewhere.
</acceptance-ideas>
