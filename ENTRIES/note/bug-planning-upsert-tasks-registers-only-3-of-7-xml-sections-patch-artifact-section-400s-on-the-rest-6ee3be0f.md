---
id: 6ee3be0f-63b7-454f-8153-3dc2447ce7f0
type: note
title: Bug: planning_upsert_tasks registers only 3 of 7 XML sections — patch_artifact(section=...) 400s on the rest
tags: batch-ops, overmind-bug, planning-api, reported-by-dotnetcrew, sections
createdAt: 2026-09-29T15:03:48Z
updatedAt: 2026-09-29T15:03:48Z
---

## Bug: batch task upsert leaves most XML sections unregistered — later section patches fail 400

**Reported:** 2026-09-29, found during dotNetCrew feature `007-product-strategist` execution (task 06, main orchestrator report).

**Repro:**
1. `planning_upsert_tasks` with a task body declaring the full XML section set: `<goal>`, `<scope>`, `<anti-scope>`, `<constraints>`, `<acceptance-criteria>` (checkbox items), `<refs>`, `<obstacles>` — obstacles carried non-empty text `None.`.
2. Creation succeeds; `planning_set_status` log appends work fine (single `<log>`, no duplication — the 003-era dup did NOT reproduce on this path).
3. AC ticking via `planning_patch_artifact(acIndex=...)` works.
4. `planning_patch_artifact(section="obstacles", content=...)` → **400**: `Section 'obstacles' was not found in the artifact body. Sections present: scope, acceptance-criteria, log`.

**Observation:** of the 7 declared sections, the section registry only sees 3 (`scope`, `acceptance-criteria`, `log`). `goal`, `anti-scope`, `constraints`, `refs`, `obstacles` are in the body but not addressable via section patches after a batch upsert. Section registry appears inconsistent between `planning_upsert_artifact` (single — sections registered fine in the 003 era) and `planning_upsert_tasks` (batch — most sections lost to the registry).

**Impact:** after batch task creation, `<obstacles>` / `<refs>` / `<constraints>` cannot be patched in place — the documented sub-agent write-back path (patch obstacles/refs) is broken for batch-created tasks. Workaround: stash findings in `<log>` via `planning_append_log` (used in 007/task 06), or an expensive full-body `planning_upsert_artifact` rewrite.

**Related:** dotNetCrew entry `edaff06e` (003-era log-duplication bug, same subsystem), Overmind 078 (upsert log guard), 039 (patch write), 044 (batch ops — the path that surfaced this).

**Suggested fix direction:** section registry must cover every declared XML section regardless of upsert path; a declared section with placeholder text (`None.`) must register and be patchable; empty declared sections should materialize on first patch (already suggested in edaff06e).

**Refs:** dotNetCrew feature 007-product-strategist, task artifact `4474de93-23f8-42df-9a0f-4b1fbd0f02d6`.