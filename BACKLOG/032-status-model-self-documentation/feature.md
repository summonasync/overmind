---
feature: 032
slug: status-model-self-documentation
status: done
priority: p2
version: 2
created: 2026-09-20
---

# Feature 032 — Status model self-documentation: lifecycle в onboarding + per-kind статусы в MCP-тулах

<goal>
Модель жизненного цикла и статусов самодокументируется для агентов и людей: overmind_onboard quickstart несёт краткую lifecycle-модель (5 статусов + авто-переходы + escape-hatch + «done терминален»), MCP-тулзы и их descriptions описывают per-kind наборы статусов артефактов (у result нет done — его «готово» = validated), onboard-хинт «перед работой глянь notes/decisions/tombstones». Закрывает тему «модель нигде не описана — чужие агенты угадывают» (догфуд-факты 1-2, батч 2 item 2).
</goal>

<context>
Mindlash-агенты строили маппинг-логику на ложной премисе «Review не существует» (их таск-54) и ловили 400-сюрпризы на result-done. Корень — модель не self-documented: quickstart про статусы молчит, тулзы не описывают допустимые статусы по kind. Сюда же складываем открытую развилку из батча 2 item 1.
</context>

<scope-sketch>
- overmind_onboard quickstart: +3-5 строк про модель (статусы, авто-переходы, done из любого не-done + терминальность, per-kind статусы кратко).
- MCP tool descriptions: planning_set_status/planning_upsert_artifact описывают per-kind допустимые статусы; planning_set_feature_status уже учит (эталон стиля).
- Onboarding-хинт про пред-чтение notes/decisions (+ tombstones после 029).
- Опционально: web — статусные подсказки в UI.
</scope-sketch>

<open-questions>
- result-validated на inProgress-фиче → авто-review? (батч 2 item 1; оркестратор рекомендует НЕ делать — escape-hatch покрывает; решение юзера).
- Формат модели в quickstart: prose vs компактная таблица.
</open-questions>

<status>
Draft; спека короткая. Источники: notes «Факты 1-2» и «батч 2» (удаляются после нарезки — знание перенесено сюда).
</status>