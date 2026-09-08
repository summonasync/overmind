---
feature: 001
slug: example-feature
status: draft        # draft | approved (approved = FROZEN)
version: 1
feature: feature.md    # ссылка на фича-файл, которому соответствует план
created: YYYY-MM-DD
---

# Plan 001 — <Feature title>

<approach>
Общий подход: какие слои/сервисы затрагиваем, порядок внедрения, почему так.
</approach>

<tasks>
- [ ] 01-<slug> — одна строка: что делаем, какие файлы, как проверяем → tasks/01-slug.md
- [ ] 02-<slug> — ...
<!-- Декомпозиция в стиле superpowers:writing-plans: мелкие таски,
     точные пути файлов, шаг верификации у каждой таски. -->
</tasks>

<risks>
- Технические риски, зависимости, что может пойти не так + mitigation
</risks>

<verification>
- Как проверяем фичу ЦЕЛИКОМ после всех тасок (build, тесты, ручной сценарий)
</verification>

<!--
LIFECYCLE: после status: approved файл ЗАМОРОЖЕН (как feature.md).
Чекбоксы в <tasks> — единственное, что обновляется по ходу работы.
-->
