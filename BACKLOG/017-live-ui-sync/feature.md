---
feature: 017
slug: live-ui-sync
status: done
priority: p2
version: 1
created: 2026-09-19
---

# Feature 017 — Live UI sync: board/entries refresh on MCP-side changes

<goal>
Интерактивность дашбордов (user request 2026-09-19): когда данные меняются через MCP (другой агент/сессия двигает церемонию), открытый UI обновляется САМ, без ручной перезагрузки. Минимум — поллинг ~каждые 15 сек; дальше можно SSE/WebSocket.
</goal>

<requirements>
- idea stub: обсуждается, спека не начата
- MVP: React Query refetchInterval (~15s) на живых списках — planning board, feature workspace, entries, drift; в фоне (refetchIntervalInBackground?) решить на спеке
- Заметность чужих мутаций: current-пин, статусы тасок, новые фичи от других агентов должны появляться сами
- Dogfood data point (2026-09-19): на табах spec/plan/tasks воркспейса обновления, пришедшие через MCP, видны только после принудительного F5/Ctrl+Shift+R — живое подтверждение боли, поллинг должен покрывать и artifact-контент воркспейса, не только доску
- Дальше MVP (отдельным этапом?): server-push (SSE) вместо поллинга; поллинг 15s — приемлемая цена на старте (user decision 2026-09-19)
</requirements>

<non-goals>
- Realtime коллаборация, optimistic concurrency, lock'и на редактирование
</non-goals>