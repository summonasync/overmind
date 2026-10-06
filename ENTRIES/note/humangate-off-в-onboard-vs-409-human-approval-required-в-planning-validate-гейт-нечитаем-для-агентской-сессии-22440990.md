---
id: 22440990-6f55-4ee8-847e-b5b3cdf752f3
type: note
title: humanGate=off в onboard vs 409 «Human approval required» в planning_validate — гейт нечитаем для агентской сессии
tags: agent-experience, planning, reported-by-qwencode-squeezr, validation-gate
createdAt: 2026-09-28T11:29:39Z
updatedAt: 2026-09-28T11:29:39Z
---

# humanGate=off в onboard, но planning_validate требует человека (409)

**Кто сообщает:** Qwen Code, main-агент, сессия в проекте Squeezr (`31f33e30-6056-40a3-8447-c43d889fee9f`), фича `063-dogfood2-ignore-recall-suggest`. Дата: 2026-09-28.

## Что произошло

1. `overmind_onboard` (первый вызов церемонии) вернул блок:
   ```
   Validation: humanGate=off
   ```
2. Следом `planning_validate(artifactId=96d083eb-b80e-48f1-b2c4-3b9fb2dfffcf, note=…)` — spec фичи 063 — вернул:
   ```
   overmind API returned 409 (POST /api/agent/planning/artifacts/96d083eb-…/validate):
   Human approval required: send overmind_notify(kind=approval, featureSlug=063-dogfood2-ignore-recall-suggest)
   and wait for the human's reply in the notification center, or validate via the UI.
   ```

То есть payload говорит «гейт human-валидации выключен», а сервер требует живого человека. Одно из двух врёт.

## Какие проблемы это сделало

- Пользователь явно попросил автономный прогон (`старт: spec→plan→tasks→кодеры автономно`), и автономная волка встала на ПЕРВОМ же гейте — агент не может ни зафиксировать spec, ни по правилам церемонии перейти к plan/tasks.
- Человек при этом сидит в том же чате и способен сказать «ок» словами, но канал «chat approval → planning_validate» не существует: 409 требует либо реплая в notification center, либо клика в UI. То есть самый дешёвый апрув (в чате) не имеет детерминированного механизма применения.
- `humanGate=off` в onboard — единственный сигнал, по которому агент планирует церемонию; полагаться на него сейчас нельзя, надо угадывать по первому же 409.

## Что хочется

- Either `humanGate` in the onboard payload reflects the real per-project/per-artifact-kind validation requirement (and says which kinds are gated), or it is renamed so it stops reading as «validate will pass».
- Либо явный документированный путь «chat approval»: notify(kind=approval) + reply → авто-валидация артефакта, без ручного клика в UI.
- Либо в 409 нести подсказку, какой именно флаг/путь включает авто-валидацию для агентских сессий.

## Попутные schema-papercut этой же сессии (мелочь, но стоит времени)

- `planning_list_artifacts` и `planning_set_current` требуют `slug`, хотя соседние planning-тулзы принимают `featureSlug` — первый вызов с `featureSlug` = 400 `params must have required property 'slug'`.
- `planning_set_current` дополнительно требует явный `isCurrent` (boolean), что не очевидно из «pin/unpin» формулировки описания.
- `read_entry` требует `entryId`, а не `id`, хотя везде в ответах поле называется `id`.

Никаких обходных правок диска не делал: spec остался draft, жду человеческий апрув в UI.