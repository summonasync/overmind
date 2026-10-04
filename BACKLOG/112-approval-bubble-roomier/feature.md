---
feature: 112
slug: approval-bubble-roomier
status: inProgress
priority: p1
version: 1
created: 2026-10-04
---

# Feature 112 — Approval bubble: up to 800 chars of text, bigger window

<goal>
Approval bubble gets roomier (user dogfood feedback 2026-10-04): the window grows and carries up to 800 characters of the notification text instead of the current 4-line clamp — approvals/questions are context-heavy and the bubble is where the human decides.
</goal>

<context>
approval-bubble.tsx:84 clamps the text with line-clamp-4; the server allows up to 2000 chars per notification text. User: «может быть больше и в нём может быть больше текста. До 800 символов легко».
</context>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
</log>
