---
feature: 002
slug: superpowers-gap-closure
status: validated
created: 2026-09-24
validated: 2026-09-24
---

# Plan 002 — Superpowers gap closure: миграция функциональных техник в кит

<approach>
Мастер-правки делает основной агент (по образцу 001 — максимум контекста у оркестратора): Kimi-сторона мастер, порты по translation tables SYNC-STRUCTURE.md. squeezr-скилл — каноническое тело из .claude/skills/squeezr на все стороны. Каждая таска завершается своей grep/счётчик-верификацией; финальный гейт — полный pre-commit checklist. Никаких коммитов.
</approach>

<steps>
1. T1 Parallel dispatch: overmind-ceremony SKILL.md Techniques (мастер .kimi-code → порты ×3) + task-orchestrator Phase 2 строка ×4.
2. T4 (объединён с T1 файлово, но отдельная таска) Workspace isolation: overmind-ceremony Techniques + capability matrix 063 interim ×4.
3. T3 Receiving-review: task-orchestrator Phase 4 ×4.
4. T5 Skill-check: Development Workflow правило 0 в AGENTS/CLAUDE/GEMINI/QWEN.md ×4.
5. T2 Authoring rules: секция в SYNC-STRUCTURE.md (один файл).
6. T6 Squeezr promotion: скилл ×4 + manifest.json записи + SYNC-STRUCTURE (mapping, parity 22+25, benign-исключение) + секция навигации в core-файлах ×4.
7. T7 Final gate: все 5 contamination greps, parity, frontmatter grep, install.sh --dry-run, result-артефакт.
</steps>

<verification>
Per-task AC в task-артефактах (greps + diff против мастера). Финальный гейт: полный pre-commit checklist SYNC-STRUCTURE.md зелёный; materialized BACKLOG/002-superpowers-gap-closure/ соответствует борде.
</verification>

<log>
- 2026-09-24 [agent] — plan drafted; execution order: file-grouped (overmind-ceremony → task-orchestrator → core files → SYNC-STRUCTURE → squeezr → gate)
</log>
