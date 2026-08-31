---
feature: 001
task: 06
slug: cross-contamination-gate
status: done
created: 2026-08-31
---

# 001/06 — Final cross-contamination gate

<goal>
Финальная верификация всей фичи: обе стороны чисты от чужих строк, структура соответствует
feature.md, sync dry-run зелёный.
</goal>

<context>
Последняя таска фичи 001, запускается после 01-05. Это review-гейт фичи целиком
(verification из plan.md).
</context>

<scope>
- Прогнать все grep-гейты и структурные проверки (acceptance-criteria)
- Проверить `.gitignore` — не исключает ли новые директории
</scope>

<anti-scope>
- Не правим контент скиллов/агентов — только флагуем находки пользователю.
- Коммиты — только после подтверждения пользователя.
</anti-scope>

<constraints>
- Любой хит в grep-гейте = BLOCKER → возврат в таску-владельца (02/03) с file:line.
</constraints>

<acceptance-criteria>
- [ ] `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml' .claude/` — пусто (включая `.claude/CLAUDE.md`)
- [ ] `grep -rni 'claude code\|sonnet\|opus\|haiku' .kimi-code/` — пусто; `grep -rni 'claude' .kimi-code/` — хиты допустимы ТОЛЬКО в: layout note `.kimi-code/AGENTS.md` и стоп-листе `.kimi-code/skills/coding-challenge-orchestrator/SKILL.md` (user ruling, whitelist)
- [ ] Паритет: `ls .kimi-code/agents/*.md | wc -l` = `ls .claude/agents/*.md | wc -l` = 18; `ls -d .kimi-code/skills/*/ | wc -l` = `ls -d .claude/skills/*/ | wc -l` = 20
- [ ] `ls .kimi-code` = agents skills AGENTS.md SKILLFLOW.md SOCIAL_DICTIONARY.md config.toml; `ls .claude` = agents skills CLAUDE.md SKILLFLOW.md; `TASKS/` в корне; в корне НЕТ AGENTS.md и CLAUDE.md
- [ ] `powershell -File sync.ps1 -DryRun` — зелёный; план включает `.kimi-code/AGENTS.md` → `~/.kimi-code` и `.claude/CLAUDE.md` → `~/.claude`
- [ ] Ссылка на SYNC-STRUCTURE.md из layout note в `.kimi-code/AGENTS.md` и `.claude/CLAUDE.md` резолвится (файл в корне)
- [ ] `.gitignore` не исключает `.kimi-code/`, `.claude/`, `TASKS/`
- [ ] Финальный список изменённых файлов доложен пользователю с вопросом о коммите
</acceptance-criteria>

<refs>
- @../feature.md — verification-блок requirements
- @../plan.md — verification
- tasks/01-05 — все предыдущие таски
</refs>

<obstacles>
- Gate 2 изначально противоречил критерию 6 (layout note обязана упоминать .claude/). User ruling: whitelist layout note + coding-challenge-orchestrator stop-list. Гейт переформулирован.
- .gitignore: слипшиеся правила `*.lnk` и `.superpowers/` — разделены.
</obstacles>

<log>
- 2026-08-31: все гейты зелёные (kimi-gate пуст, claude-gate = только whitelisted строки, паритет 18/18 и 20/20, структура ок, sync dry-run exit 0, ссылки резолвятся, .gitignore ок).
</log>
