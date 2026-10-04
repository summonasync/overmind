---
feature: 111
task: 01
slug: brief-cap-500
status: done
review: light
created: 2026-10-04
brief: BriefCapLength 150→500, 091 matrix re-pinned, result (Simple path)
---

# Task 01 — Cap constant + re-pin + result

<goal>
The brief excerpt cap moves 150 → 500 text elements; nothing else changes.
</goal>

<scope>
- BriefCapLength 150 → 500 in CeremonyPhasePlanner.BriefExcerpt (one source: board cards + planning_task_queue).
- The 091 cap matrix re-pinned: 500/501 boundary, cyrillic long goal, surrogate pairs, «…» overflow, null on absent goal/body/blank goal.
- Contract tests asserting excerpt shapes updated where they hardcoded the cap.
- Result artifact.
</scope>

<anti-scope>
- Multi-paragraph briefs; per-project caps; line-clamp (091 decision stands).
</anti-scope>

<acceptance-criteria>
- [x] Cap matrix green at 500/501; surrogate pairs and «…» overflow intact.
- [x] Board + queue parity tests green (same derivation feeds both).
- [x] Affected suites green; result drafted.
</acceptance-criteria>

<refs>
- Spec 8af687f4; plan cfa287da; 091 CeremonyPhasePlanner tests.
</refs>

<log>
- 2026-10-04 [agent] — executed by the orchestrator (one-constant change, Simple path): ExcerptCap 150→500, 091 matrix re-pinned at 500/501, tool description updated; planner 19/0, Api parity 2/0, build 0/0; result 9f442f45 drafted. Light review rides the 111+112 joint pass.
- 2026-10-04 [agent] — [orchestrator] Light review passed (PASS-WITH-NITS, 0 blockers), all 3 AC confirmed. Closing task; result stays draft per pipeline mandate (human tap batched).
</log>
