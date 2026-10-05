---
feature: 110
slug: ready-means-ready-status-honesty
status: draft
created: 2026-10-05
---

# Result 110 — Status honesty: spec/plan grooming keeps the feature ready; inProgress only when a task is taken

## What shipped

The feature lifecycle no longer lies about being worked on:

- **Ceremony never fakes inProgress.** Artifact create no longer flips a feature to inProgress — grooming (spec/plan upserts, validations) keeps the feature ready (draft → ready on the first artifact, one `[auto]` line). Review branches untouched: reopen on task-create-on-review, demote on document invalidation, reopen-on-delete.
- **inProgress means a task is taken.** Taking a task — moving it to inProgress, review or done (the spec's full taken-set; widened from inProgress-only in the architect round) — promotes its draft/ready feature to inProgress with a `task NN taken → inProgress` `[auto]` line. One shared code path serves both doors; a second take does not duplicate the line; the review promotion runs first and the guard no-ops under review.
- **One-time data-fix.** `PlanningStatusBackfill` (startup, after backlog import, per-project materialization dispatch): inProgress features without a taken task (none, all-todo, or all-blocked) → ready with a `data-fix` `[auto]` line. Idempotent via AppSettings stamp (written even at zero liars, retried on failure); soft-deleted tasks excluded; races with async materialization ruled out (import awaited, dispatch reads committed state).
- **No auto-demote** (challengeable default per spec): a feature that started work stays inProgress even if its task is later reset.
- **Docs:** quickstart lifecycle line now reads «artifact create → ready (grooming never fakes inProgress); taking a task (any door) → inProgress»; the backfill boundary is documented next to the import-verbatim clause (a pre-deploy stale mirror may restore a liar on re-import — check taken tasks); MCP `planning_set_status` description mentions the feature-promotion side effect.

## Review rounds

- T1 light review PASS-WITH-NITS 0 blockers → orchestrator fix: direct draft-take pin added (import-shaped state).
- Architect light **APPROVED-WITH-WARNINGS** (audits/architect-110.md), both warnings resolved in the closeout round: W1 — take-trigger widened to the spec's taken-set (closing a task on a ready feature was skipping promotion, the exact liar class the wave kills); W2 — backfill boundary documented in the quickstart. I1 (all-blocked → ready) recorded as honest («blocked = not working»); the validated spec was deliberately not patched to avoid demotion for an informational.
- Consistency sweep: CeremonyPhasePlanner derives ceremony context from machine state (ready features get honest «writing spec»/«planning»/«plan validated» labels); web transition map already matched the server map (no UI flow relied on manual ready→review); claim UI and queue projection are orthogonal to grooming.

## Gates (re-run by orchestrator)

build 0w/0e exit 0, Core 365, Infrastructure 684, Api 656, Mcp 486 — all green; format verify green (Infrastructure + tests).

## Post-release click-check

1. Groom a fresh feature (spec → plan → tasks) → the card stays `ready` the whole time, ceremony label walks «writing spec» → «planning» → «plan validated».
2. Take task 01 (any door) → the feature flips to inProgress with a `task 01 taken → inProgress` `[auto]` line.
3. Board liars: any pre-deploy inProgress feature without a taken task lands as ready after deploy (log line `data-fix: inProgress without a taken task → ready`).
4. Close tasks straight to done from a ready feature → the feature goes inProgress, then review on the last done (validated spec+plan required).
5. Reopen: create a task on a review feature → back to inProgress, single reopen line, no second review line after closing the new task.
6. Manual ready → review from the board/agent door → refused by the transition map.

<log>
- 2026-10-05 [agent] — closeout complete: consistency sweep clean, docs updated (quickstart ×2 lines, MCP description), architect APPROVED-WITH-WARNINGS with W1 trigger-widening + W2 boundary docs resolved (audits/architect-110.md), gates green (Infra 684 / Mcp 486 / Api 656). Waiting on the human tap.
</log>