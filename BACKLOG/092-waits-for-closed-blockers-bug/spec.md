---
feature: 092
slug: waits-for-closed-blockers-bug
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 092 — Bug: "waits for" shows done/review blockers as waiting (feature page)

> Grooming 2026-10-04 (user in session). Source: the p1 bug feature on the board; backend is correct — `closed` arrives per entry; this is a UI-semantics bug.

## Goal

Feature page renders one «Waits for» group for ALL blockedBy entries, including done/review blockers. Cleared chips are muted + line-through (good), but the group heading still reads «Waits for» — a fully-cleared feature still LOOKS like it is waiting. The open-count badge is computed correctly (`openBlockers`), the heading is not.

## Decisions

- **Split the group by closure state.** Open blockers stay in «Waits for» (danger chips, «N open» badge, X remove). Closed blockers move to a separate «Cleared by» group: muted chips (keep line-through + link + X for history cleanup), no danger styling, no count in the waiting badge. Empty «Waits for» with non-empty «Cleared by» renders «nothing open» instead of «nothing yet».
- Web-only change (one component + test). No API/domain movement — `closed` already carries the truth.

## Non-goals

- Auto-removing closed blockers server-side (history stays visible by design); touching the Blocks group (same closed treatment already applied there).

<log>
- 2026-10-04 [agent] — groomed with the user in session: split by closure state, closed → «Cleared by» muted group; web-only.
- 2026-10-04 [agent] — validated with human proof: notification 59f6b203-1997-4e09-8407-b3d01683dfdb replied 2026-10-04 11:48:54 UTC
</log>