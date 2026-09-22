---
feature: 052
slug: agent-briefing-tools
status: draft
priority: p2
version: 1
created: 2026-09-22
---

# Feature 052 — Agent briefing: стартовый MCP-тул — guards/gates/decisions/последние notes/tombstones одним вызовом (getgates/getnotes/… как альтернатива)

<goal>
Агент, стартуя в проекте, должен увидеть guards, gates, decisions и последние notes ДО любой работы — сейчас это list_entries с фильтром + read_entry по одному (столько же round-trips, сколько категорий и записей), а указатель на `overmind://resources` в инструкциях сервера бесполезен, когда ресурс-тулы сессией не загружены (полевая жалоба DMT). Идея пользователя — специализированные тулы (getgates/getnotes/gettombstones/getguards); рекомендация оркестратора — один сводный «стартовый брифинг»-тул: агенту на старте нужны все категории разом, а N вызовов вместо одного — это ровно batch-боль из 044.
</goal>

<requirements>
- Сводный тул (рабочее имя `overmind_briefing` / `get_project_briefing`) — одним вызовом: guards полностью (короткие, критичные), gates полностью, decisions (полные или заголовки — спека), последние N notes заголовками+сниппетами, tombstones списком (когда 029 закроет сущность).
- Альтернатива четырёх специализированных тулов — явно взвесить в спеке (trade-off: имена в tool-discovery vs один вызов); решение зафиксировать.
- Выдача компактная, в контракте 037-echo-slimming: полные тела только там, где коротко и критично (guards/gates).
- Onboarding-quickstart обновлёт строку «Before working, read notes/decisions/guards via overmind://resources and list_entries filters» на новый тул — первая точка контакта агента указывает на него.
- Свежесть: порядок по updatedAt, лимит N — параметры тула, не константы.
</requirements>

<non-goals>
- Замена `list_entries`/`read_entry` для глубокой работы — брифинг про старт, не про исследование.
- Чинить MCP-resources как канал — идём мимо обычными тулами.
- Изменения БД-схемы — тул читает существующее.
</non-goals>

<refs>
- 029-entry-tombstones — tombstones в выдаче брифинга; «список агентам перед работой» был в её задумке — reconcile спек, чтобы список был один.
- 042-onboard-observability — сосед по первой точке контакта; по духу это продолжение полевой боли (кандидат на p1 словом пользователя).
- 044-batch-planning-ops — та же экономия round-trips.
- FEATURELIST/03-tool-description-gaps.md — жалоба на бесполезный resources-pointer.
- 036-agent-workflow-onboarding — брифинг как один из кирпичей обучения агента.
- 037-mcp-echo-slimming — контракт худого эха.
- Полевой контекст: юзер предложил 2026-09-22 («чтобы каждый агент, стартуя, мог засосать себе в контекст последнее»).
</refs>