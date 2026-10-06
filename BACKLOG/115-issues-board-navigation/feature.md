---
feature: 115
slug: issues-board-navigation
status: done
priority: p1
version: 1
created: 2026-10-04
---

# Feature 115 — Issues Board: third project tab + cross-project sidebar view (new/unread first)

<goal>
Issues Board navigation per user dogfood feedback 2026-10-04: (1) the project page gains a THIRD tab — dashboard, entries, issues board (board-scoped, as today); (2) the sidebar Issues Board item stays, but WITHOUT a project context it shows issues from ALL projects — new/unread ones surfaced first. Cross-project list needs a backend delta (issues are project-scoped today; the agent door already has projectId-less id routes, but no cross-project LIST).
</goal>

<context>
User mid-session message: «issues board - оно в левом меню, прикольно, НО - оно отправляет к проектам, открываешь проект - у него 2 табы - dashboard, entries - логично, что третья таба должна быть - issues board. А вот слева в меню issues board - можно оставить, НО - без проекта там показывать заведённые issues на все проекты (новые/непрочитанные)».
</context>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
- 2026-10-05 [agent] — Van by claude claimed the feature
- 2026-10-05 [auto] — all tasks done + spec + plan validated → review
- 2026-10-06 [agent] — Van by claude took over stale claim of Van by claude
- 2026-10-06 [agent] — done via agent review — approved by human tap (notification 2607f4b6, reply 2026-10-05 21:57:27 UTC)
- 2026-10-06 [agent] — claim released on done-close
</log>
