---
feature: 039
task: 02
slug: artifact-patch-write
status: done
review: light
created: 2026-09-22
brief: PlanningArtifact.PatchSection/TickAc + service PatchAsync: snapshots on every patch, frozen demotion, strict 400-grade diagnostics, AppendLog joins snapshots
---

# 039/02 — Patch domain + service: PatchSection/TickAc, snapshots, strict errors

<goal>
Domain mutations that patch a body without resending it: `PlanningArtifact.PatchSection` and `TickAc`, plus `PlanningArtifactService.PatchAsync` with strict diagnostics. Every patch snapshots the previous body (D4); frozen artifacts demote (D5); errors are loud, never silent (D6). `AppendLog` joins the snapshot discipline.
</goal>

<context>
Task 01 delivered `PlanningSections`. Today `PlanningArtifact.UpdateBody` (PlanningArtifact.cs:131-158) is the only body mutation that snapshots (`PlanningArtifactVersion.Create(Id, Version, Body)` + `Version++`) and handles frozen demotion; `AppendLog` (:203) mutates `Body` with no snapshot — an inconsistency this task removes per spec D4. The service layer (`PlanningArtifactService`) wraps mutations with materialisation sync; `PatchAsync` follows the same shape as `SetStatusAsync` (:112-143).
</context>

<scope>
- `PlanningArtifact.PatchSection(string section, string content, string? logNote)`: replace via `PlanningSections`; snapshot first; frozen requires `logNote` and demotes to draft (mirror `UpdateBody`); `Version++`.
- `PlanningArtifact.TickAc(int? index, string? text, string? logNote)`: locate item via `ParseAcceptanceCriteria`; by index (0-based) or by exact text match; mark `[ ]` → `[x]` preserving indentation and suffix; snapshot + version as above; already-ticked target → successful no-op (idempotent); multiple text matches → throw for a 400 with the matching indices listed.
- `PlanningArtifact.AppendLog`: now snapshots like `UpdateBody` (D4 — the single behavioural change to an existing path; note it in the artifact log after implementation).
- `PlanningException` additions: section-not-found (with the section names the body does contain), AC-item-not-found (with visible item indices/texts), ambiguous-AC-text (with indices).
- `PlanningArtifactService.PatchAsync(Guid artifactId, patch request, ct)`: loads artifact + feature, applies the mutation, `SaveChangesAsync`, `SyncMaterializationAsync` — same guard shape as existing mutations.
- Unit tests: frozen demotion with/without logNote; tick idempotency (version does NOT bump on a no-op re-tick — or bumps exactly once, decide by "no material change → no snapshot" and pin it); diagnostics carry the searchable content; AppendLog now bumps Version (pin the new behaviour).
</scope>

<anti-scope>
- No HTTP or MCP surface — task 03.
- No materialisation/e2e verification — task 04.
- No Web UI.
- No batch operations (044).
</anti-scope>

<constraints>
- Snapshot BEFORE mutation (ordering is load-bearing for 045 diff history).
- Errors must name what was searched and what exists — an agent must be able to self-correct from the message alone.
- Idempotent re-tick: no snapshot, no version bump, no log line — a true no-op.
</constraints>

<acceptance-criteria>
- `dotnet build` + `dotnet test` green.
- Every mutation path snapshot-tested; AppendLog version bump pinned.
- Strict-error messages contain section names / AC indices; ambiguity lists indices.
- Frozen semantics identical to `UpdateBody` for both new operations.
</acceptance-criteria>

<refs>
- Spec 039 D4, D5, D6, R1, R5.
- PlanningArtifact.cs:131-158 (UpdateBody pattern), :203 (AppendLog).
- PlanningArtifactService.cs:112-143 (SetStatusAsync shape), :162 (AppendLogAsync).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-22 — Carry-over from review 039/01 (NIT-2, audits/review-039-01.md): PlanningSections.OpenLength includes the trailing \r — when PatchSection replaces content, prefer an explicit OpenEnd helper (or equivalent) over hand-rolled step-back arithmetic so CRLF bodies stay byte-predictable.
- 2026-09-22 — Execution starts: coder sub-agent takes the domain/service contract on top of the reviewed PlanningSections parser (task 039/01 done, PASS-WITH-NITS).
- 2026-09-22 — Coded (dotnet-coder): PatchSection/TickAc on PlanningArtifact via shared ApplyBodyMutation (snapshot-before-mutate, Version++, frozen demotion identical to UpdateBody); AppendLog joined the snapshot discipline — BEHAVIOUR CHANGE pinned: append_log now bumps Version and writes a PlanningArtifactVersion row (old no-bump pins replaced in Core/Infra/Api tests). PatchAsync follows the SetStatusAsync shape and returns the mutated artifact for task 03's compact echo. PlanningException additions: PatchSectionNotFound (lists present sections), TickItemNotFoundByIndex/ByText (list visible items), TickTextAmbiguous (lists indices) plus two beyond the contract: TickItemNotCheckable (plain bullet refused loudly, D6) and InvalidPatchRequest (service dispatch: section XOR tick, content required for section). Pinned deviations: idempotent re-tick short-circuits BEFORE the frozen guard (spec D6 "must not punish the agent" — re-tick on a frozen artifact is a no-op, a real flip still requires logNote and demotes); text match is exact/ordinal/case-sensitive against the marker-stripped item text. NIT-2 carry-over satisfied by delegating to the tested PlanningSections.Replace — zero hand-rolled step-back arithmetic, task-01 files untouched; CRLF byte-predictability pinned by PatchSection_CrlfBody/TickAc_CrlfBody tests. TDD red (30 failing) then green. Build 0W/0E; tests 1068/1068 (Core 175 / Infra 346 / Mcp 238 / Api 309). Off to light review.
- 2026-09-22 — Light review PASS-WITH-NITS (0 BLOCKER / 0 IMPORTANT / 2 NIT), gates re-verified: build 0W/0E, tests 1068/1068. Task-01 files confirmed untouched; snapshot ordering, frozen mirror, re-tick pin, diagnostics all verified. Report: audits/review-039-02.md.
- 2026-09-22 — Accepted: 2 NITs routed — NIT-1 (SetStatus body-append without snapshot) → microbatch queue #36; NIT-2 (blank-field 500) → carried into 039/03 task log as a boundary-validation requirement. Domain layer is the stable base for the agent surface.
</log>
