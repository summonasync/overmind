---
feature: 042
slug: onboard-observability
status: validated
created: 2026-09-22
validated: 2026-09-22
---

# Plan 042 — Onboard observability: материализация и режим валидации видны в ответе `overmind_onboard` ДО первого вызова

<approach>
One task, composition-sized: the endpoint learns two blocks it can already compute, the domain gains one nullable column, the materialiser stamps it, the quickstart explains itself. No new services, no new resolution logic — PlanningPolicy is called, not forked. The MCP layer inherits the blocks through the existing HTTP proxy; only its description grows.

Sequencing note: rides the same no-deploy wave (bundle at the end).
</approach>

<tasks>
- [ ] 01-onboard-blocks — Core/Infrastructure: Project.LastMaterializedAt (nullable UTC) + migration; the materialiser stamps it at the end of each successful SyncAsync pass (PlanningMaterializer — find the single success exit point; failure paths must not stamp). Host: OnboardResponse gains `materialization` {strategy (resolved, never "inherit"), targetDir, trigger, lastRun} and `validation` {humanGate} composed via PlanningPolicy (D2); strategy "off" still emits the block with lastRun null and a trigger line saying it will not run (D4). Mcp: overmind_onboard description mentions both blocks additively. Quickstart: one line after the board summary covering both (D3 wording). Tests: onboard blocks present for on/off/inherit→default paths with resolved values; lastRun null before a pass, set after a mutation-triggered one (integration against a scratch materialisation root); migration round-trip; quickstart + description pins; verify: build + full tests green → tasks/01-onboard-blocks.md
</tasks>

<risks>
- **Stamping from a failure path** would lie to the agent (lastRun set, disk not written). Mitigation: stamp only on the success exit; the test asserts a failed pass (forced drift/clobber abort) leaves lastRun untouched.
- **Quickstart bloat.** The text is read every session by every agent — one line, not a paragraph; the pin-test enforces the line exists, not its length (reviewer judges verbosity).
- **Migration on existing DBs** (prod 4761 runs against ~/.overmind/overmind.db): nullable column, backfill-free by design; verify the app boots against a copy of a real DB shape in the integration test, not only a fresh one.
</risks>

<verification>
- dotnet build + dotnet test green (baseline 1132).
- Pins: blocks for all strategy paths; resolved (not inherit) values; lastRun semantics both ways; quickstart line; MCP description.
- git status: Core/Infrastructure (column + stamp), Host (endpoint + contracts), Mcp (description) — no web, no materialiser scheduling changes.
- Review: light.
</verification>