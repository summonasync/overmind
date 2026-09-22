---
feature: 043
task: 01
slug: status-provenance
status: done
review: light
created: 2026-09-22
brief: Marker format in PlanningLog (append+parse, legacy-tolerant) + sourced service lines on every status mutation path (ui/agent/auto/import), grep-gated
---

# 043/01 — Sourced log lines: marker format + every mutation path stamped

<goal>
Every status mutation — feature or artifact, from any door — writes a log line carrying its source: `- YYYY-MM-DD [source] — text` with source ∈ {agent, ui, auto, import}. No silent status change remains in the codebase.
</goal>

<context>
The spec's code survey maps the sites: feature — PlanningFeatureService.cs:87 (UI update, silent today), :129 (agent, noted), :238 (importer restore); PlanningArtifactService.cs:245 (TransitionOnArtifactCreate), :254 (TryPromoteToReviewAsync); artifacts — agent SetStatus, the human-door status path (verify its current note behaviour in task), importer restore (PlanningFeatureService.cs:321). PlanningLog (src/Overmind.Core/PlanningLog.cs, now rebased on PlanningSections) owns the line format.

Executor reads ONLY this file.
</context>

<scope>
- Core: PlanningLog.Append(source, note) emits `- YYYY-MM-DD [source] — text`; Parse reads the marker into PlanningLogLine.Source; legacy lines (no marker) parse with Source null — backwards compatible; pin BOTH directions (append→parse round-trip, legacy tolerance) before touching call sites.
- Infrastructure, feature paths: :87 → service line `status moved to '<status>' via ui`; :129 → `[agent]` + the user's note; :238 → `[import] status restored from imported file`; :245 → `[auto] artifact created → inProgress`; :254 → `[auto] all tasks done + spec + plan → review`.
- Infrastructure, artifact paths: agent SetStatus → `[agent]` + note; human-door status path → `[ui]` (+ its note if it takes one today — verify; do not relax a note requirement); :321 → `[import] status restored from imported file`.
- Grep gate for the reviewer: no SetStatus call on feature/artifact remains without a sourced line or note.
- Tests: marker round-trip; legacy parse; each door's line verbatim; auto-transition reasons; import line.
</scope>

<anti-scope>
- No transition rule/matrix changes (spec D4).
- No new entity/table for logs; no web UI; no 045 diffs.
- Task 02's docs/quickstart work.
</anti-scope>

<constraints>
- English service-line texts exactly as specified; no comments; no new packages; no commits.
- Gates: dotnet build (0W/0E) + dotnet test green (baseline 1144; known flake PlanningSetStatusDescription_QuotesAllowedStatusesPerKind — rerun in isolation before judging).
</constraints>

<acceptance-criteria>
- Round-trip + legacy pins green; all surveyed paths stamped; grep gate clean.
- All gates green; git status shows Core/Infrastructure + tests only.
</acceptance-criteria>

<refs>
- Spec 043 D1/D2/D4, R1-R3, R6; plan risks (marker drift, human-door verification).
- PlanningLog.cs; PlanningSections (039); PlanningFeatureService.cs:87/:129/:238/:321; PlanningArtifactService.cs:245/:254.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-22 — Body fix before execution: duplicated closing goal tag and a stray fragment removed; contract text unchanged.
- 2026-09-22 — Execution starts: coder takes the sourced-lines contract (plan gate passed).
- 2026-09-22 — Light review PASS-WITH-NITS (0 BLOCKER / 0 IMPORTANT / 2 NIT, audits/review-043-01.md): all eight doors stamped with verbatim texts, marker pinned both directions, D4 matrix untouched, grep gate 7/7 clean. NIT-1 (numeric markers) fixed in-tree (digit-guard + in-range pin, 1170/1170); NIT-2 (internal overload) → microbatch #44 — no Core InternalsVisibleTo exists and cross-assembly tests use the overload legitimately.
- 2026-09-22 — Accepted — sourced lines complete across every door, numeric-marker guard landed. Feature awaits task 02 (docs/legend/result, in execution).
</log>
