---
feature: 001
slug: example-feature
status: draft        # draft | ready
version: 1
created: YYYY-MM-DD
---

# Feature 001 — <Feature title>

<goal>
Что и ЗАЧЕМ. Результат для пользователя/системы, не процесс.
</goal>

<requirements>
- Проверяемые требования, по одному на строку (outcome-level, не implementation-level)
</requirements>

<non-goals>
- Что явно НЕ входит в эту фичу (защита от scope creep на уровне фичи)
</non-goals>

<!--
LIFECYCLE: создаётся при постановке фичи в бэклог ДО старта работы.
Не замораживается гейтом, но изменения после status: ready проходят через пользователя (bump version).
Молчаливые правки запрещены.
Вход для spec gate: на его основе строится spec.md через superpowers:brainstorming.
-->
