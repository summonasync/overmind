---
feature: 095
slug: os-desktop-notifications
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 095 — Attention surfaces: OS desktop toasts + web sound + browser tab attention для approval/question

> Grooming 2026-10-04. 2 tasks, 01 → 02. Lands AFTER 107 (attention composes through its title owner).

## Task chain

1. **01 — Attention module + surfaces.** One client module over the 093 pipeline: hidden-only OS toast (Web Notifications, permission via settings toggle, 101-hierarchy body, click → deep-link focus), hidden-only chirp (Web Audio), favicon swap + title «(N)» prefix composed through the 107 owner; clears on focus/open. Settings page section (notifications toggle + permission state). Web tests: trigger matrix (hidden/focus × kind), permission states rendering, attention compose/clear.
2. **02 — Closeout.** Consistency (bubble vs toast vs title — one event source, no duplicate noise); architect light; result with post-release click-check (reply to an approval from another window: toast + chirp + favicon/title, click focuses and deep-links).

## Verification

- Web trigger matrix tests (01), gates; architect light (02).

<log>
- 2026-10-04 [agent] — plan groomed: 2 tasks; 107 dependency recorded.
- 2026-10-04 [agent] — validated with human proof: notification 14b5d9b4-afc2-42c8-b567-66590bcc20ed replied 2026-10-04 12:12:56 UTC
</log>