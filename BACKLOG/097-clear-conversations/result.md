---
feature: 097
slug: clear-conversations
status: validated
created: 2026-09-29
validated: 2026-09-29
---

# Result 097 — Clear conversations: guarded bulk-delete of replied notifications (077 proof-aware)

## Outcome

Both tasks done, all layers green, the feature lives in the working tree (uncommitted, awaiting the wave commit):

- **01 Backend (done, PASS-WITH-NITS):** AgentNotification.HiddenAt (nullable) + Hide(); migration 20260929000000_AddNotificationHiddenAt (additive nullable, no index/backfill); ClearConversationsAsync — one set-based ExecuteUpdate over replied-unhidden rows (MarkAllReadAsync idiom); default list AND unreadCount filter HiddenAt == null BEFORE OrderBy/Take(50); ?includeHidden=true reveals via GetOverviewAsync(includeHidden) — the ListRepliesAsync(includeRead) shape precedent; POST /api/notifications/clear-conversations → { hidden }; ListRepliesAsync byte-untouched. 5 Api pins.
- **02 Web (web-delta PASS-WITH-NITS):** "Clear conversations" in the center header (disabled at repliedCount === 0), no optimistic patch — server-authoritative + settleList invalidation over the single keys family (["notifications","list",{includeHidden}] → settleList hits notificationKeys.all); "Show hidden" toggle (aria-pressed chip) with dimmed archive rows, × from the archive keeps the 096 hard-delete semantics; header always renders (the old empty-state early-return made the toggle unreachable exactly in the post-clear state); placeholderData: keepPreviousData so toggling does not flash a loader. 4 web pins (40/40 total).

## Verification

- dotnet build 0/0; dotnet test full **1649/1649** (Core 225, Infrastructure 535, Api 501, Mcp 388) — both coder and architect-reviewer runs agree.
- Web gates reviewer-run: lint 0 errors (6 pre-existing warnings in untouched files), tsc clean, static export build OK, 40/40.
- **Live e2e** (isolated host: HOME=/tmp/overmind-097-walk, port 4896, PID-scoped cleanup incl. the child dotnet process; prod 4761 untouched):
  1. question → reply "SQLite" → replied row present
  2. POST clear-conversations → {"hidden":1}
  3. default list: 0 replied rows; the unread survivor stays (unreadCount=1)
  4. includeHidden=true returns the row with hiddenAt stamped
  5. the AGENT replies endpoint still returns the hidden row — 077-safe by construction, verified live
  6. second call → {"hidden":0} — idempotent
  7. UI layer (button/toggle/dim) — user click-check at the next deploy, listed below.

## Deviations & notes

- includeHidden shape: a bool parameter on GetOverviewAsync(bool, ct) with the default at the endpoint — mirrors ListRepliesAsync(includeRead); documented in task 01 obstacles.
- Task 02 obstacles' premise "every archive row is replied" is inaccurate (includeHidden returns all rows; the backend applies no filter) — the code is correct per the task letter; premise fixed at closeout (review-097-02 NIT-2).
- **Recorded decision (architect WARNING-1):** the archive rides the 50-row windowed read model — past ~50 total rows older hidden rows are unreachable in the UI (data intact; the 077 proof search is the only windowless read and stays complete). RepliedCount is window-bound while the mutation is global (the { hidden } response reports the real number). Filed to the microbatch queue (#140) for the 087/pagination era: raise the take for includeHidden (S) or paginate the archive (M).
- Architect APPROVED-WITH-WARNINGS (audits/architect-review-2026-09-29-097.md): WARNING-2 — the hide≠validation invariant is pinned only on the replies door; a priority microbatch test (#139: approval → reply → Hide() → ValidateAsync finds the proof, closes #137 by giving Hide() its first caller) makes it structural. SUGGESTIONs #141 (ADR for the hide model, user-gated) and #142 (web pin for ?includeHidden).
- UI click-check (user door): "Clear conversations" collapses the replied rows; "Show hidden" reveals the dimmed archive; × inside the archive still hard-deletes; Mark all read / Clear read behave as before.

## Follow-ups

- #139 priority: 077 suite test with a hidden proof row (invariant becomes structural).
- #140 archive window (pagination or raised take) — with 087.
- #141 ADR (hide-vs-delete + UI-only invariant), user-gated; #142 web-side ?includeHidden pin.
- 096's Clear read stays the hard-delete path for spendable read rows; the two buttons now split cleanly.

<log>
- 2026-09-29 [agent] — result drafted after architect APPROVED-WITH-WARNINGS + live e2e; feature moves to the user's result-validation gate.
</log>