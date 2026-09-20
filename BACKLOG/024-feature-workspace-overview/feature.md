---
feature: 024
slug: feature-workspace-overview
status: done
priority: p2
version: 2
created: 2026-09-20
---

# Feature 024 — Feature workspace rework: card header + stepper over body + Feature Overview panel

<goal>
Feature workspace становится «карточкой фичи» с честной вертикальной иерархией, а правая панель в режиме фичи — Feature Overview (user request 2026-09-20, во время визуального прохода 0.1.8):
1. Header: back-to-dashboard над тайтлом; Title; снизу slug · updated; отдельная строка [статус-селект с цветом как на борде] [v1] [Edit]; Delete — иконка в правом верхнем углу.
2. Строка статусов (церемониальный степпер из 019) — НАД описанием фичи (сегодня под).
3. Правая панель при открытой фиче — Feature Overview вместо дашбордного: статус/версия/pin, tasks x/y, spec/plan/result, возраст в бэклоге (created → сейчас), последний апдейт, open questions из секции <open-questions> спеки.
</goal>

<requirements>
- idea stub: обсуждается, спека не начата
- Header переезды: ← Dashboard из actions-строки — над тайтлом (мелкий, muted); actions-строка = статус-селект + v-бейдж + Edit; Delete — отдельно в правом верхнем углу (confirm-модалка остаётся)
- Степпер: рендер сразу после хедера, до body-описания
- Правая панель: в режиме фичи контент = Feature Overview (данные уже в feature query: статус, version, artifacts, createdAt, updatedAt; open questions — парс <open-questions>-секции из spec body и рендер markdown; «none» → показать как есть)
- Ничего из 022/023-механики не ломать: transition-aware селект, done-лок, оптимистика
</requirements>

<non-goals>
- Редизайн дашбордного овервью (правая панель без фичи не трогается)
- Новые эндпоинты (всё из feature query + spec body)
</non-goals>