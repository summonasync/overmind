---
feature: 011
task: 04
slug: side-menu-rework
status: done
review: light
created: 2026-09-19
---

# 011/04 — Live dogfood closeout

<goal>
Feature 011 is verified live on the installed host: full gates green, the rebuilt web UI deployed to the live install, the whole navigation scenario clicked through, BACKLOG materialized, result artifact written, feature closed.
</goal>

<context>
Feature 011, final task. Tasks 01-03 delivered the /dashboard route + rebrand, project tabs + sidebar cut, and the InfoPanel contract. This task proves it end-to-end on the live dogfood install and closes the ceremony. The executing agent reads ONLY this file.

Environment: repo /mnt/Ubuntu-Dev/Projects/AI-Tools/Overmind. Live install ~/apps/overmind (symlink → overmind-0.1.3-linux-x64), host runs headless via `bash ~/apps/overmind/run.sh --no-tray`, health http://127.0.0.1:4761/api/health. Web-only deploy ritual: `rsync -a --delete web/out/ ~/apps/overmind/web/` (backend untouched, no host restart needed for static files). Overmind project id in the live DB: ff313f29-8144-488c-94e6-5ea5dde57bfd. MCP helper /tmp/omcp.py (usage: python3 /tmp/omcp.py <tool> '<json>'; recreate from the obstacles section of this task if missing). Planning materialization: POST /api/projects/{id}/planning/materialize {"force":false} — BACKLOG/ must never be hand-edited, it is materialized. Human door for feature status: PUT /api/projects/{id}/planning/features/{slug} with {"status":"done"}.
</context>

<scope>
- Full gates: `dotnet build Overmind.sln && dotnet test Overmind.sln` (untouched backend — must stay green, baseline 677/677); in web/: `npm run lint && npx tsc --noEmit && npm run build`
- Deploy: rsync web/out to the live install, hard-reload the UI
- Live click-through (http://overmind.localhost:4761 or 127.0.0.1:4761):
  - sidebar shows exactly Projects/Kits/Search/Settings; mobile nav mirrors it
  - /entries and /dashboard show the tab bar, correct active tab, project preserved across tab switches
  - /planning?project=<id>&feature=<slug>&tab=spec lands on /dashboard with params intact
  - /entries right panel shows Entries Overview; /dashboard without project shows select-a-project with NO stats panel; all section labels read "Dashboard"
- Materialize BACKLOG via the API (force=false), verify BACKLOG/011-side-menu-rework/ files are clean (no stray duplicates)
- Write the result artifact (kind=result) summarizing what shipped + live evidence, set feature status done, unpin current if still pinned (planning_set_current isCurrent=false)
- Commit ritual: stage and commit the feature's changes with a conventional message (user delegated commits; do NOT push unless the remote is verified alive — remote http://192.168.1.69:3000/Bob-home-ai/agents-overmind.git was flaky 503 earlier; if push fails, report and leave local)
</scope>

<anti-scope>
- No new code changes beyond closeout fixes for what the live click-through proves broken (anything bigger than a trivial fix goes back as a new task, not silent scope)
- No backend changes, no version bump / pack (web-only deploy)
- No cleanup of old installs/snapshots
</anti-scope>

<constraints>
- Known dotnet flake: Internal CLR error 0x80131506 on cold invocations → dotnet build-server shutdown, retry
- pkill -f Overmind.Host returns a bogus exit code — verify host state via curl, not exit codes
- planning_set_status requires a logNote for every status move
</constraints>

<acceptance-criteria>
- [ ] dotnet build + test green; npm lint + tsc + build green
- [ ] Live UI click-through passes every scenario above (evidence in the result artifact)
- [ ] BACKLOG/011-side-menu-rework/ materialized clean
- [ ] result artifact validated, feature status done, current unpinned
- [ ] Changes committed; push attempted and reported
</acceptance-criteria>

<refs>
- tasks/01-dashboard-route-rebrand.md, tasks/02-project-tabs-shell.md, tasks/03-info-panel-contract.md — the shipped work under test
- @BACKLOG.md — index entry flips to [x] via materialization
</refs>

<obstacles>
- /tmp/omcp.py may have been cleaned: recreate as a python3 stdio JSON-RPC client that POSTs initialize → notifications/initialized → tools/call to /home/johan-boolman/apps/overmind/mcp/overmind-mcp and prints text content blocks
</obstacles>

<log>
- 2026-09-19 — Created from validated plan 011
- 2026-09-19 — Started: live dogfood closeout (agent dispatched)
- 2026-09-19 — 2026-09-19 — Live dogfood closeout complete: gates green (dotnet build 0 err; tests 677/677; lint 0 err/6 pre-existing warnings; tsc clean; static build ok), web/out rsynced to live install ~/apps/overmind/web, full click-through verified in headless Chrome on the live host (127.0.0.1:4761): sidebar exactly Projects/Kits/Search/Settings with mobile nav mirror; tab bar on /entries + /dashboard with correct active state and project preserved in tab hrefs; /planning?...&tab=spec lands on /dashboard with params intact (feature 011 spec workspace renders); /entries right panel Entries Overview with stats; /dashboard without project shows select-a-project with no stats panel; section labels read Dashboard. BACKLOG materialized clean.
</log>
