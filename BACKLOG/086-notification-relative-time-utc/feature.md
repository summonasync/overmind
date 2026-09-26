---
feature: 086
slug: notification-relative-time-utc
status: done
priority: p1
version: 1
created: 2026-09-26
---

# Feature 086 — Bug: relative time в колокольчике рендерит UTC как локальное — свежие нотификации «3h ago»

<goal>
Expected: a notification created seconds ago renders as "just now"/"1m ago" in the notification center. Actual: it renders "3h ago" (user report 2026-09-26, live). Cause: createdAt arrives UTC-marked ("2026-09-26T16:22:05Z"), but the relative-time formatter treats it as local wall-clock — the user's UTC+3 offset becomes a fake 3-hour age.
</goal>

<requirements>
- Find the formatter used by the notification center (web notification slice) and fix UTC parsing (Date parsing must respect the Z marker; no string slicing that drops it).
- Sweep ALL relative-time renders in web/ for the same defect (notification center, board "updated X ago", feature workspace age line from 024) — same class, one fix.
- Pin: a test that renders a "now - 10s" UTC timestamp and asserts "just now"/sub-minute output.
</requirements>

<non-goals>
- Reworking age formatting rules from 024 (days >24h, hours <24h stay); notification feature work (084).
</non-goals>

<refs>
- User report 2026-09-26 in-session (live bell check during the 077 proof flow).
- 020 (notification center), 024 (age formatting helper).
</refs>

<log>
- 2026-09-26 [auto] — artifact created → inProgress
- 2026-09-26 [agent] — by user request in-session («го флип, коммит, деплой)» — batch 086/088/070: 2/2 таска done, оба ревью PASS-WITH-NITS с применёнными фиксами (W1 project-binding + вакуумный пин), result 3eb7abb7, web-тесты 16/16
</log>
