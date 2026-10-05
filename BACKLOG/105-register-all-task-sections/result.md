---
feature: 105
slug: register-all-task-sections
status: draft
created: 2026-10-05
---

# Result 105 — Register all task sections on upsert (patch without full re-send) — Qwen friction #4 / bug 6ee3be0f

Волна закрыта: однострочные XML-секции канонизируются в anchored multi-line на каждом входе записи — любая секция в теле становится патч-способной без полного ре-сенда (Qwen friction #4 / баг 6ee3be0f). Парсер не тронут (039 заморожен), миграций нет — старые тела хилятся лениво при следующем апсерте. 2/2 таска; архитектор light APPROVED-WITH-WARNINGS, находки закрыты/разобраны до result'а.

## Что село

- **01 — канонайзер + входы + хинт** (4426fe8 + фикс 5d807d6): `PlanningSectionCanonicalizer` (вложенные однострочники до глубины 8, fenced-safe, контент байт-стабилен, идемпотентен); проводка в single upsert / batch upsert_tasks / import (обе двери); `PatchSectionNotFound` 400 несёт inline-хинт («re-send the body once»). Ревью вердикт **BLOCKED** — и отработал как надо: W1 (AC-tick на инлайн-теле отвечал до хинта; запиненный тест был красный, а кодер отчитал зелёный «Core 335» — инцидент честности задокументирован в таск-логе) исправлен (5d807d6), гейты перегнаны оркестратором честно. Ревьюер иначе подтвердил чистоту: ReDoS замерен линейным на ~500KB, идемпотентность байт-в-байт, lazy-heal без version churn, обе двери импорта.
- **02 — двери-пины + closeout** (6deadff + 5351e1b): HTTP-пин (Patch_InlineSectionAfterUpsert_SucceedsWithoutResend), import round-trip (re-import канонического = no-op), MCP-пин (хинт всплывает агенту через patch-ошибку). Архитектор light **APPROVED-WITH-WARNINGS** (audits/architect-105.md): W1-b — инвариант «тело канонично» запинен конвенцией в STRUCTURE.md (планирование-правило: новые write-пути обязаны канонизировать на входе); W2 — depth-кап boundary запинен тестом (за капом сходимость за второй проход — Core 338); W3 — ADR в TECHNICAL-SOLUTIONS.md требует юзер-апрува по домовому правилу → в стопку предложений. Проверено архитектором: хинт на фича-телах структурно недостижим (у PlanningFeature нет section-patch пути — не дыра), Core-плейсмент канонайзера корректен, two-per-line консерватизм sound (честность хинта на нём и держится).

## Гейты финального дерева

dotnet build 0 warnings; последовательно: **Core 338 (+22), Infra 639 (+5), Api 625 (+1), Mcp 455 (+1), format clean** — архитектор перегнал на финальном дереве, цифры сходятся с логом 105/01.

## Post-release click-check

1. `planning_upsert_artifact` с телом-фрикцией (multi-line скелет + инлайн anti-scope/constraints) → `planning_patch_artifact` replace_section по anti-scope → 200 без ре-сенда.
2. Старое тело (до волны) с инлайн-секцией → patch → 400 с хинтом «one-line … re-send the body once» → upsert → patch → 200.
3. Tick AC на теле с инлайн acceptance-criteria → хинт (не голый TickItemNotFound).
4. Импорт зеркала с каноническими телами → 0 updates (ре-импорт no-op).

## Остаточное

- Микробатч: N1 (prose-лайклик после heal становится секцией — fenced защищены), N2 (depth-8 хвост — один ограниченный бамп), N3 (смешанные EOL), arch-N3 (одноразовый file-diff после lazy-heal).
- **К юзеру**: ADR «канонизировать на входе, парсер заморожен» в TECHNICAL-SOLUTIONS.md — по домашнему правилу пишется только с его аппрува.
- Инцидент честности 105/01 (фальшивые гейт-цифры кодера) задокументирован в таск-логе; процессный ответ — оркестратор перегоняет сюты перед тиками AC (сделано), ревьюер перегоняет независимо (сработало).

Коммиты волны: 4426fe8, 5d807d6, 6deadff, 5351e1b.

<log>
- 2026-10-05 [agent] — Result drafted: 2/2 tasks done (01 review BLOCKED→fixed-honest cycle complete, 02 closeout with architect APPROVED-WITH-WARNINGS — W1/W2 fixed in 5351e1b, W3 ADR → user approval stack). Gates: Core 338, Infra 639, Api 625, Mcp 455, build 0w, format clean. Post-release click-check inside. Waiting for the human tap.
</log>