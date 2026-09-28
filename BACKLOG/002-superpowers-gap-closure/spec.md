---
feature: 002
slug: superpowers-gap-closure
status: validated
created: 2026-09-24
validated: 2026-09-24
---

# Spec 002 — Superpowers gap closure: миграция функциональных техник в кит

<goal>
Закрыть 5 функциональных дыр, оставшихся после фичи 001 (техники, жившие только в superpowers-плагине), и интегрировать squeezr в кит как первый класс — вторая опора связки «overmind + squeezr». После закрытия удаление плагина из Claude-рантайма и секции Superpowers Integration из глобального ~/.claude/CLAUDE.md (human-only, вне скоупа агента) теряют последние препятствия.
</goal>

<scope>
1. Parallel dispatch (замена superpowers:dispatching-parallel-agents): техника в overmind-ceremony «Techniques» + строка-ссылка в task-orchestrator Phase 2 — ×4 стороны.
2. Skill-authoring craft (замена superpowers:writing-skills): секция авторинга в SYNC-STRUCTURE.md (корневой файл, один на кит; verification-половина уже есть как pre-commit checklist).
3. Receiving-review discipline (замена superpowers:receiving-code-review): правило «verify, don't obey» в task-orchestrator Phase 4 Fix Loop — ×4.
4. Workspace isolation (замена superpowers:using-git-worktrees): техника в overmind-ceremony + обновлённое interim-правило capability matrix 063 — ×4.
5. Skill-check gate (замена superpowers:using-superpowers forcing function): правило 0 в Development Workflow ядра (AGENTS/CLAUDE/GEMINI/QWEN.md) — ×4.
6. Squeezr promotion: untracked stray `.claude/skills/squeezr` → полноценный кит-скилл: копия ×4 стороны, manifest.json deploy-записи (mirror ×3 + merge для qwen), SYNC-STRUCTURE mapping-строки + parity 24→25 + benign-исключение для greps (скилл намеренно называет несколько рантаймов), компактная секция «Squeezr — code navigation» в 4 core-файлах (когда какой тул, не-индексированный репо → сказать, не молча грепать).
</scope>

<anti-scope>
- НЕ трогаем ~/.claude/settings.json, глобальный ~/.claude/CLAUDE.md, плагины — конфиг пользователя, human-only.
- НЕ поднимаем squeezr-индекс/сервисы и НЕ регистрируем MCP без явной просьбы пользователя (скилл сам требует explicit request, секция 0).
- НЕ добавляем агентов и НЕ меняем crew-роли — parity агентов 22 остаётся; скиллы 24→25.
- НЕ коммитим — no auto-commits.
</anti-scope>

<constraints>
- Quadruple-edit: каждое семантическое изменение на 4 стороны в одном батче, по translation tables SYNC-STRUCTURE.md; mcp__overmind__* и squeezr MCP-имена не переводятся.
- Формула упоминания superpowers — только «replaces superpowers:X» в overmind-ceremony Techniques (единый стиль 001); в остальных файлах — ноль упоминаний.
- Контент техник crew-owned и самодостаточен: правило читается без знания о существовании плагина.
- Squeezr-скилл на 4 сторонах — каноническое тело (CLI/MCP одинаковые), кросс-рантаймовые упоминания (Claude-хук пример, qwen-заметка) остаются в теле verbatim и покрываются benign-исключением в чек-листе.
</constraints>

<acceptance-criteria>
- [ ] Parallel dispatch: техника в 4 копиях overmind-ceremony SKILL.md (batch spawn, file-disjoint иначе sequential, failure в obstacles без отмены siblings) + ссылка из task-orchestrator Phase 2 ×4
- [ ] Authoring rules: секция в SYNC-STRUCTURE.md (description-как-триггер, один скилл = одна работа, cold-reader стиль, anti-scope, done = pre-commit checklist + dogfood)
- [ ] Receiving-review: правило в Phase 4 ×4 — каждый BLOCKER воспроизводится до фикса, неверный коммент отбивается доказательством, никакого performative agreement
- [ ] Isolation: техника в overmind-ceremony ×4 + interim-правило 063 в matrix ×4 (branch feature/<slug>, параллельные писатели не делят checkout, git worktree где нет нативного тула)
- [ ] Skill-check: правило «0. Skill check first» в Development Workflow ×4 core-файла
- [ ] Squeezr: скилл-директория существует на 4 сторонах с идентичным SKILL.md; manifest.json содержит 4 деплой-записи и проходит dry-run; SYNC-STRUCTURE несёт 4 mapping-строки, parity 22+25, benign-исключение для squeezr-скилла в greps
- [ ] Core-файлы ×4: компактная секция squeezr-навигации (сценарии тулов + «нет в индексе → сказать»), переведённая по сторонам
- [ ] Все 5 contamination greps пустые (с benign-исключениями incl. squeezr); parity 22+25; frontmatter grep пустой; install.sh --dry-run чистый
</acceptance-criteria>

<refs>
- Фича 001-overmind-ceremony (result-артефакт) — базовая миграция, известные follow-ups
- SYNC-STRUCTURE.md — quadruple-edit контракт, translation tables, pre-commit checklist
- .claude/skills/squeezr/SKILL.md — исходник промоушена (мастер-тело)
</refs>

<log>
- 2026-09-24 [agent] — spec drafted from the in-session gap analysis (5 functional gaps), content approved by user in chat
- 2026-09-24 [agent] — v2: scope +6 squeezr promotion by user question in-session («что у нас со squeezr интеграцией — нужно что-то добавить?»); anti-scopeExplicit: индекс/сервисы не поднимаем без явной просьбы
</log>
