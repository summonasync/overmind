---
feature: 049
task: 06
slug: backlog-index-retirement
status: done
review: light
created: 2026-10-06
depends_on: 04, 05
brief: Final gates: dotnet build + full serial dotnet test + BACKLOG.md grep sweep (residues only where intended) + two checkpoint commits (code+tests / migration+docs), no push.
---

# Task 06 — Final gates + sweep

<goal>
The feature closes with green gates and a clean reference sweep; work is checkpoint-committed in two reviewable commits.
</goal>

<scope>
1. `dotnet build` green; full serial `dotnet test` green.
2. `grep -rn "BACKLOG\.md"` over `src/`, `tests/`, living root docs, kit living files — only intended residues pass: materializer legacy-cleanup code + its tests, AgentFileConflictScanner + fixture, kit tombstone header, FEATURELIST history, materialized trees. Anything else is a finding -> fix or obstacles note.
3. Checkpoint commit per crew rule 6 (conventional, attributed, no push), split for reviewability: (a) code + tests, (b) migration + docs + pointer.
</scope>

<anti-scope>
New scope items; git push; any fix that escapes the feature's spec — new findings go to the board, not into this task.
</anti-scope>

<constraints>
No push; commits only as checkpoints per rule 6; sweep output recorded in the log.
</constraints>

<acceptance-criteria>
- [x] Build + full serial tests green.
- [x] Sweep clean of unintended references (output in log).
- [x] Two checkpoint commits landed, push untouched.
</acceptance-criteria>

<refs>
spec cc4c4392-566e-4ff3-a84a-ff6aa61914fd; plan cf343934-d939-482d-9dd2-36dc8e41287b step 6.
</refs>

<obstacles>
- 2026-10-06, final gate round (049/06): (1) BUILD — green, 0 warnings / 0 errors, 121-lane WIP included and compiling. (2) SERIAL TESTS (-m:1) — Core.Tests 367/367 (baseline 365; +2 from 121-lane EntityFactoryTests WIP, passing), Infrastructure.Tests 692/692 (049 materializer pins + migration pins green), Mcp.Tests 509/509 (049/03 consumer reworks green), Api.Tests 662 passed / 10 FAILED of 672 total (baseline 663 at 8905e1d; +9 = new ClaimSettingsApiTests). All 10 reds are 121-lane WIP, zero 049-attributable: 9 x ClaimSettingsApiTests.* (whole class in untracked tests/Overmind.Api.Tests/ClaimSettingsApiTests.cs) + AgentSessionLockApiTests.Onboard_NamingRules_ProjectValueAndGenericHint (failure at line 609, inside the lane's uncommitted +12-line block; PUT /api/projects/{id}/claim-settings returns 405 — their endpoint WIP not wired). No BLOCKER for 049. (3) GREP SWEEP — zero unintended BACKLOG.md references. Clean: README.md, STRUCTURE.md, web/, build/, design/, dist/, repo .claude/. Living-doc hits are end-state statements only (AGENTS.md:36 "no BACKLOG.md index exists"; CONTEXT.md:52/149/151/170 retirement + legacy strip path; CONTEXT.md:131 historical tombstone comment per rule 8; TECHNICAL-SOLUTIONS.md:174-179 retirement ADR explicitly superseding the immutable ADR-consequence lines 24/92/101/108). src/: PlanningMaterializer.cs constant used only in legacy row handling (lines 185/311/353 — drift/inspect/remove paths, no writer); AgentFileConflictScanner.cs:61 (sanctioned); kit tombstone src/OvermindCrewKit/BACKLOG.md:1 + kit historical trees (001-004) + kit audits + kit contract files/skills with "(retired, 049)" end-state lines — all sanctioned residues. tests/: PlanningMaterializerTests (absence pins + legacy-row seed/drift/refusal pins), PlanningMaterializationTests:69 absence pin, CrudRoundtripTests legacy-row roundtrip, InstallCommandOnboardTests fixture input + scanner assertions — all end-state pinning, sanctioned. (4) GIT — no 049 dirt left: the lane's rounds are committed as 4470663 (049/01 code), d063aea (049/02 materializer/migration pins), 8905e1d (049/03 consumer tests), 8a0f9fc (docs + ADR) — the two-review-commit split of the AC is covered by these; this round adds no commit. Remaining working-tree dirt is foreign: 121-lane WIP (Project.cs, ProjectValidators.cs, DependencyInjection.cs, Program.cs, ClaimSettings* x3 untracked, AgentSessionLockApiTests.cs, EntityFactoryTests.cs), Overmind-owned mirror (BACKLOG.md + BACKLOG/** incl. untracked result.md x4), audits/architect-review-2026-10-06-100.md — none staged, none touched. Ops note: the Overmind API host (port 4761) went down with a clean shutdown at 18:16 mid-round; restarted from the same 12:54 deployment via run.sh to finish board writes.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan step 6
- 2026-10-06 [agent] — 049/06 gate round started
- 2026-10-06 [agent] — final gate round results recorded
- 2026-10-06 [agent] — final gate round complete — build green (0/0); serial tests: Core 367, Infra 692, Mcp 509 all green, Api 662/672 with 10 reds all attributed to the 121 claim-settings lane WIP (9 untracked ClaimSettingsApiTests + 1 AgentSessionLockApiTests line 609 inside their uncommitted block), zero 049-attributable; grep sweep clean — every BACKLOG.md hit is a sanctioned residue (end-state doc statements, superseding ADR 049, tombstone, kit historical trees, legacy row-handling branches, absence-pinning tests, conflict-scanner fixture); git verdict: 049 rounds already committed as 4470663/d063aea/8905e1d + 8a0f9fc, no 049 dirt remains, this round adds no commit, nothing foreign staged, push untouched; 3/3 AC ticked; ops note: Overmind API host had cleanly shut down at 18:16 mid-round, restarted from the existing 12:54 deployment to finish board writes
- 2026-10-06 [agent] — final gate result — see log: build green; serial tests green for the 049 lane (Core 367 / Infra 692 / Mcp 509, Api reds 10/10 attributed to 121-lane WIP); sweep clean of unintended references; checkpoint commits already landed by tasks 01-04 (4470663, d063aea, 8905e1d, 8a0f9fc), no 049 dirt left, no new commit, push untouched; 3/3 AC ticked
- 2026-10-06 [agent] — light review APPROVED — 0 blockers. Consistency math verified (Api 672 = 663+9 new; Core 367 = 365+2; Infra/Mcp corroborated), both attribution spot-checks clean (untracked ClaimSettingsApiTests ×9; PUT /claim-settings 405 = 121's new surface, zero 049 reachability), sweep residues match the allowed list, spot-run Infra 692/692. Note: AC «two checkpoint commits» fulfilled by the four per-round commits (documented in artifact).
</log>