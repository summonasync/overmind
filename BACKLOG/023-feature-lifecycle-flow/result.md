---
feature: 023
slug: feature-lifecycle-flow
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 023 — Feature lifecycle state machine + board filter persistence

<outcome>
Feature 023 delivered the honest lifecycle state machine end-to-end: five statuses with a transition map (draft/ready/inProgress/review/done), event-driven auto-promotions (artifact-create → inProgress; last-task-done + spec + plan → review), review→inProgress rollback, and — added 2026-09-20 by user decision — the escape hatch (done reachable from any non-done status, one click; done terminal; auto-transitions never set done). The ceremony gap caught live on 025 (task carved on a review feature left it stuck) is fixed: task-create on review auto-demotes to inProgress (023/06). Full lifecycle proven live on the deployed build (023/05 walk, evidence in task log).
</outcome>

<evidence>
- Gates final: dotnet 922/0 (baseline ~801 at feature start; matrix tests inverted for the escape hatch); web lint/tsc/build green.
- Reviews: review-023-01..04 (original tasks), review-023-06.md (PASS, 0 blocking), review-023-07.md (PASS-WITH-NITS, parity exact).
- Live walk 2026-09-20 on deployed 0.1.9 (288e7b5): draft→ready manual; ready→inProgress auto on spec; task-done promotions; manual rollback; task-create-on-review auto-demote (06 live); one-PUT done (escape hatch); done→X 400 typed "allowed targets: ." — all green, scratch cleaned.
- Spec amended (v2) with the escape-hatch decision (tombstone of the review-only rule + decision entry on the board).
</evidence>

<deviations>
- done-lock in web selects now derived from allowedTransitions().length === 0 (was hardcoded — review-023-03 INFO-2, closed by 023/07).
- 023 was flipped done prematurely mid-session (with tasks 05/06 open) — tail work continued on the done feature; terminal-done made repinning impossible and unnecessary.
</deviations>

<deferred>
- 023/08 (carved, todo): planning_set_feature_status MCP tool — done-by-request with mandatory logNote (user decision 2026-09-20).
- Reopen-demotion asymmetry (done-task reopen on review does not demote) — review-023-06 INFO-1; carve if multi-wave flows start reopening tasks.
- result-validated → review promotion question (dogfood feedback batch 2, item 1) — decision pending; escape hatch covers the pain meanwhile.
- Per-kind artifact status model documentation (feedback batch 2, item 2) — candidate task joining the lifecycle self-documentation theme (feedback batch 1, fact 1).
</deferred>

<log>
- 2026-09-20 — Supersedes the premature result (which covered tasks 01-04 only, written before the tail was finished): the feature stayed done while tasks 05-07 executed; this version records the full outcome incl. the escape hatch (07), the ceremony-gap fix (06), the live walk (05), and the deferred items. Prior version preserved in artifact history.
</log>
