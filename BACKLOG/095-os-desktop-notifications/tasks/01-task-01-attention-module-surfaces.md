---
feature: 095
task: 01
slug: os-desktop-notifications
status: todo
review: light
created: 2026-10-04
brief: Web: attention module — hidden-only OS toast + chirp + favicon/title attention through the 107 owner + tests
---

# Task 01 — Attention module + surfaces

<goal>
Approvals/questions reach the user when the tab is hidden — toast, chirp, tab attention.
</goal>

<scope>
- One client module over the 093 pipeline: hidden-only OS toast (Web Notifications API; permission via a settings-page toggle «Desktop notifications» with granted/denied/blocked states; body per the 101 hierarchy — project prominent, feature/agent secondary; click focuses the tab + follows the 093 deep-link url).
- Hidden-only chirp (Web Audio, built-in, settings toggle, no asset pipeline).
- Tab attention: favicon swap to an attention variant + «(N)» title prefix composed THROUGH the 107 title owner (third input: attention count); clears on tab focus / after opening the notification; no private blink loop.
- Web tests: trigger matrix (hidden/focus × approval/question), permission-state rendering, attention compose/clear.
</scope>

<anti-scope>
- Native/Electron layer; per-project sound; non-attention kinds; mobile push.
</anti-scope>

<acceptance-criteria>
- [ ] Trigger matrix pinned (no duplicates in focus; re-raise while hidden).
- [ ] Permission states rendered; settings toggles wired.
- [ ] Attention composes through the title owner; clears on focus/open — pinned.
- [ ] Gates green (lint/tsc/node test).
</acceptance-criteria>

<refs>
- Spec 12623237; plan 6b95d68b (task 1); 107 title owner; 093 url contract; 087/093 pipeline.
</refs>