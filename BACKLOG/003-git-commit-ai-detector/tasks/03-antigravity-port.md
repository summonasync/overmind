---
feature: 003
task: 03
slug: antigravity-port
status: done
review: light
created: 2026-09-01
---

# 003/03 — Antigravity port of git-commit-ai-detector

<goal>
На Antigravity-стороне существуют `.gemini/antigravity/skills/git-commit-ai-detector/SKILL.md` и
`.gemini/antigravity/agents/git-commit-ai-detector.md` — семантически идентичные Kimi-мастеру из задачи 01.
</goal>

<context>
Triple-edit contract (SYNC-STRUCTURE.md). Порт механический: источник —
`.kimi-code/skills/git-commit-ai-detector/SKILL.md` и `.kimi-code/agents/git-commit-ai-detector.md`
(результат задачи 01), трансляция по таблице «Kimi → Antigravity» из `SYNC-STRUCTURE.md`
+ обязательный ручной проход (role-based model wording вместо имён моделей).
</context>

<scope>
- Create: `.gemini/antigravity/skills/git-commit-ai-detector/SKILL.md`
- Create: `.gemini/antigravity/agents/git-commit-ai-detector.md`
</scope>

<anti-scope>
- `.kimi-code/` источник — read-only; ошибки мастера = <obstacles>, не правка.
- `.claude/` — задача 02, не эта.
- SYNC-STRUCTURE.md, BACKLOG.md, manifest.json — не эта таска.
- Никаких git commit/push.
</anti-scope>

<constraints>
- Translation table Kimi → Antigravity из SYNC-STRUCTURE.md, строго по порядку + manual pass:
  `~/.kimi-code/skills` → `~/.gemini/antigravity/skills`; `Kimi K3`/`K3` →
  `strongest reasoning model (selected in Agent Manager)`; `K2.7` → `strong execution model`;
  `Kimi Code CLI` → `Google Antigravity`; `config.toml` → `Antigravity permission settings`.
- Model policy в порте: «ALWAYS executed by the strongest reasoning model; do not run on the strong execution model».
- Agent frontmatter: ключи `whenToUse`/`tools` сохраняются (Antigravity игнорирует неизвестные ключи — по таблице Known intentional differences).
- Контент по смыслу не переписывается — только трансляция.
</constraints>

<acceptance-criteria>
- [ ] Оба файла созданы, структура секций 1:1 с Kimi-мастером.
- [ ] `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml\|sonnet\|opus\|haiku\|\.claude' .gemini/antigravity/skills/git-commit-ai-detector/ .gemini/antigravity/agents/git-commit-ai-detector.md` — пусто.
- [ ] Model wording — role-based, без имён моделей; пути `~/.gemini/antigravity/skills/...`.
- [ ] Стоп и репорт списка изменённых файлов.
</acceptance-criteria>

<refs>
- @../spec.md — <requirements> (triple-edit deliverables)
- @../plan.md — Task 03
- @SYNC-STRUCTURE.md — translation table Kimi → Antigravity + Known intentional differences
- tasks/01-kimi-master-skill-agent.md — источник контента (должна быть done)
</refs>

<obstacles>
<!-- Заполняется исполнителями ПО ХОДУ работы -->
</obstacles>

<log>
<!-- YYYY-MM-DD — кто — что сделано -->
2026-09-01 — implementer subagent — ported SKILL.md + agent file to `.gemini/antigravity/` via SYNC-STRUCTURE.md translation table + manual pass (role-based model wording, S1–S7 path rewired to `~/.gemini/antigravity/skills/ai-code-detector/SKILL.md`); acceptance grep empty; section structure 1:1 with Kimi master.
</log>
