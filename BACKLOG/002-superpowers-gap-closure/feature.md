---
feature: 002
slug: superpowers-gap-closure
status: review
priority: p0
version: 1
created: 2026-09-24
---

# Feature 002 — Superpowers gap closure: миграция функциональных техник в кит

<goal>
Закончить снятие зависимости от superpowers-плагина: фича 001-overmind-ceremony вычистила тексты и перенесла 5 inline-техник, но функциональный слой, живший только в плагине (parallel dispatch, skill-authoring craft, receiving-review discipline, workspace isolation, skill-check forcing function), в ките замены не имеет. Закрыть эти 5 дыр crew-owned правилами. После этого пользователь сносит плагин из Claude-рантайма и секцию Superpowers Integration из глобального ~/.claude/CLAUDE.md (human-only действия, вне скоупа агента).
</goal>

<requirements>
- Parallel dispatch: техника в overmind-ceremony (Techniques) + ссылка из task-orchestrator Phase 2 — ×4 стороны.
- Skill-authoring craft: секция авторинга в SYNC-STRUCTURE.md (корневой файл, один) — конденсированная замена superpowers:writing-skills.
- Receiving-code-review: дисциплина «verify, don't obey» в task-orchestrator Phase 4 Fix Loop — ×4.
- Workspace isolation: правило в overmind-ceremony (техника + interim-правило к capability matrix 063) — ×4.
- Skill-check gate: правило «skill check first» в Development Workflow ядра (AGENTS/CLAUDE/GEMINI/QWEN.md) — замена forcing function плагина — ×4.
- Все правки по quadruple-edit контракту SYNC-STRUCTURE.md (translation tables, contamination greps, parity 22+24).
</requirements>

<non-goals>
- НЕ сносим плагин и НЕ правим ~/.claude/settings.json / глобальный ~/.claude/CLAUDE.md — конфиг пользователя, human-only.
- НЕ решаем судьбу .claude/skills/squeezr — отдельный follow-up фичи 001.
- НЕ коммитим — no auto-commits, коммит ветки по явной просьбе пользователя.
- НЕ трогаем церемониальный цикл и MCP-контракты — они прошли 001.
</non-goals>

<log>
- 2026-09-24 [auto] — artifact created → inProgress
- 2026-09-24 [auto] — artifact created → inProgress
- 2026-09-24 [auto] — all tasks done + spec + plan → review
</log>
