---
feature: 020
slug: agent-notifications
status: done
priority: p2
version: 1
created: 2026-09-19
---

# Feature 020 — Agent → human notifications via MCP with deep links + reply path

<goal>
Система нотификаций агент → человек (user request 2026-09-19): любой агент, работающий через overmind MCP, может кинуть нотификацию в UI — «аппруви тут», «вопросик тут» — со ссылкой на план/спек/таску. Юзер кликает → проваливается в нужное место (feature workspace, нужный таб) → читает → отвечает. Закрывает human-door полностью: сейчас агент может работать через MCP, но человек узнаёт о вопросах, только если сам зашёл в UI.
</goal>

<requirements>
- idea stub: обсуждается, спека не начата
- MCP-дверь: тулза типа `notify` (additive-only контракт) — kind (approval/question/info), target (feature slug + artifact/tab), текст; agent identity (кто кинул — из 013 онбординга?)
- UI: центр нотификаций (колокольчик/counter в топбаре) + deep-link в воркспейс (?feature&tab); unread/read состояния
- Ответная дорога: юзер отвечает в нотификации → ответ доступен агенту через MCP (inbox/outbox, poll или в связке с 017 live sync)
- Связки: human-validation gate (006) — нотификация «жду аппрува»; 017 live sync — доставка в открытый UI без F5
- Хранение: SQLite (новая таблица), retention/cleanup решить на спеке
</requirements>

<non-goals>
- Внешние каналы (email/telegram/webhook) — отдельная история
- Realtime чат с агентом
</non-goals>