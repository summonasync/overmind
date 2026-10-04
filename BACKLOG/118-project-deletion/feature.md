---
feature: 118
slug: project-deletion
status: draft
priority: p2
version: 1
created: 2026-10-04
---

# Feature 118 — Project deletion path

<goal>
Overmind has no way to remove a project from the system — the only exits are leaving it on the board forever or hand-hacking the DB. A project needs a deliberate delete path with the same honesty discipline as 094 artifact deletion.
</goal>

<context>
User intake 2026-10-04: «Кстати у нас нет функции удаления проекта из системы)». Open questions for grooming: soft delete (094 pattern: DeletedAt + query filter) vs hard delete with cascade inventory (entries, issues, notifications, agent sessions, claims, vector embeddings in Qdrant, materialized BACKLOG/ tree on disk); what happens to agent claims on a dying project; MCP surface (new destructive tool?); human door placement (project settings, confirm flow); foreign projects holding issues authored by the deleted project (028 author-provenance).
</context>