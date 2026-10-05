---
feature: 105
slug: register-all-task-sections
status: done
priority: p1
version: 1
created: 2026-10-03
---

# Feature 105 — Register all task sections on upsert (patch without full re-send) — Qwen friction #4 / bug 6ee3be0f

<goal>
Task artifacts ride in with partial section registration (Qwen friction log item 4 = board bug note 6ee3be0f, independently re-confirmed 2026-10-03): `<anti-scope>` and `<constraints>` arrive in the body but patch_artifact refuses them — fixing one word costs a full ~8 KB body re-send through the model.
</goal>

<requirements>
1. Batch task upsert registers ALL task sections present in the body (goal, scope, anti-scope, constraints, acceptance-criteria, refs, obstacles, log) — patchable afterwards.
2. Existing task artifacts created before the fix: a backfill path (registration-only migration or lazy registration on next touch) — decide at spec.
3. Contract tests: upsert with all 8 sections → every section patchable without full re-send.
</requirements>

<non-goals>
- Rewriting the section parser (039 contract stands).
</non-goals>

<refs>
- Bug note 6ee3be0f (original report, dotNetCrew find).
- Friction log entry 500f8197 (item 4).
</refs>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
- 2026-10-05 [agent] — Van by claude claimed the feature
- 2026-10-05 [auto] — all tasks done + spec + plan validated → review
- 2026-10-05 [agent] — done via agent review — approved by human tap (consolidated stack approval bb929195, reply 2026-10-05 17:36:12 UTC)
- 2026-10-05 [agent] — claim released on done-close
</log>
