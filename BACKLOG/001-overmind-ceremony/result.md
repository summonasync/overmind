---
feature: 001
slug: overmind-ceremony
status: draft
created: 2026-09-24
---

# Result 001 — Overmind ceremony migration: единый знаменатель для планирования

<outcome>
Кит полностью переведён на Overmind-церемонии. Superpowers удалён из всех 4 сторон (0 резидуальных ссылок вне намеренных). Создан скилл `overmind-ceremony` (×4: skill + agent) — владелец ceremony loop, гейтов, capability matrix и sub-agent briefing contract; техники superpowers перенесены в него inline. Переписаны: AGENTS/CLAUDE/GEMINI/QWEN.md (Overmind Integration, workflow rules 7-10), SKILLFLOW.md ×4, task-orchestrator ×4, audit-orchestrator ×4 + 11 light skills ×4. Инфра: manifest.json (-4 BACKLOG deploy), _TEMPLATE и legacy TASKS/ удалены, root BACKLOG.md = tombstone, SYNC-STRUCTURE.md и README.md синхронизированы.
</outcome>

<verification>
- Contamination greps ×5: пустые (две задокументированные benign-исключения в SYNC-STRUCTURE).
- Count parity: 22 agents + 24 skills на Kimi/Gemini/Qwen; Claude=25 skills из-за stray untracked `.claude/skills/squeezr` (не кит, оставлено, решение за пользователем).
- Frontmatter YAML grep: пустой. install.sh --dry-run: чистый.
- Догфуд пройден: вся фича велась через planning_* тулы; materialized BACKLOG/001-overmind-ceremony/ соответствует борде (feature/spec/plan/7 tasks).
</verification>

<deviations>
- Импорт старых фич 001-003 отменён пользователем (чистый старт) — контент в git-истории.
- Antigravity-сторона верифицирована только статически (рантайм не установлен на машине).
- humanGate=off: spec/plan валидировались агентом с provenance-логом (аппрув плана в сессии).
</deviations>

<follow-ups>
- Решить судьбу `.claude/skills/squeezr` (untracked, ломает parity-счёт).
- Когда на борде Overmind дойдут 030 (dependencies), 029 (entry tombstones), 044 (batch ops), 058 (agent import), 063 (worktree binding) — обновить capability matrix в overmind-ceremony и соотв. правила в AGENTS.md.
- Пользователь: закоммитить ветку feature/overmind-ceremony и прогнать install.sh для деплоя в homes.
</follow-ups>