---
feature: 001
slug: overmind-ceremony
status: done
priority: p0
version: 2
created: 2026-09-24
---

# Feature 001 — Overmind ceremony migration: единый знаменатель для планирования

<goal>
Привести кит агентов (4 рантайма: Kimi/Claude/Antigravity/Qwen) к единому знаменателю: Overmind MCP — единственный источник правды для церемоний планирования, статусов и артефактов. Устранить коллизии трёх систем: superpowers plugin + файловый BACKLOG + overmind.
</goal>

<requirements>
- Новый скилл `overmind-ceremony` (skill dir + agent pointer ×4 стороны): ceremony loop, гейты, capability matrix (проверено по борде Overmind), sub-agent briefing contract, inline-техники взамен superpowers (Socratic questioning, plan decomposition, root-cause debugging, verification-before-completion, TDD scope).
- Superpowers удаляется из кита ПОЛНОСТЬЮ: AGENTS/CLAUDE/GEMINI/QWEN.md, SKILLFLOW.md ×4, task-orchestrator ×4, SYNC-STRUCTURE.md, README.md, упоминания в 6+ скиллах.
- Overmind MCP = hard requirement; без MCP — STOP. Antigravity-сторона: пометка configure manually.
- Дисковый BACKLOG/ = read-only materialized mirror; root BACKLOG.md и legacy TASKS/ получают tombstone; _TEMPLATE-шаблоны удаляются (форматом владеет materialization); manifest.json теряет BACKLOG deploy-записи.
- Старое состояние репо НЕ импортируется (решение пользователя 2026-09-24): чистый старт борды, эта фича = 001.
- Quadruple-edit rule: все изменения зеркалятся на 4 стороны в одном коммите (SYNC-STRUCTURE.md).
</requirements>

<non-goals>
- Импорт старых фич 001-003 (dual-tool-repo-layout, antigravity-support, git-commit-ai-detector) — отменён пользователем, git-история сохраняет контент.
- Изменение формата XML-секций артефактов — он принадлежит Overmind materialization.
- Настройка overmind MCP в рантаймах — это среда пользователя, не репо.
</non-goals>

<log>
- 2026-09-24 [auto] — artifact created → inProgress
- 2026-09-24 [auto] — all tasks done + spec + plan → review
- 2026-09-24 [ui] — status moved to 'done' via ui
</log>
