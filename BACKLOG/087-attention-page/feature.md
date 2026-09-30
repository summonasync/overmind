---
feature: 087
slug: attention-page
status: done
priority: p1
version: 1
created: 2026-09-26
---

# Feature 087 — Attention-страница в левом меню: все вопросы и attention агентов в одном месте (не только колокольчик)

<goal>
The bell modal is too small to be the single surface for human actions. A dedicated page in the left menu aggregates ALL agent questions and attention requests (approval/question notifications, validation-proof waits, blocked-by-user states) in one scannable list — the user lands, sees everything awaiting their call, acts inline or jumps via direct link.
</goal>

<requirements>
- Left-menu entry (e.g. "Attention" / "Требуют внимания" — naming pass at spec) with an unread/pending count badge.
- Page: full-height list of pending human actions, newest-first, richer than the bell modal — full text, agent, age (post-086 time fix), target context (project/feature/tab), inline reply/approve (077-proof-aware) and direct deep link.
- Grouping/filtering by project or kind; answered items move to a collapsed history section (not silently dropped — ties into the 084 clear-notifications request).
- Real-time freshness via the existing 017 polling.
- Overlaps with 084 (channels of visibility + actionability) — the page is its primary surface; 084 keeps OS-level/banner visibility ideas and clear-notifications lifecycle. Spec pass will carve the boundary precisely.
</requirements>

<non-goals>
- Multi-user/auth; mobile; backend notification model changes unless the spec proves them necessary.
</non-goals>

<refs>
- 084-notification-human-actions (sibling — visibility channels, clear-notifications; the page this feature builds is 084's primary surface).
- 020 (notification entity + doors), 077 (approve = proof), 017 (live sync), 064/024 (web patterns).
- User request 2026-09-26: «отдельный ui слева в меню, страница со всеми вопросами и attention агентов ко мне — чтобы не только маленький колокольчик».
</refs>

<log>
- 2026-09-29 [auto] — artifact created → inProgress
- 2026-09-29 [auto] — all tasks done + spec + plan validated → review
- 2026-09-29 [agent] — [agent] by user request in-session (ok в чате, 2026-09-29): result validated, все таски done 13/13 AC, architect light APPROVED-WITH-WARNINGS + fix round, live e2e пройден, код закоммичен 54cca56.
</log>
