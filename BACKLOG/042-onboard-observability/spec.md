---
feature: 042
slug: onboard-observability
status: validated
created: 2026-09-22
validated: 2026-09-22
---

# Spec 042 — Onboard observability: материализация и режим валидации видны в ответе `overmind_onboard` ДО первого вызова

<decisions>
- **D1 — The onboard response carries two resolved blocks: `materialization` and `validation` (agent).** `materialization`: `{strategy: "off"|"on"|"archive" (resolved, never "inherit"), targetDir, trigger, lastRun}` — targetDir is where the materialiser ACTUALLY writes for the resolved strategy: the project's BACKLOG root under its disk path for on, and the archive root (under the host data dir, per PlanningMaterializer's archive layout) for archive; trigger states the factual semantics ("event-driven: runs after every planning mutation" — observed live all session, not a timer); lastRun is the UTC timestamp of the last materialiser pass (null when never). `validation`: `{humanGate: bool}` — the resolved gate. Resolved values only: the agent must not re-derive the inherit cascade.
- **D2 — PlanningPolicy is the single source; onboard calls it, no logic forks (agent).** `ResolveValidationGateAsync` and `ResolveMaterializationAsync` already exist (PlanningPolicy.cs:18-50); the endpoint composes their results. targetDir derives from the resolved strategy and the project's disk path / the materialiser's archive root; lastRun needs one new persisted field (`LastMaterializedAt` on Project, set by the materialiser at the end of each successful SyncAsync) — a single-column migration, the honest price of "how do I know it ran".
- **D3 — The quickstart text explains both blocks in place (agent).** One line after the board summary: materialisation (where it writes, when it runs, how to see the last pass) and the validation mode (auto-validate vs human gate — "planning_validate returns STOP when the human gate is on"). Follows the 032 self-documentation pattern; the MCP tool description for overmind_onboard mentions the two blocks additively.
- **D4 — Off-strategy is explicit, not absent (agent).** When the resolved strategy is off, the block still appears with `strategy: "off"`, `lastRun: null`, trigger noting it will not run — the DMT agent searched blindly precisely because "not configured" was indistinguishable from "broken".
</decisions>

<open-questions>
- Should `lastRun` live on Project (one column, global for the project) or per-feature? Recommendation: Project — one materialiser pass covers the project; per-feature timestamps are granularity nobody asked for.
- Does the web UI settings page need the same resolved view? Recommendation: no — the UI already edits the raw settings; this feature is the agent's window.
</open-questions>

<requirements>
- R1 — `OnboardResponse` carries the two blocks per D1; resolved through PlanningPolicy (D2); present for every strategy including off (D4); targetDir reflects the actual write location for every strategy, archive included.
- R2 — Project gains `LastMaterializedAt` (nullable UTC); the materialiser stamps it on each successful pass; migration + backfill-safe (null for never-run).
- R3 — Quickstart line per D3; `overmind_onboard` MCP description mentions the blocks additively.
- R4 — Tests: onboard response contains both blocks with resolved values (on/off/archive/inherit→default paths); lastRun null before any pass and set after a mutation-triggered one; quickstart line pinned; migration round-trips.
</requirements>

<non-goals>
- Changing materialiser scheduling (it is event-driven by design; this feature only surfaces it).
- Surfacing materialisation drift/errors in onboard (the drift endpoint exists; 033 covers guards).
- The web UI settings page.
- Validation-gate mechanics themselves (043 owns status/provenance).
</non-goals>

<context>
Field sources: FEATURELIST/06 — the DMT agent searched blind for materialisation output (git status, find -newermt, ~/.overmind) because no tool or instruction answered "where, when, did it run"; and could not know the validation mode before calling planning_validate. FEATURELIST/08 — "planning_validate succeeded silently; no way to know which mode except by attempting the call". Code facts: OnboardResponse(projectId, name, created, board, quickstart) at OnboardApi.cs:75; PlanningPolicy.cs:18-50 resolves both values today; materialisation is event-driven (SyncMaterializationAsync after planning mutations — observed live across this session's ceremony moves).
</context>

<log>
- 2026-09-22 — v2 from review 042/01 NIT-1: D1 corrected — targetDir for the archive strategy is the materialiser's archive root (under the data dir), not the repo BACKLOG/; the first cut fixed the repo path for all strategies, which lied for archive — the exact blind-search mode this feature exists to cure. R1/R4 updated (archive path covered).
</log>
