---
feature: 001
slug: overmind-ceremony
status: validated
created: 2026-09-24
validated: 2026-09-24
---

# Plan 001 — Overmind ceremony migration: единый знаменатель для планирования

<approach>
Kimi-сторона (.kimi-code/) — мастер-эдиты делает основной агент (максимум контекста по дизайну). Порты на Claude/Antigravity/Qwen — coder-субагенты (K2.7), брифинг = этот артефакт + SYNC-STRUCTURE.md translation tables. Каждая таска завершается верификацией из acceptance-criteria.
</approach>

<steps>
1. T1 Repo cleanup: ветка feature/overmind-ceremony; manifest.json минус 4 BACKLOG-записи; удаление .kimi-code/BACKLOG+TASKS, .claude/BACKLOG+TASKS, root TASKS/; снос старых BACKLOG/001-003 (решение: чистый старт); tombstone root BACKLOG.md.
2. T2 overmind-ceremony: skills/overmind-ceremony/SKILL.md + agents/overmind-ceremony.md ×4 стороны.
3. T3 Core contract: AGENTS.md / CLAUDE.md / GEMINI.md / QWEN.md — Overmind Integration, doc table, workflow rules 7-10, sub-agent contract.
4. T4 SKILLFLOW.md ×4 — маршруты на overmind-артефакты, Simple = фича с одной таской, удаление superpowers-имён.
5. T5 task-orchestrator SKILL.md ×4 — Start-of-Work с борды, Phase 0 = planning_validate гейты, брифинг субагентов artifactId.
6. T6 audit-orchestrator ×4 (состояние с борды) + 11 лёгких скиллов ×4 (doc-reading lists).
7. T7 SYNC-STRUCTURE.md (mapping, parity 22+24, superpowers-строки out) + README.md + полный pre-commit чек-лист.
</steps>

<verification>
Per-task AC в task-артефактах. Финальный гейт: contamination greps пустые, count parity 22+24, install.sh --dry-run чистый, materialized BACKLOG/001-overmind-ceremony/ соответствует борде.
</verification>

<log>
- 2026-09-24 — plan validated — approved by user in-session together with spec (ExitPlanMode, 2026-09-24)
</log>
