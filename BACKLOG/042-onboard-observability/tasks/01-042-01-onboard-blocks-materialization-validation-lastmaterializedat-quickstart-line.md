---
feature: 042
task: 01
slug: onboard-observability
status: done
review: light
created: 2026-09-22
brief: Onboard blocks: resolved materialization+validation via PlanningPolicy, LastMaterializedAt stamped on success only, quickstart line, pins
---

# 042/01 — Onboard blocks: materialization + validation, LastMaterializedAt, quickstart line

<goal>
The onboard answer tells the agent, before any other call: whether materialisation is on, where it writes, when it runs and when it last ran; and whether validation is a human gate. Two resolved blocks, one nullable column, one quickstart line.
</goal>

<context>
PlanningPolicy (src/Overmind.Infrastructure/Planning/PlanningPolicy.cs:18-50) already resolves both values today; OnboardResponse (OnboardApi.cs:75) does not carry them. The materialiser is event-driven (SyncMaterializationAsync after planning mutations); its single success exit is the stamp point for the new column. The DMT field report searched blind for exactly this information.

Executor reads ONLY this file.
</context>

<scope>
- Core: Project.LastMaterializedAt (DateTime?, UTC) + EF migration (nullable, no backfill).
- Infrastructure: PlanningMaterializer stamps the column at the end of each successful SyncAsync pass — locate the success exit; failure/abort paths (drift, clobber, exceptions) must NOT stamp.
- Host: OnboardResponse gains two blocks composed via PlanningPolicy (inject it into OnboardApi): materialization {strategy: resolved off|on|archive (never "inherit"), targetDir (project BACKLOG root under DiskPath), trigger (factual: event-driven, after every planning mutation; for off: states it will not run), lastRun (null when never)}; validation {humanGate: bool}. Strategy off still emits the block (D4).
- Mcp: overmind_onboard description mentions the two blocks additively (one sentence).
- Quickstart (OnboardQuickstart.Text): one line after the board summary covering both blocks (where materialisation writes + when it runs + how to see the last pass; validation mode meaning for planning_validate STOP).
- Tests: onboard blocks present for on/off/inherit→default with resolved values; lastRun null before any pass and set after a mutation-triggered one (integration, scratch materialisation root); a forced abort (drift/clobber) leaves lastRun untouched; migration round-trip (boot against a pre-existing DB shape, not only fresh); quickstart line pin; MCP description pin (032 pattern).
</scope>

<anti-scope>
- No materialiser scheduling changes; no drift surfacing in onboard; no web UI.
- No validation-gate mechanics (043).
- No per-feature lastRun.
</anti-scope>

<constraints>
- English strings; no comments (non-obvious only); no new packages; no commits.
- Gates: dotnet build (0W/0E) + dotnet test green (baseline 1132). Known flake PlanningSetStatusDescription_QuotesAllowedStatusesPerKind — rerun in isolation before judging.
</constraints>

<acceptance-criteria>
- Onboard carries both blocks on every strategy path; values resolved (never inherit).
- lastRun honest both ways (set on success, untouched on abort).
- Quickstart line + MCP description pinned; migration round-trips.
- git status: Core/Infrastructure/Host/Mcp + tests only.
</acceptance-criteria>

<refs>
- Spec 042 D1-D4, R1-R4; plan risks (failure-path stamping, quickstart bloat, live-DB migration).
- PlanningPolicy.cs:18-50; OnboardApi.cs:75; OnboardQuickstart.Text.
- 032 self-documentation pattern; 033 abort semantics.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-22 — Execution starts: coder takes the onboard-blocks contract (plan gate passed).
- 2026-09-22 — Light review PASS-WITH-NITS (0 BLOCKER / 0 IMPORTANT / 2 NIT, audits/review-042-01.md): stamp honesty proven on every path, resolved-only blocks, genuine pre-existing-DB migration test. Fix round for NIT-1 accepted without a second review pass — spec v2 + independent-path test + green gates cover it: PlanningArchive.Root is the single layout truth (materialiser delegates, onboard resolves per strategy), 1144/1144. NIT-2 (clobber-abort pin) → microbatch #42.
- 2026-09-22 — Accepted — onboard observability complete: resolved blocks for every strategy (archive targetDir honest via the shared PlanningArchive.Root), honest LastMaterializedAt, quickstart line, spec v2. Feature 042 assembled.
</log>
