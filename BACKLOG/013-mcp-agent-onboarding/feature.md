---
feature: 013
slug: mcp-agent-onboarding
status: done
priority: p2
version: 1
created: 2026-09-19
---

# Feature 013 — MCP agent onboarding ceremony (discover + remember project ID)

<goal>
MCP-онбординг церемония для агентов: агент, видящий overmind MCP в репо, ПЕРЕД началом работы обязан заонбордиться — определить/запомнить свой project ID и получить quickstart церемонии (user decision 2026-09-19: «агент видит mcp — знает, что прежде чем работать, он должен заонбордиться и запомнить свой project ID»). Закрывает живой гэп из догфуда: Claude в Squeezr имел подключённый overmind, но не писал туда — инструкции репо вели его в файловый BACKLOG, а projectId он не знал.
</goal>

<requirements>
- idea stub: обсуждается, спека не начата
- Agent-driven discovery: MCP-тулза или prompt (MCP prompts capability) типа `onboarding` — агент дёргает её и получает: projectId (матчинг по cwd/disk path, или создание проекта), сводку доски, quickstart церемонии (какие тулзы в каком порядке)
- «Запомнить project ID»: решить где живёт привязка repo↔project — server-side (host знает cwd агента?) vs agent-side (сниппет в агент-файле репо)
- Install-side: `overmind-mcp install --onboard` дропает инструкционный сниппет в агент-файл целевого проекта (CLAUDE.md/AGENTS.md) + создаёт проект в базе, если нет (из Deferred-семени 2026-09-19)
- Связать с 009-mcp-registration: install уже умеет писать конфиги 6 агентов — онбординг логично едет туда же
- Dogfood data point (2026-09-19): КАЖДЫЙ внешний агент (Claude в Squeezr, агент в MindlashStudio) отчитался, что ему пришлось ВРУЧНУЮ переписывать свой CLAUDE.md под MCP-first — онбординг должен убирать именно это
- Решение-набросок (user discussion 2026-09-19): НЕ реврайт чужого агент-файла, а managed block с маркерами (overmind:begin/end) — «этот репо = проект X, id ..., MCP-first, переопределяет файловые церемонии»; маркеры дают update/remove самим инсталлером
- Conflict detection: если в агент-файле уже есть файловая церемония (BACKLOG/ и т.п.) — варнинг, не молчаливый реврайт
- Server-side discovery (тулза/prompt onboarding) — второй слой для агентов без файла; соло проблему не решает (chicken-egg: агент её не дёрнет без наводки из файла)

</requirements>

<non-goals>
- Полный wizard в веб-UI для онбординга агентов (UI-часть — отдельно, если понадобится)
</non-goals>