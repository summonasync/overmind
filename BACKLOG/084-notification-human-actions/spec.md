---
feature: 084
slug: notification-human-actions
status: validated
created: 2026-09-28
validated: 2026-09-28
---

# Spec 084 — Notification center: human actions (approval/question) — видимость, прямые ссылки на overmind, actionability

## Decisions (Socratic round 2026-09-28, user-approved)

- **Scope split.** 084 owns PERSISTENT visibility of unresolved approvals + discipline setting + lifecycle. The event-driven toast when a notification ARRIVES and the agent-side deep-link quoting belong to 093. OS-level desktop notifications belong to 095. Both are anti-scope here.
- **Surface: zerling speech bubble.** Non-blocking dialog-cloud pinned to a corner (not a full-width bar): a zerling mascot (pose picked at implementation; candidates `menacing`/`yelling` from the existing 10-pose sprite set) + "N agents await your call". Appears while any approval is unresolved; question kind does NOT trigger the bubble (bell state + 087 page cover it).
- **Bubble = action surface.** Shows the OLDEST unresolved approval with inline Approve/Reject buttons (same mutations and canonical approved/rejected replies as 088 bell quick actions — 077 proof compatibility preserved) + an "Open →" link to the target feature. With multiple pending: a badge «ещё N → open approvals» linking to the notification center filtered by approvals. NO carousel: deciding the shown one never pops the next — the badge count updates; if the bubble is open, content refreshes in place.
- **Bell state.** The bell gains a distinct "awaiting your call" state (in addition to the unread counter) while any approval is unresolved. Cheap companion to the bubble.
- **Discipline setting (server-side, SettingsApi — not localStorage: user works from two machines).** New settings: nudge mode = `quiet` | `nag` | `mini` (default `quiet`), plus `nagIntervalMinutes` (used by `nag`). Semantics: `quiet` — a dismissed bubble returns only when a NEW approval arrives; `nag` — the bubble re-pops every N minutes while the approval stays unresolved; `mini` — dismissing collapses to a small sleeping-zerling corner icon that re-expands on click (occupies the corner, never pops).
- **Notification center filter.** The center gains a kind filter (at minimum approvals) — the landing target of the «ещё N» badge.
- **Lifecycle.** (A) Dead targets: bubbles/list dim an approval whose featureSlug is absent from the cached feature list (no new API) — «target gone», buttons still work. (B) TTL: nag surfaces (bubble) only consider approvals younger than 14 days (constant, no setting); the center keeps the honest full archive. (C) Agent-side cancel tool — anti-scope (020 contract not extended).

## Requirements

- Web: bubble component + visibility state machine (quiet/nag/mini), bell awaiting-state, center kind filter, dead-target dimming, 14-day TTL gate on nag surfaces. Zerling sprite reuse via shared/ui/zerling.
- Backend: settings fields (mode + interval) with validation, round-trip through the existing settings endpoints.
- Approve/Reject inside the bubble reuse the existing notification reply mutations and canonical reply texts (088/077) — no new write paths.
- Verification: backend settings round-trip tests; web component tests for the state machine (quiet/nag/mini transitions, FIFO pick, in-place refresh, TTL gate, dead-target dimming); lint + tsc + build green; live walk of the whole loop on an isolated host (notify → bubble → approve → bubble clears).

## Non-goals

- Event-driven arrival toast + agent deep-link quoting in chat (093).
- OS-level desktop notifications (095).
- Agent-side cancel/void tool (020 contract unchanged).
- Auto-read on view (settled in 084-pre: read comes only from an action).
- Multi-user/auth, mobile, push to external services.

<open-questions>
- none — every fork resolved with the user 2026-09-28 (scope split, bubble form, discipline modes, multi-approval behavior, lifecycle A+B)
</open-questions>

<refs>
- 020-agent-notifications (bell, center, reply path, deep links).
- 088-bell-approval-quick-actions (button + mutation reuse).
- 077-validation-provenance (reply = proof).
- 087-attention-page (all-kinds summary page — no overlap, bubble is approval-only nag).
- 093-approval-deep-link-toast (arrival toast — anti-scope here), 095-os-desktop-notifications (OS — anti-scope here).
- 084-pre task 6cf21335 (read-on-approve — shipped, feeds the bubble's cleared-on-action behavior).
</refs>

<log>
- 2026-09-28 [agent] — validated with human proof: notification 6a9d9455-58f7-45f1-9c16-55e38725b1a3 replied 2026-09-28 00:18:53 UTC
- 2026-09-28 [agent] — [agent] hard-review finding #1 resolution: lifecycle (A) partially descoped at implementation — dead-target dimming ships in the BUBBLE only; the center list keeps the honest full archive (dimming there would require a per-notification board query inside the modal, and the center is not a nag surface). User smoke validated bubble-only dimming. Descope recorded in result Deviations; plan T2/T3 scope narrowing made this silent — this log line makes it explicit.
</log>
