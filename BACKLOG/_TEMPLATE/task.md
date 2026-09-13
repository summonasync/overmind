---
feature: 001
task: 01
slug: example-task
status: todo         # todo | in-progress | blocked | done
review: light        # light (default — code reviewer) | hard (code reviewer + architect-review)
created: YYYY-MM-DD
---

# 001/01 — <Task title>

<goal>
Что должно быть достигнуто. Формулируй через РЕЗУЛЬТАТ, не через процесс (what, не how).
</goal>

<context>
Почему задача существует, к какой фиче относится, связи с другими тасками.
Субагент-исполнитель читает ТОЛЬКО этот файл — весь нужный контекст должен быть здесь
или в явных ссылках <refs>. spec.md/plan.md субагент НЕ читает.
</context>

<scope>
- Что входит: конкретные файлы, слои, эндпоинты, компоненты
</scope>

<anti-scope>
- Что НЕ трогаем ни при каких обстоятельствах (hard constraints)
</anti-scope>

<constraints>
- Стек, версии, лимиты (производительность, бюджеты токенов/времени)
- Лицензионные проверки — через nuget-licensing-guard
</constraints>

<acceptance-criteria>
- [ ] Проверяемый критерий (тест, команда, наблюдаемое поведение)
- [ ] Этот список + соответствие spec.md — чек-лист для review-гейта
</acceptance-criteria>

<refs>
- @../feature.md — durable what/why (читает ревьюер, не субагент)
- @../spec.md — какая секция (читает ревьюер, не субагент)
- @../plan.md — какой шаг плана покрывает эта таска
- @CONTEXT.md — какая секция
- @TECHNICAL-SOLUTIONS.md — какой ADR
- tasks/NN-slug.md — зависимые таски
</refs>

<obstacles>
<!-- Заполняется исполнителями ПО ХОДУ работы: workaround'и, кривые флаги,
     проблемные зависимости, особенности окружения.
     Следующий агент не должен переоткрывать эти грабли. -->
</obstacles>

<log>
<!-- Одна строка на запись: YYYY-MM-DD — кто (агент/скилл) — что сделано. -->
</log>
