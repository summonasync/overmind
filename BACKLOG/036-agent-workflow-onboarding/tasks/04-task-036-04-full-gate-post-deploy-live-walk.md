---
feature: 036
task: 04
slug: agent-workflow-onboarding
status: done
review: light
created: 2026-10-06
depends_on: 01, 02, 03
brief: Full gate: dotnet build Overmind.sln + full dotnet test Overmind.sln green (cold-CLR flake: build-server shutdown, retry). Post-deploy verification — real /api/onboard payload eyeball — only after the next adapter deploy, NOT runnable now; contract tests carry the gate until then.
---

# Task 036/04 — full gate + (post-deploy) live walk

<goal>
The whole suite is green with the new teaching text in place, and the live onboard payload is eyeballed once the adapter ships it — closing the feature's done bar honestly.
</goal>

<scope>
- dotnet build Overmind.sln, then full dotnet test Overmind.sln. Known cold-CLR flake per AGENTS.md: dotnet build-server shutdown, then retry the same command.
- Live-payload check (POST-DEPLOY ONLY, not runnable in this task's environment now): after the next adapter deploy/restart, run the host and eyeball the real /api/onboard output — ceremony block present, 032 blocks intact, no truncation. Until then the contract tests (036/01–03) carry the gate; record the deploy-time observation in this task's <log> when it happens.
</scope>

<anti-scope>
- No code changes in this task; a red suite routes back to the owning slice task (036/01–03), not fixed here.
</anti-scope>

<constraints>
- Full serial suite — no test filtering for the gate; targeted filters were the per-slice gates.
- If the post-deploy walk is impossible at close time, the task closes with the suite gate + an explicit <log> note that the live walk is pending deploy — the limitation goes to the result artifact, never silently dropped.
</constraints>

<acceptance-criteria>
- [x] dotnet build Overmind.sln green.
- [x] Full dotnet test Overmind.sln green (flake protocol followed if hit).
- [ ] Post-deploy live walk done and logged, OR an explicit pending-deploy note in <log> carried into the result artifact.
</acceptance-criteria>

<refs>
- Spec: 036/S (a8d38f0d-37c9-4513-948e-30a227c1a566, validated). Plan: 036/P (66a76b70-ca36-46a8-ada4-6527042ad4ab, validated) — slice 4.
- Blocked by tasks 1–3: the full gate runs on their combined result.
</refs>

<obstacles>
- Live walk requires the next adapter deploy (spec obstacle note, 032 precedent) — pending by design, see constraints. Gate round 2026-10-06: contract tests carry the gate until then (036/01–03 suites green); the real /api/onboard payload eyeball (ceremony block present, 032 blocks intact, no truncation) happens after the next adapter deploy/restart and must be recorded in this task's <log> per the original scope.
- 44 expected-red tests from the 049/01 lane sit in the working tree (uncommitted PlanningMaterializer.cs / ManagedBlock.cs writer-removal WIP, Van's lane): all failures are BACKLOG.md-writer removals, deterministic on re-run, none in the 036 ceremony lane. Excluded from this gate per orchestration brief; tasks 049/02–03 fix them.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan 66a76b70 (slice 4).
- 2026-10-06 [agent] — 036/04 gate round started
- 2026-10-06 [agent] — Gate round at HEAD 5c336b3: dotnet build Overmind.sln green (8 projects, 0 warnings / 0 errors). Full serial dotnet test Overmind.sln -m:1: Core.Tests 365/365, Mcp.Tests 509/509, Api.Tests 658/663, Infrastructure.Tests 653/692 — 2035 passed / 44 failed / 2729 total. All 44 failures are the 049/01 lane (uncommitted writer-removal WIP in the tree): PlanningMaterializerTests 25, PlanningRoundTripTests 7, PlanningMaterializationTests 4, BacklogImportMaterializationTests 3, PlanningMaterializationStatusReaderTests 2, OnboardApiTests.Onboard_MaterializationBlock 1, EntryMaterializationTests 1, PlanningServiceGuardTests 1 — every failure is the removed BACKLOG.md writer (FileNotFoundException / skipped-count 4→3), deterministic on re-run, so the cold-CLR flake protocol (build-server shutdown + retry) was not triggered. 036-relevant suites judged strictly green: Mcp.Tests 509/509, Core.Tests 365/365, OnboardApiTests 27/28 (sole red is the materialization-block test, 049 lane). No code changes, no commit (WIP in the tree belongs to the 049/01 holder).
- 2026-10-06 [agent] — gate green — live-walk post-deploy pending
- 2026-10-06 [agent] — light review correction — the summary line «2035 passed / 44 failed / 2729 total» is a transcription slip (arithmetic impossible, contradicts own per-suite numbers); correct figures: 2185 passed / 44 failed / 2229 total (Core 365 + Mcp 509 + Api 663 + Infra 692 = 2229, failed Api 5 + Infra 39 = 44). Per-suite evidence verified and corroborated independently by the 049/01 artifact and 036/03; reviewer spot-check Mcp.Tests 509/509 post-commit.
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 1 non-blocking correction (summary-line transcription slip, corrected via preceding append_log: 2185/44/2229). Consistency triple-corroborated, exclusion of 049-lane reds judged legitimate (attribution, not filtering), spot-check Mcp 509/509. AC3 live-walk stays pending-deploy in obstacles.
- 2026-10-06 [agent] — POST-DEPLOY LIVE WALK (0.8.0, host 1.0.0+2cce10c, 2026-10-06): POST /api/onboard (cwd+vendor) — quickstart step 4 carries the full gate ceremony verbatim (planning_create_feature → spec → «overmind_notify(kind=approval, featureSlug), wait for the human's reply, then planning_validate; every edit voids the reply» → plan → planning_upsert_tasks «execution contract… ONLY its own task artifact» → work loop planning_current_task/planning_set_status); agentIdentity block renders (121/02) with default alias/vendor + project naming-rules text; validation block humanGate:false. AC3 (live-walk) CLOSED.
</log>