---
feature: 068
slug: mutation-transparency
status: validated
created: 2026-09-26
validated: 2026-09-26
---

# Spec 068 — Mutation transparency: маркеры на всех путях записи, statusChanged в ответах, current_task различает result-состояния (OM-11/12/13)

<goal>
The agent SEES what its mutation did. Three bands: OM-11 — every <log> write path stamps its source marker; OM-12 — patch/upsert responses carry artifact status and statusChanged on demotion, the feature follows its spec/plan out of review, one term ("validated") across all machine surfaces; OM-13 — planning_current_task tells result-drafted from result-validated, skips non-workable tasks with a held signal, and warns when spec/plan are missing or unvalidated. Absorbs microbatch #45.
</goal>

<decisions>
- D1 (user, 2026-09-26): the feature DEMOTES review→inProgress when its spec/plan stops being validated. Symmetry with 023/06 (task create demote). Invariant to document: review ⟺ spec+plan validated AND all tasks done.
- D2 (user, 2026-09-26): "validated" is the single term across responses, errors, tool texts, quickstart. "Frozen" leaves machine surfaces entirely.
- D3 (orchestrator, from MindlashStudio gap 2): current_task skips tasks in review/done when picking the next task; held state is returned only when nothing workable remains.
</decisions>

<requirements>
- R1 (OM-11): one log-line formatter on ALL write paths. PlanningLog.Append(source, note) exists (043) — route every missing call site through it: artifact.AppendLog gains a source param (append_log MCP door → Agent, HTTP door → Ui); patch logNote lines and upsert logNote lines carry the door's marker; [auto]/[import] lines unchanged. No write path may emit an unmarked line anymore; legacy unmarked lines still parse (043 contract holds).
- R2 (OM-12a): patch and upsert responses echo `status` and `statusChanged` ("validated → draft") when the mutation demoted a validated artifact; the demotion is recorded by an [auto] log line. Wording unified per D2 — tool descriptions, 409 ChangeNoteRequired text, quickstart.
- R3 (OM-12b, D1): demoting spec/plan below validated on a review feature flips the feature review→inProgress in the same SaveChanges ([auto] line + statusChanged reflected in the artifact response). Done features are never resurrected. Review-question resolved per D1; rule stated in the onboard quickstart.
- R4 (OM-13a): current_task result states — no result artifact → "All tasks are done — write the result artifact."; result draft → "Result artifact drafted — validate it."; result validated → "Result validated — close the feature." Distinguishable by state/reason without body reads.
- R5 (OM-13b): task payloads carry soft warnings — ["spec missing", "plan not validated"] when applicable; never STOP.
- R6 (OM-13c, D3): the resolver picks the next task among todo/inProgress only (blocked handled by the existing blocked-branch); review/done tasks are skipped. If only review/blocked tasks remain → state "held" with the reason naming the artifact and its status (e.g. "5 in review — awaiting review completion").
- R7: contract pins — MCP response shapes (Canned + McpTests), API behaviors (AgentPlanningApiTests), PlanningLog source round-trips, a grep gate asserting no "frozen" remains in machine-facing strings.
</requirements>

<non-goals>
- Artifact demotion semantics themselves (validated→draft on edit stays; only its visibility + the feature-level consequence change).
- Batch ops (044), version diff (045), artifact roster (080), task dependencies (081), feature-body update door (083).
- Hard STOP behavior for R5 warnings — soft by design.
- Feature-level dependency handling in current_task (030 already owns it).
</non-goals>

<acceptance-ideas>
- append_log via MCP → line reads "- YYYY-MM-DD [agent] — note"; via HTTP → [ui].
- Patch a validated spec → response shows statusChanged "validated → draft", [auto] line in <log>; if the feature was in review it is now inProgress (and the response says so).
- Tool texts and errors contain no "frozen"; quickstart states the review invariant and the demote rule.
- current_task: three result states distinct; a review-state next-task yields "held", not a workable-looking task payload; warnings appear on task payloads when spec/plan are missing/draft.
- dotnet build + test green; new contract pins in Mcp/Api/Core test suites.
</acceptance-ideas>

<context>
Code facts: PlanningLog.Append/Parse carry PlanningLogSource with legacy tolerance (043, PlanningLogTests). AppendLogAsync (PlanningArtifactService.cs:307) calls artifact.AppendLog(note) — no source → unmarked lines (OM-11 core defect). current_task resolver (006/04 contract): step 4 returns "All tasks are done — write the result artifact." blind to an existing result artifact (OM-13). TransitionOnArtifactCreate demotes review→inProgress on TASK create only (023/06) — spec/plan invalidation leaves the feature in review (D1 fixes this). UpsertArtifact demotes artifact validated→draft silently; response says "patched — version 2" (OM-12 defect). Tool text says "validated", 409 says "frozen" (D2 fixes the mix).
</context>

<refs>
- 043-status-provenance — the marker system extended here.
- 055/056 — response/warning idioms followed.
- 078-upsert-log-guard — logNote contract on upsert, term alignment lands on its 409 text.
- MindlashStudio dissection note 2026-09-26, gap 2 (held signal) — note deleted after intake; R6 is the intake.
- Microbatch #45 (append_log markers — absorbed; remove from the queue when this feature lands).
</refs>

<log>
- 2026-09-26 — spec drafted from feature body OM-11/12/13 + field intake; D1/D2 decided by user in chat, D3 from the MindlashStudio report.
</log>