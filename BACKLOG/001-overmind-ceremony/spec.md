---
feature: 001
slug: overmind-ceremony
status: validated
created: 2026-09-24
validated: 2026-09-24
---

# Spec 001 — Overmind ceremony migration: единый знаменатель для планирования

<goal>
Единый знаменатель церемоний: Overmind MCP = единственный источник правды для планирования во всех 4 рантаймах кита. Ноль коллизий между superpowers (удаляется), файловым BACKLOG (становится read-only зеркалом) и overmind (становится ядром).
</goal>

<scope>
- Новый скилл `overmind-ceremony` + agent pointer на 4 сторонах (.kimi-code, .claude, .gemini/antigravity, .qwen).
- Ревизия ядра: AGENTS.md/CLAUDE.md/GEMINI.md/QWEN.md (секция Superpowers Integration → Overmind Integration; Documentation table; Development Workflow rules 7-10; Sub-Agent Rules +artifact contract), SKILLFLOW.md ×4, task-orchestrator SKILL.md ×4, audit-orchestrator ×4.
- Лёгкие правки 11 скиллов ×4 (списки чтения доков: BACKLOG → overmind board): fast-fixer-coder, pipeline-doctor, coding-challenge-orchestrator, dotnet-architect, dotnet-init, dotnet-coder, dotnet-code-reviewer, next-js-architect, next-js-coder, next-js-code-reviewer, maui-coder.
- Инфра: SYNC-STRUCTURE.md (mapping, count parity 22+24, удаление superpowers-строк), manifest.json (минус 4 BACKLOG-записи), README.md, удаление _TEMPLATE и legacy TASKS/, tombstone root BACKLOG.md.
- Верифицированная capability matrix overmind в теле скилла: работает — lifecycle/artifacts/validate/patch/notify/entries/materialization; НЕТ — agent import (058), batch (044), dependencies (030), entry tombstones (029), entries materialization (051), briefing tool (052), worktree binding (063), BACKLOG.md retirement (049 — направление).
</scope>

<anti-scope>
- НЕ импортируем старые фичи 001-003 (решение пользователя 2026-09-24, чистый старт).
- НЕ меняем формат XML-секций (принадлежит Overmind).
- НЕ трогаем squeezr-скилл и прочее не связанное с planning-церемониями.
- НЕ настраиваем MCP в рантаймах (среда пользователя).
</anti-scope>

<constraints>
- Quadruple-edit: каждое семантическое изменение — на 4 стороны в одном коммите (SYNC-STRUCTURE.md), translation tables соблюдены.
- No auto-commits. Ветка feature/overmind-ceremony.
- Frontmatter YAML-правило (quoted `: `) и cross-contamination greps — чистые.
- Sub-agents: Kimi K2.7/K3 only, primary forbidden.
</constraints>

<acceptance-criteria>
- [ ] Скилл overmind-ceremony существует на 4 сторонах, семантика идентична, contamination-greps пусты
- [ ] Ни одного упоминания superpowers в кит-файлах (кроме исторических записей в git)
- [ ] Ни одной инструкции читать/писать BACKLOG-файлы напрямую — все церемонии через mcp__overmind__*
- [ ] Count parity: 22 agents + 24 skills на каждой стороне; manifest dry-run чистый
- [ ] Эта фича проведена через борду: spec/plan validated, таски закрыты через planning_set_status, materialized дерево соответствует борде
</acceptance-criteria>

<refs>
- Борда Overmind (проект ff313f29-8144-488c-94e6-5ea5dde57bfd): фичи 006/013/019/020/023/033/039/040/042/043/049/055 — доказательная база capability matrix
- SYNC-STRUCTURE.md — quadruple-edit contract
</refs>

<log>
- 2026-09-24 — spec validated — content approved by user in-session (plan approval via ExitPlanMode, 2026-09-24); humanGate=off
</log>
