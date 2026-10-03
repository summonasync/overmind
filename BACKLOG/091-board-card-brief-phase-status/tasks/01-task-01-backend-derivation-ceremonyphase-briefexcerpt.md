---
feature: 091
task: 01
slug: board-card-brief-phase-status
status: inprogress
review: light
created: 2026-10-03
brief: CeremonyPhase + BriefExcerpt derivation in Infrastructure, wired into board payload + queue entries, edge matrix
---

# Task 01 — Backend derivation: CeremonyPhase + BriefExcerpt

<goal>
One server-side derivation feeding both surfaces: phase label machine + brief excerpt, wired into the board payload and the queue projection.
</goal>

<scope>
- src/Overmind.Infrastructure/Planning/CeremonyPhaseService.cs (or a static planner per house idioms): Derive(feature) per the spec table (drafting / writing spec / planning / plan validated / in progress / review / done) from specStatus/planStatus/taskDone/taskTotal/status — no new persisted status; BriefExcerpt(feature) — first non-empty <goal> paragraph, cap ~150 chars with ellipsis, null when no goal/body.
- Wire into the board read payload (PlanningMapper/board path) and planning_task_queue projection (excerpt + phase label fields; 072 payload shape extends).
- Edge matrix unit tests: no goal, no body, validated-but-draft, done card, cap boundary on cyrillic/CJK, spec-validated-no-plan, all seven states.
</scope>

<anti-scope>
No web rendering (task 02), no MCP contract changes beyond the new queue fields (task 03), no persisted status changes.
</anti-scope>

<constraints>
English-only, no comments by default, no auto-commits; dotnet build + full test real numbers.
</constraints>

<acceptance-criteria>
- [ ] dotnet build 0/0 + full dotnet test green
- [ ] Phase machine: all 7 states pinned by tests
- [ ] Excerpt: goal-paragraph derivation, 150-cap on cyrillic/CJK, absent goal → null (no empty string)
- [ ] Board payload + queue entries carry both fields
- [ ] Queue excerpt is null-safe for bodyless features
</acceptance-criteria>

<refs>
- Spec: d066ec12-2fcb-44cd-9e8a-51491ab205ab (Decisions + Edge cases).
- 072-planning-task-queue (payload shape being extended).
</refs>

<obstacles></obstacles>

<log></log>

<log>
- 2026-10-03 [agent] — dispatched to dotnet-coder (same session — wave 028/099 patterns), 101 done unblocked 091 per the user's pipeline plan
</log>
