---
feature: 096
slug: notification-cleanup
status: validated
created: 2026-09-28
validated: 2026-09-28
---

# Result 096 — Notification cleanup: delete + bulk actions в колокольчике (per-row ×, mark all read, clear read)

<outcome>
096-notification-cleanup shipped end to end: the human door gained real housekeeping — per-row DELETE /api/notifications/{id} (204/404), POST read-all (idempotent, {marked}), POST clear-read ({deleted}) — and the bell center became cleanable in two clicks: per-row quiet ×, «Mark all read», «Clear read» with optimistic cache surgery (badge + list move in one render, rollback on error). Spec v2 (architect-driven): clear-read excludes replied proof rows — the 077 validation-proof trail survives bulk cleanup; per-row DELETE stays the explicit remover. Family-wide invalidation (notificationKeys.all) pre-wires 087/093 queries. Agent door untouched, retention/TTL deferred to 095.

Suite: dotnet build 0w/0e; Notification filter 23/23 (rewritten proof-row test pins the v2 semantics); Api full suite 492/492; web lint 0e / tsc clean / build green / tests 27/27. Reviews: 01 PASS-WITH-NITS (audits/review-096-01.md, NIT-1 fixed in-tree, NIT-2 spec log-clarification), 02 PASS-WITH-NITS (audits/review-096-02.md, 2 no-action NITs), architect light APPROVED-WITH-WARNINGS (audits/architect-review-2026-09-28-096.md; WARNING-1 → spec v2 + fix-round; SUGGESTION-2 applied; SUGGESTION-3 radar).
</outcome>

<evidence>
Live e2e on an isolated dev host (scratch HOME, port 4894, PID-scoped cleanup, prod 4761 untouched):
1. agent-door create ×3 (info/approval/question) → 201 ×3; human list unread 3.
2. DELETE unread info → 204; repeat → 404; list unread 2.
3. read-all → {"marked":2}; repeat → {"marked":0} (idempotent); list unread 0, both rows ReadAt set.
4. New approval + reply "ship it" → ReadAt stamped by reply (084 invariant); read-all after reply → {"marked":0}.
5. Fresh unread info + clear-read → {"deleted":3} — wait, superseded by v2: the walk ran against pre-v2 code (deleted the replied row); post-v2 the same scenario keeps replied rows. The v2 behavior is pinned by the rewritten test ClearRead_KeepsRepliedProofRows_UnreadApprovalSurvivesReadAll (deleted=0 for proof rows, per-row DELETE → 204 on the replied row, then unread approval still removable after read-all).
6. replies endpoint returns the surviving question's reply ("answer survives"); host killed by PID, port free.

Gates re-run independently by reviewers (01: build + 492/492; 02: lint/tsc/test/build) and by the orchestrator fix-round (build 0e, Notification 23/23, web 27/27, tsc clean).
</evidence>

<deviations>
- Clear-read semantics amended post-implementation (spec v2): architect WARNING-1 showed the v1 delete set destroyed 077 validation proofs (replied = read ⇒ bulk-deletable). Resolution: exclude ReplyText != null from the bulk delete; per-row DELETE covers explicit removal. User-approved via the spec v2 validation round.
- Task 02 coder extracted shared mutation helpers (patchList/restoreList/settleList) and moved the 084 reply mutation onto them — review confirmed reply semantics byte-identical (becameRead guard, one-decrement, restore-on-error).
- Plan's task-03 scope line «reply → clear-read сносит» superseded by v2 (log deviation recorded on the plan; no body demote).
</deviations>

<follow-ups>
- Microbatch candidates: web-level unreadCount invariant test (badge-vs-window arithmetic — architect READ-4 «do not simplify» pin); markRead mutation not yet on the patchList idiom (intentional lightness, revisit when 087 adds queries).
- 093 spec should require a not-found fallback on the toast [Open] action (deep links are mortal since this feature — DELETE 404s them).
- 087 spec: rely on notificationKeys.all invalidation (already wired) and proof-row persistence (already guaranteed).
- NotificationsService split trigger: if 095 retention adds DeleteOlderThanAsync — extract bulk/maintenance ops then (architect SUGGESTION-3 radar; MarkAllRead SetProperty vs MarkRead() drift watch-item).
</follow-ups>

<log>
- 2026-09-28 [agent] — result drafted after closeout: live walk green, architect light APPROVED-WITH-WARNINGS resolved (spec v2 + fix-round), all gates green.
</log>