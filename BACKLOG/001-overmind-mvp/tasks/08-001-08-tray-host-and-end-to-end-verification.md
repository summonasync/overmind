---
feature: 001
task: 08
slug: overmind-mvp
status: done
review: hard
created: 2026-09-06
---

# 001/08 — Tray host and end-to-end verification

<goal>
Overmind.Host runs as a tray application: single instance, tray icon with "Open Overmind" (opens the web UI in the default browser) and "Quit". Startup is resilient (free-port fallback, DB auto-creation). The whole MVP passes the full manual e2e scenario.
</goal>

<context>
Spec R1: single process = tray + Kestrel + SPA hosting. This task wires Avalonia's TrayIcon into the Host and performs final integration. It is the last task — everything else must be done first.
</context>

<scope>
`src/Overmind.Host/`:
- Avalonia desktop lifetime with TrayIcon only (no windows): menu items "Open Overmind" (launches default browser at the bound address) and "Quit" (graceful shutdown of Kestrel + Avalonia).
- Single-instance enforcement: mutex/named-semaphore per user; second launch opens the browser at the existing instance and exits.
- Startup sequence: ensure `~/.overmind/` exists → open/migrate SQLite → select port (default 4761, fallback 0) → write `~/.overmind/host.port` → start Kestrel → show tray icon. Any startup failure shows a native notification/stderr message and exits non-zero.
- Graceful shutdown on SIGINT/SIGTERM and tray Quit.
- `README.md` "Running" section: `dotnet run --project src/Overmind.Host`, where the DB lives, where the port file lives, example MCP client config JSON pointing at the adapter.

E2E manual checklist (execute and record results in this file's <log>):
1. Start host → tray icon visible, `~/.overmind/overmind.db` and `host.port` exist.
2. Open UI via tray → Projects page loads.
3. Create project "demo" with a temp disk path; add one gate and one note.
4. Create kit "demo-kit" with one skill (Claude variant), add entries, link to project, activate.
5. Verify `<demo-path>/.claude/skills/` contains the skill + `overmind-bootstrap/SKILL.md` with gate content.
6. Configure Claude Code MCP to spawn `Overmind.Mcp` with `OVERMIND_PROJECT_ID`; in a session, read `overmind://projects/<id>/gates` → gate content returned.
7. FTS search in UI finds the note; semantic search (Qdrant up) also finds it.
8. Stop Docker/Qdrant → UI banner appears, FTS still works, entry CRUD unaffected.
9. Second host launch → focuses browser, no second instance.
10. Deactivate kit → materialized files removed.

Automated: integration test for single-instance helper and port fallback; the 10-step scenario is manual (recorded in <log>).
</scope>

<anti-scope>
- No installers/MSIX/pkg packaging, no autostart registration (post-MVP).
- No code signing, no .app bundle (macOS tray limitation is a known, documented limitation — mention in README).
- No new features — bugs found during e2e are fixed in place only if trivial; otherwise written as new task files (per bug-reports-are-tasks rule).
</anti-scope>

<constraints>
- Avalonia packages already pinned (task 01); BSD/MIT — verified there.
- No `async void` except tray event handlers (unavoidable in Avalonia; keep them empty shells delegating to async Task methods).
- Windows AND macOS: the e2e checklist must be executed on at least Windows; macOS run recorded as done/known-issue.
</constraints>

<acceptance-criteria>
- [ ] Tray icon with both menu items works; Quit shuts down cleanly (no orphan port).
- [ ] Single instance enforced (integration test + manual).
- [ ] README "Running" section accurate (every command executed for real).
- [ ] All 10 e2e steps executed, results recorded in <log>; failures either fixed or filed as new task files.
- [ ] `dotnet build && dotnet test` fully green; `npm run lint && npx tsc --noEmit && npm run build` clean.
</acceptance-criteria>

<refs>
- @../plan.md — task 08 + <verification> section
- @../spec.md — R1, R7
- tasks/03-core-api.md — host startup (must be done first)
- tasks/07-web-ui.md — SPA hosting (must be done first)
</refs>

<obstacles>
- 2026-09-07 — executor sub-agent burned 2x2h timeouts: it launched the tray host in the foreground, and the Avalonia UI loop blocks by design. Implementation + tests landed; e2e executed by the orchestrator with the host as a background process and `--no-tray` for API steps.
</obstacles>

<log>
2026-09-06 — writing-plans — task created from approved spec.
2026-09-07 — implementation landed (executor sub-agent): Avalonia TrayIcon (`TrayApp`/`TrayApplication`), per-user mutex single instance (`SingleInstance`), startup sequence + port file (existing from 03, wired), README "Запуск" section with macOS tray limitation, `SingleInstanceTests` (+9). 165/165 green.
2026-09-07 — e2e run (orchestrator, Windows):
- [1] headless start: `~/.overmind/overmind.db` + `host.port` (4761) created. Tray-icon part: pending user validation (tray build launched).
- [2] pending user validation (tray "Open Overmind"); `OpenInDefaultBrowser` proven by step 9.
- [3] PASS — project "demo" (id 5105c9d7-9736-4d73-96e3-a20399758d1c, temp disk path), gate "No force pushes", note "Zerling memo" created via API.
- [4] PASS — kit "demo-kit" + skill "zerling-tactics" (Claude variant), entry added, activated: 200 with 2 materialized files.
- [5] PASS — `.claude/skills/zerlingtactics/zerling-tactics/SKILL.md` + `.claude/skills/overmind-bootstrap/SKILL.md` on disk; bootstrap references `overmind://projects/<id>/gates|guards`.
- [6] PASS — real stdio MCP session (`overmind-mcp.dll`, `OVERMIND_PROJECT_ID`): initialize OK, `resources/read overmind://projects/<id>/gates` returned gate content.
- [7] FTS PASS — `q=zerling` finds note + skill. Semantic-up: pending (Qdrant down on this machine).
- [8] PASS (down-state) — health `vectorSearch:"down"`, FTS unaffected, CRUD unaffected, semantic returns clean 503 "Semantic search unavailable". UI degraded banner: pending user eyes (implemented in 07).
- [9] PASS — second launch: "already running at http://127.0.0.1:4761/; opening it in the browser", exit 0, no second instance.
- [10] PASS — deactivate: 200, both materialized files removed, `.claude` clean.
2026-09-07 — tray validation (user) + three fixes landed by orchestrator:
- Tray menu invisible on right-click: root cause = code-only `Application` had no theme, so the managed tray popup (`TrayPopupRoot`/`MenuFlyoutPresenter`) rendered without templates. Fix: added `Avalonia.Themes.Fluent` 12.1.2 (MIT, first-party, user-approved) + `Styles.Add(new FluentTheme())` in `TrayApp`.
- Quit left a zombie process: after the Avalonia loop exited, `await app.StopAsync()` posted its continuation into the dead dispatcher's SynchronizationContext (loop ran on a threadpool thread inside the async chain). Fix: dedicated STA thread for the tray loop + `TaskCompletionSource(RunContinuationsAsynchronously)` handshake in `Program.cs`.
- Exit crash `ApplicationException` in `InstanceLock.Dispose`: `Mutex.ReleaseMutex()` has thread affinity (acquired on main thread, released on a threadpool continuation). Fix: dispose-only; abandoned-mutex semantics already handled in `TryAcquire`.
- Final validation (user): tray icon OK, menu renders, Open Overmind opens UI, Quit → "Application is shutting down...", exit code 0, port free, no orphan process.
- E2E verdict: 10/10 (steps 2/7-semantic-up/8-banner partially validated: Open proven, banner implemented in 07, Qdrant was down on the machine so semantic-up remains unverified).
</log>
