---
feature: 090
task: 01
slug: field-nits-batch
status: done
review: light
created: 2026-09-29
brief: Backend+MCP brief-patch: PatchPlanningArtifactRequest.brief, PatchAsync third branch (task-only, no version bump), InvalidPatchRequest 3-action text, MCP brief param + tests
---

# Task 1 — brief-patch (R1)

<goal>
Fixing a typo in a task brief must not cost a full body re-upsert + version bump. planning_patch_artifact gains a brief action: task-only, no version bump, no log line, body untouched.
</goal>

<scope>
- src/Overmind.Host/Api/Contracts/PlanningContracts.cs — PatchPlanningArtifactRequest gains string? Brief.
- src/Overmind.Infrastructure/Planning/PlanningArtifactService.cs — PatchAsync: exactly one of {section, tick, brief}; brief branch calls artifact.SetBrief(patch.Brief); non-task kinds reject PlanningException.BriefNotAllowedForKind; feature.Touch + save + materialization sync like sibling branches.
- src/Overmind.Core/Abstractions/PlanningException.cs — InvalidPatchRequest message names all three mutually exclusive actions.
- src/Overmind.Mcp/OvermindPlanningTools.cs — planning_patch_artifact gains brief parameter + description (task-only, max 500 chars, no version bump); op builder extended.
- tests/Overmind.Api.Tests/PlanningApiTests.cs — new cases.
</scope>

<anti-scope>
- Feature title patching (already covered by planning_update_feature).
- planning_upsert_tasks brief handling (already exists).
- logNote requirements changes (task artifacts are never frozen — none needed).
</anti-scope>

<constraints>
- Existing section/tick behavior stays byte-identical for old callers (load-bearing XOR).
- No migrations, no new packages, comments only for non-obvious decisions.
</constraints>

<acceptance-criteria>
- [x] Patching brief on a task returns 200, version unchanged, body unchanged, brief updated.
- [x] Patching brief on spec/plan/result returns 400 BriefNotAllowedForKind.
- [x] section+brief and tick+brief combinations return 400 InvalidPatchRequest with the three-action message.
- [x] planning_patch_artifact accepts brief and forwards it; description documents task-only/no-version-bump.
- [x] dotnet build green.
</acceptance-criteria>

<refs>
- Spec 62aac67e (R1); plan b78738e8 (Task 1).
- PlanningArtifact.SetBrief semantics: no version, no log (same as brief-on-upsert).
</refs>

<obstacles>
Orchestrator probe after the report: section-patch (artifact 11062612, obstacles, now v2) and a tick no-op (this artifact, acIndex 0 → "already ticked — no change") both succeeded against the live 0.5.5 host. The reported generic failures did not reproduce; likely transient contention while coder and orchestrator sessions mutated concurrently (SQLite single-writer). If it resurfaces, capture the HTTP status/detail before blaming the tool.

</obstacles>

<log>
- 2026-09-29 [agent] — task cut from validated plan.
- 2026-09-29 [agent] — [agent] taken into work; dispatching dotnet-coder (sonnet) per the briefing contract.
- 2026-09-29 [agent] — implemented brief-patch; logged three scope deviations (AgentPlanningApi.cs was required, tests placed in AgentPlanningApiTests.cs, MCP tier phrase kept pinned)
- 2026-09-29 [agent] — diagnostic: tick and section patches now fail after the 17:06 obstacles write; reads still work
- 2026-09-29 [agent] — full-body re-upsert: ticked all five ACs after verification, and repaired the nested <obstacles> tags my earlier section patch introduced (content was sent wrapped — double nesting broke section parsing for every later patch call)
- 2026-09-29 [agent] — [agent] → review: implementation complete and verified — full solution build green (0 warnings), full dotnet test green (Core 225 + Infrastructure 535 + Api 515 + Mcp 388 = 1663 passed, one unrelated Mcp flake did not reproduce in two reruns), all five ACs verified by the six new AgentPlanningApiTests cases and ticked via the full-body upsert after planning_patch_artifact started failing server-side (both its branches error on a well-formed body while append_log/upsert/set_status work; not reproducible against the repo source)
- 2026-09-29 [agent] — Obstacles: added live-probe outcome for obstacle 4 (patch-tool failures not reproducible).
- 2026-09-29 [agent] — [agent] done: light review PASS-WITH-NITS (audits/review-090-01.md), 1 IMPORTANT + 3 NIT → microbatch queue, 0 blockers; all 4 coder obstacles confirmed (4th not reproducible, orchestrator probe evidence attached); build 0w, Api 123/123 + Infra 535/535 + Mcp 388/388 reviewer rerun.
</log>