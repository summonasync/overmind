---
feature: 015
task: 01
slug: inotify-watch-storm
status: done
review: light
created: 2026-09-19
---

# 015/01 — Content root pin (host + adapter)

<goal>
Both binaries stop inheriting the caller's cwd as content root: the MCP adapter and the Host run with ContentRootPath = AppContext.BaseDirectory, and the Host still finds its web UI in pack layout and in dev `dotnet run`. inotify watch count per process drops from "one per cwd-tree directory" to a handful.
</goal>

<context>
Feature 015 (spec validated 2026-09-19). Proven live: .NET 8+ PhysicalFilesWatcher behind the default reloadOnChange appsettings providers registers one inotify watch per directory under the whole content-root tree. Adapter inherits the agent's cwd (637–10711 watches), host inherits the caller's cwd ($HOME → 43312). No appsettings.json exists in src/ or in the pack layout (verified), so the pin changes no actual config loading. The executing subagent reads ONLY this file.
</context>

<scope>
- src/Overmind.Mcp/Program.cs (~line 44): `Host.CreateApplicationBuilder(args)` → `Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory })`. The install branch above it returns early — untouched.
- src/Overmind.Host/Program.cs: `WebApplication.CreateBuilder(args)` (find the exact builder call near the top of the file) → `WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory })`.
- src/Overmind.Host/Program.cs webRoot resolution (~lines 84-87): today `OVERMIND_WEB_ROOT` env wins, else `Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "web", "out"))`. Re-base the fallbacks: (1) OVERMIND_WEB_ROOT if set; (2) `<AppContext.BaseDirectory>/../web` normalized (pack layout: host/../web); (3) bounded upward walk from AppContext.BaseDirectory (max 8 levels, stop at filesystem root) looking for `web/out` (dev: bin/Debug/net10.0 → repo root = 5 levels). First candidate that Directory.Exists wins; if none exists, keep the current behavior of logging the warning and not serving the UI.
- Local proof (do it, record numbers in the task <log>): build, then run the adapter binary from a generated 300-directory dummy tree and count inotify watches via /proc/<pid>/fdinfo (grep -c '^inotify' on inotify fds) — expect ≤ ~5 (was: one per directory). Also run the published host from $HOME, curl /api/health, count watches — expect dozens (install tree), not tens of thousands. Kill both after measuring.
</scope>

<anti-scope>
- No smoke-script/guard work (task 02), no docs, no pack/deploy (task 03)
- No changes to config keys, API, MCP contract, DI registrations
- Do NOT add an appsettings.json
</anti-scope>

<constraints>
- .NET 10 / C# 14; no new NuGet packages; no comments in code
- `dotnet build Overmind.sln` then `dotnet test Overmind.sln --no-build` must stay green (677 baseline). Known flake: Internal CLR error on cold dotnet → dotnet build-server shutdown, retry
- The live host on :4761 is running the packed 0.1.4 install — do not kill it; measure YOUR OWN published binaries (dotnet publish or dotnet run) on throwaway ports/pids
</constraints>

<acceptance-criteria>
- [ ] Adapter ContentRootPath pinned; install branch unchanged
- [ ] Host pinned; webRoot found in pack layout (BaseDirectory/../web) and in dev dotnet run (upward walk)
- [ ] Watch counts measured and logged: adapter from 300-dir tree ≤ ~5; host from $HOME = install-tree scale
- [ ] UI actually served by the pinned host (curl / returns HTML) in dev layout
- [ ] dotnet build + dotnet test green
</acceptance-criteria>

<refs>
- @src/Overmind.Mcp/Program.cs — builder call at ~line 44
- @src/Overmind.Host/Program.cs — builder call + webRoot block at ~lines 84-87
- tasks/02-watch-guard-smoke.md — depends on this task's binaries
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-19 — Created from validated plan 015
- 2026-09-19 — Started: content root pin (coder dispatched)
- 2026-09-19 — Coder done: pins + ResolveWebRoot; adapter 5 watches from 301-dir tree, host 61 from $HOME (was 43332); 677/677 green; light review dispatched
- 2026-09-19 — Light review PASS-WITH-NITS (0 blocker/important, 3 nit), audits/review-015-01.md; reviewer reproduced all measurements
</log>
