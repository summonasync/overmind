---
feature: 036
slug: agent-workflow-onboarding
status: done
priority: p0
version: 1
created: 2026-09-20
---

# Feature 036 — Agent workflow onboarding: обучить агента без скиллов флою feature→spec→plan→tasks→result

## Что

Сегодня Overmind «работает с кайфом» у нас, потому что у оркестратора уже есть скиллы (task-orchestrator), чей пайплайн совпадает с церемонией борды. Чужой агент без таких скиллов получает набор тулов и quickstart с порядком вызовов — но не дисциплину: юзер-гейты (STOP до аппрува спеки/плана), декомпозиция plan→tasks как execution-контрактов, ревью-гейты, materialize+commit после ходов, скармливание суб-агенту только его таска. Нужен продуктовый механизм, который обучает агента флою без внешних скиллов.

## Почему

- Целевая аудитория Overmind — любой агент любой сессии (Mindlash-крю уже показал: агенты строят логику на ложных премиссах, если борда молчит).
- 032 закрывает модель статусов (что можно ставить); эта фича — процесс (в каком порядке и с какими гейтами работать).
- Скиллы пользовательской машины — не фактор: всё нужное должно приезжать с самим Overmind.

## Кандидаты-механизмы (вопросы уровня спеки)

1. **Teach in tool responses** (прогрессивное раскрытие): тула сам учит в ответах — teaching-400 уже есть как прецедент (023/08), развить: planning_create_feature ответ подсказывает «теперь spec → validate → user gate»; feature без спеки при попытке таска → 400 с обучением; result без tasks → предупреждение. Дешево, difícil промахнуться, работает во всех MCP-клиентах.
2. **Workflow-ресурс**: overmind://workflow — полный playbook церемонии как MCP-ресурс + строка-указатель в quickstart.
3. **Onboarding-флоу на борде**: planning_current_task в стоп-стейтах отдаёт следующий шаг церемонии (сейчас отдаёт stop/empty reason — расширить текстом «что дальше по флою»).

Комбинируются; в v1 вероятно 1+3 (инлайн, ноль новых поверхностей), 2 — по мере роста текста.

## Открытые вопросы

- Насколько агрессивно тула отказывает vs подсказывает (400 vs warning в ответе)?
- Нужен ли отдельный «оркестраторский» quickstart (для агента-менеджера) vs «исполнительский» (для суб-агента с одним таском)?
- Связь с 032: один blocker на 032 (тексты quickstart) или независимы?

## Источник

Идея юзера в сессии 2026-09-20 («что делать, если человек без таких скиллов поставит overmind — мы можем сделать onboarding для агента, обучая его работать по feature-spec-plan-tasks-result flow?»).

<log>
- 2026-10-06 [agent] — Van by claude claimed the feature
- 2026-10-06 [auto] — artifact created → ready
- 2026-10-06 [auto] — task 01 taken → inProgress
- 2026-10-06 [auto] — all tasks done + spec + plan validated → review
- 2026-10-06 [agent] — by user request in-session — armer notification c059e273; result validated in the final batch sitting. Onboarding teaching closed (ad50c68 + 5ba84f9 + 5c336b3); live-walk post-deploy follow-up recorded in task 04 log.
- 2026-10-06 [agent] — done via agent review by Van (claude), approved by human tap 2026-10-06
- 2026-10-06 [agent] — claim released on done-close
</log>
