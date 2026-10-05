---
feature: 092
slug: waits-for-closed-blockers-bug
status: validated
created: 2026-10-04
validated: 2026-10-05
---

# Result 092 — Bug: "waits for" shows done/review blockers as waiting (feature page)

## Outcome

Feature dependencies panel no longer reads as «waiting» when blockers are done/review: open blockers stay in «Waits for» (danger chips, «N open» badge, X), closed blockers moved to a separate «Cleared by» group (muted chips, line-through, link + X kept for history cleanup, excluded from the waiting badge). Empty-waits-with-cleared renders «nothing open»; total absence renders «nothing yet». Blocks group untouched; add-blocker flow intact; remove works from both groups through one handler. Clean logic extracted to `lib/feature-blocker-groups.ts` (house precedent: entry-read/board-card-context), component consumes it.

## Verification

- Light review PASS-WITH-NITS (audits/review-092-01.md): 0 blockers; split semantics, regress (blocks/add/remove flows), a11y (X buttons), style/AI-trace verified; AC 2 ticked by the reviewer.
- Gates re-run by the reviewer: lint 0 errors (6 pre-existing in untouched files), tsc clean, npm test 90/0 (9 new).

## Follow-ups (microbatch queue)

- NIT: `aria-label="Stop waiting for …"` on cleared chips is semantically off (history cleanup, not waiting) — one-line fix via `entry.closed` in BlockerChip (#173).
- Observation: component render wiring pinned only via the pure lib (house convention) — noted.

## Post-release click-check

A feature whose blockers are all done/review shows «Cleared by» with muted struck-through chips and no open-badge; a mixed feature shows both groups.

<log>
- 2026-10-04 [agent] — result drafted at close: outcome, verification (review PASS-WITH-NITS 0 blockers, gates 90/0), nit to the microbatch ledger, click-check.
</log>