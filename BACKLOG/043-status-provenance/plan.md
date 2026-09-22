---
feature: 043
slug: status-provenance
status: validated
created: 2026-09-22
validated: 2026-09-22
---

# Plan 043 — Status provenance: источник каждого перехода в логе (ui/agent/auto) + предсказуемые и документированные автопереходы

<approach>
Two tasks: first the marker format and the sourcing of every mutation path (Core parser + the surveyed call sites), then the documentation pins that turn the three field mysteries into named rules. The survey in the spec is the map — the executor verifies each site as it lands, not by memory.

Sequencing: last feature of the current p1 wave; no deploy (bundle at the end).
</approach>

<tasks>
- [ ] 01-sourced-log-lines — Core: PlanningLog.Append gains a source parameter emitting `- YYYY-MM-DD [source] — text` (marker after the date, per spec recommendation); Parse reads the marker, legacy lines → source null/unknown; PlanningLogLine carries Source. Infrastructure: every surveyed mutation path writes its line — feature UI update (PlanningFeatureService.cs:87 → service line `status moved to '<status>' via ui`), agent SetStatusAsync (:129 → `[agent]` + note), importer feature restore (:238 → `[import] status restored from imported file`), TransitionOnArtifactCreate (:245 → `[auto] artifact created → inProgress`), TryPromoteToReviewAsync (:254 → `[auto] all tasks done + spec + plan → review`), artifact agent SetStatus, artifact human-door status path (verify whether it takes a note today — if silent, service line `[ui]`; if noted, `[ui]` + note), artifact importer restore (:321 → `[import]`); grep gate: no `SetStatus` call on feature/artifact remains without a sourced line. Tests: parse legacy + marker; each door's line verbatim; auto reasons; grep-clean assertion via review. Verify: build + full tests green → tasks/01-sourced-log-lines.md
- [ ] 02-lifecycle-docs-pins — Quickstart/onboard lifecycle block: the three factual rules per spec D3 (done + new artifact stays done; no plan → no promotion even at 100%; import restores verbatim — imported done ≠ auto-closed) + the source-marker legend (one line: what [agent]/[ui]/[auto]/[import] mean in log lines); materialisation round-trip test (import a materialised file with sourced lines → no corruption/duplication); pins for quickstart text and service-line formats; result artifact on the board. Verify: build + full tests green → tasks/02-lifecycle-docs-pins.md
</tasks>

<risks>
- **Marker drift between append and parse** (one writes `[ui]`, another reads only exact casing) — pin both directions with round-trip tests in task 01 before touching the call sites.
- **The human-door artifact path is unverified** — the spec marks it "verify in task"; if it turns out noted already, it just gains `[ui]` and the note (no relaxation).
- **Importer volume**: a 44-task migration now writes 44+ import lines into feature logs — bodies grow. Accepted: provenance was the explicit ask; the lines are short and materialised files diff-readably show what happened.
</risks>

<verification>
- dotnet build + dotnet test green after each task (baseline 1144).
- Grep gate (task 01): every status mutation site emits a sourced line.
- Round-trip: legacy lines survive; marker lines survive import.
- Pins: service-line formats, auto reasons, quickstart rules.
- Reviews: light on both tasks.
</verification>