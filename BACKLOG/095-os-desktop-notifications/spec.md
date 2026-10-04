---
feature: 095
slug: os-desktop-notifications
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 095 — Attention surfaces: OS desktop toasts + web sound + browser tab attention для approval/question

> Grooming 2026-10-04 (user in session, fast pass). Conservative v1 on browser APIs only — no Electron/native layer (challengeable: user flagged this as the research-heavy one; defaults below are the boring-working choice).

## Goal

When the user is NOT looking at the tab, approval/question events must reach them three ways: an OS-level toast (Web Notifications API), a short sound, and browser-tab attention (favicon swap + title prefix). In-focus, the existing approval bubble (093) is enough — no duplicate noise.

## Decisions

- **Trigger:** approval and question notifications only, while `document.hidden` (in-focus → bubble only, unchanged). One surface per event — no re-notify spam; a new event while hidden re-raises.
- **OS toasts:** Web Notifications API — permission asked once via a settings toggle (global settings page: «Desktop notifications», states granted/denied/blocked gracefully rendered); toast body follows the 101 hierarchy (project prominent, feature/agent secondary), click focuses the tab and follows the existing notification deep-link (093 url contract).
- **Sound:** one short built-in chirp (Web Audio, no asset pipeline), settings toggle, plays only while hidden.
- **Tab attention:** favicon swaps to an attention variant + title gets an «(N) » pending-prefix — composed THROUGH the 107 single title owner (attention becomes a third input: f(projectName, awaitingCount, attentionN)); clears on tab focus / after opening the notification. No custom blink loop that fights the owner.
- **Where it lives:** one client module subscribed to the same events the bubble uses (087/093 pipeline); no backend changes, zero migrations.

## Non-goals

- Native/Electron layer; per-project sound choice; sound/notification for non-attention kinds; mobile push.

<log>
- 2026-10-04 [agent] — groomed fast with the user in session: browser APIs only, hidden-only triggering, one module over the 093 pipeline, attention composed through the 107 title owner. Platform-native layer explicitly deferred (challengeable).
- 2026-10-04 [agent] — validated with human proof: notification 14b5d9b4-afc2-42c8-b567-66590bcc20ed replied 2026-10-04 12:12:56 UTC
</log>