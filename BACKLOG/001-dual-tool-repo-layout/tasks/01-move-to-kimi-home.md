---
feature: 001
task: 01
slug: move-to-kimi-home
status: done
created: 2026-08-31
---

# 001/01 — Move existing content into .kimi-code/

<goal>
Kimi-специфичный контент физически переезжает в `.kimi-code/` с сохранением git-истории.
</goal>

<context>
Первая таска фичи 001. Корень репо сейчас: `agents/` (17), `skills/` (18), `BACKLOG/`,
`SOCIAL_DICTIONARY.md`, `config.toml`, `AGENTS.md`, `.gitignore`, `BACKLOG/001-dual-tool-repo-layout/`
(эта фича). `BACKLOG/` НЕ мувается — решение v2: шаблон `_TEMPLATE/` глобальный, фича-папки
локальные; деплой шаблона делает sync-скрипт (task 05).
</context>

<scope>
- `mkdir -p .kimi-code`
- `git mv agents .kimi-code/agents`
- `git mv skills .kimi-code/skills`
- `git mv SKILLFLOW.md .kimi-code/SKILLFLOW.md`
- `git mv SOCIAL_DICTIONARY.md .kimi-code/SOCIAL_DICTIONARY.md`
- `git mv config.toml .kimi-code/config.toml`
- Проверка self-references в `.kimi-code/agents/*.md` и `.kimi-code/skills/`
</scope>

<anti-scope>
- `BACKLOG/` не трогаем — остаётся в корне.
- Ничего не создаём в `.claude/` (это task 03).
- AGENTS.md, README, SYNC-STRUCTURE, скрипты — не трогаем.
- Никаких коммитов без явного подтверждения пользователя.
</anti-scope>

<constraints>
- Только `git mv` (история через rename detection).
- Windows Git Bash, пути с forward slashes.
</constraints>

<acceptance-criteria>
- [ ] `ls .kimi-code/agents | wc -l` = 18, `ls -d .kimi-code/skills/*/ | wc -l` = 20
- [ ] `.kimi-code/SKILLFLOW.md` существует
- [ ] В корне остались: `.kimi-code/`, `BACKLOG/`, `.gitignore`, `AGENTS.md`, `.git/`
- [ ] `git log --follow --oneline -1 .kimi-code/agents/dotnet-coder.md` показывает старый коммит
- [ ] `grep -rn 'skills/' .kimi-code/agents/ | grep -v '~/.kimi-code/skills'` — пусто
- [ ] `grep -rn 'SOCIAL_DICTIONARY\|config\.toml' .kimi-code/agents/ .kimi-code/skills/ | grep -v '~/\.kimi-code'` — только допустимые prose-упоминания; stale-пути исправлены на `~/.kimi-code/...`
- [ ] Список изменённых файлов доложен пользователю; коммит только после подтверждения
</acceptance-criteria>

<refs>
- @../feature.md — requirements (git mv, верификация, BACKLOG/ не переезжает)
- @../plan.md — task 01
</refs>

<obstacles>
- `git mv` refuses untracked paths (SKILLFLOW.md, architect-review, dotnet-best-practices) — used `git add -N` first; no history existed to lose.
- `git log --follow` cannot trace staged-but-uncommitted renames — re-verify after the first commit.
</obstacles>

<log>
- 2026-08-31: moved agents/skills/SKILLFLOW/SOCIAL_DICTIONARY/config.toml into .kimi-code/ via git mv; review clean (spec 7/7, minors deferred).
</log>
