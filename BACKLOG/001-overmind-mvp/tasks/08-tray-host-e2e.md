---
feature: 001
task: 08
slug: tray-host-e2e
status: todo
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
6. Configure Claude Code MCP to spawn `Overmind.Mcp` with `OVERMIND_PROJECT_ID`; in a session, read `ctx://projects/<id>/gates` → gate content returned.
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
<!-- Filled by executors during work -->
</obstacles>

<log>
2026-09-06 — writing-plans — task created from approved spec.
</log>
