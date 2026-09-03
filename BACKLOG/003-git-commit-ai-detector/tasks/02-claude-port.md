---
feature: 003
task: 02
slug: claude-port
status: done
review: light
created: 2026-09-01
---

# 003/02 — Claude port of git-commit-ai-detector

<goal>
На Claude-стороне существуют `.claude/skills/git-commit-ai-detector/SKILL.md` и
`.claude/agents/git-commit-ai-detector.md` — семантически идентичные Kimi-мастеру из задачи 01.
</goal>

<context>
Triple-edit contract (SYNC-STRUCTURE.md): одна команда — три рантайма. Порт механический:
берём файлы из `.kimi-code/skills/git-commit-ai-detector/SKILL.md` и
`.kimi-code/agents/git-commit-ai-detector.md` (результат задачи 01) и применяем translation table
«Kimi → Claude» из `SYNC-STRUCTURE.md` строго в порядке таблицы.
</context>

<scope>
- Create: `.claude/skills/git-commit-ai-detector/SKILL.md`
- Create: `.claude/agents/git-commit-ai-detector.md`
</scope>

<anti-scope>
- `.kimi-code/` источник — read-only для этой таски; найденная ошибка в мастере = запись в <obstacles>, не правка.
- `.gemini/` — задача 03, не эта.
- SYNC-STRUCTURE.md, BACKLOG.md, manifest.json — не эта таска.
- Никаких git commit/push.
</anti-scope>

<constraints>
- Translation table Kimi → Claude из SYNC-STRUCTURE.md, правила 1–16, строго по порядку:
  ключевые для этого контента: `~/.kimi-code/skills` → `~/.claude/skills`; `~/.kimi-code/` → `~/.claude/`;
  `Kimi K3`/`K3` → `Claude Opus`/`Opus`; `K2.7` → `Sonnet`; `Kimi Code CLI` → `Claude Code`;
  `Agent tool` → `Task tool`.
- Model policy в порте: «ALWAYS executed by Opus, do not run on Sonnet» (семантика строки про K3/K2.7).
- Контент скилла не переписывается по смыслу — только трансляция идентификаторов/путей/моделей.
</constraints>

<acceptance-criteria>
- [ ] Оба файла созданы, структура секций 1:1 с Kimi-мастером.
- [ ] `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm' .claude/skills/git-commit-ai-detector/ .claude/agents/git-commit-ai-detector.md` — пусто.
- [ ] В порте: model policy = Opus, пути `~/.claude/skills/...`, ссылка на ai-code-detector указывает на `.claude` сторону.
- [ ] Стоп и репорт списка изменённых файлов.
</acceptance-criteria>

<refs>
- @../spec.md — <requirements> (triple-edit deliverables)
- @../plan.md — Task 02
- @SYNC-STRUCTURE.md — translation table Kimi → Claude
- tasks/01-kimi-master-skill-agent.md — источник контента (должна быть done)
</refs>

<obstacles>
<!-- Заполняется исполнителями ПО ХОДУ работы -->
</obstacles>

<log>
<!-- YYYY-MM-DD — кто — что сделано -->
2026-09-01 — implementer subagent — портированы `.claude/skills/git-commit-ai-detector/SKILL.md` и `.claude/agents/git-commit-ai-detector.md` по translation table Kimi → Claude (правила 1, 11, 12 + agent path). Acceptance grep пуст, структура заголовков 1:1 с мастером, ссылка S1–S7 указывает на `~/.claude/skills/ai-code-detector/SKILL.md`. В agent frontmatter добавлен `model: opus` (конвенция Claude-стороны, как у `.claude/agents/ai-code-detector.md`). Репорт: `.superpowers/sdd/plan/task-02-report.md`.
</log>
