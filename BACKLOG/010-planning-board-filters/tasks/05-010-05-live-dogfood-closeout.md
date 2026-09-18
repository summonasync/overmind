---
feature: 010
task: 05
slug: planning-board-filters
status: done
review: light
created: 2026-09-18
---

# 010/05 — Live dogfood + closeout

<goal>
Feature 010 is proven live on the dogfood profile (pin via MCP → visible in the UI; filters work), BACKLOG materializes clean, the result artifact is written, and the feature is closed.
</goal>

<context>
Tasks 010/01-04 are done and reviewed. This is the ceremony closeout run by the orchestrator-style agent against the real installed Overmind. Installed layout: ~/apps/overmind → symlink to the versioned dir; host bound at http://127.0.0.1:4761; project id ff313f29-8144-488c-94e6-5ea5dde57bfd. The executing agent reads ONLY this file.
</context>

<scope>
- Full verification: dotnet build Overmind.sln && dotnet test Overmind.sln; in web/: npm run lint && npx tsc --noEmit && npm run build
- Rebuild + reinstall the app (bash build/pack.sh — note: run via `bash`, the +x bit is flaky) into ~/apps/overmind-<version>, flip the ~/apps/overmind symlink, snapshot ~/.overmind before first boot, restart the host (headless --no-tray is fine)
- Live scenario:
  1. `planning_set_current` on 009-mcp-registration via the MCP adapter (or the /tmp/omcp.py helper pattern if the session lacks native MCP tools)
  2. UI check: 009 card accented + first; board filters compose; reload keeps filter params
  3. Unpin via the UI pin button
- Materialize planning for the Overmind project; confirm BACKLOG/010-planning-board-filters/ has no stray duplicate files (git status clean-ish, only expected paths)
- Write the result artifact (kind=result) summarizing what shipped, live evidence, and deferred nits; set it validated; move the feature to done via the human door PUT /api/projects/{id}/planning/features/{slug} {status:"done"}
</scope>

<anti-scope>
- No new code changes beyond a trivial fix if the live scenario exposes a blocker (anything bigger → new task file, do not widen scope)
- No git commit/push unless the user explicitly asks in the session
</anti-scope>

<constraints>
- Upgrade ritual: unpack next to the old version → `ln -sfn` the symlink → restart → migrations auto-apply at boot
- pack.sh bit: invoke as `bash build/pack.sh ...`
</constraints>

<acceptance-criteria>
- [ ] All builds/tests green
- [ ] Live pin-via-MCP visible in UI; unpin via UI works
- [ ] Materialization clean, no duplicate task files
- [ ] result artifact validated; feature status done
</acceptance-criteria>

<refs>
- @../plan.md — verification section
- tasks/01..04 — implementation tasks (done)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-18 — Started: live dogfood + closeout
- 2026-09-18 — Closeout done: 0.1.2 packed/installed/live, pin-via-MCP verified, invariant holds, materialize clean, result validated
</log>
