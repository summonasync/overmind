---
feature: 001
slug: example-feature
status: draft        # draft | approved (approved = FROZEN)
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

<decisions>
- Ключевые решения, принятые в brainstorm: что выбрали и почему (кратко; полный разбор — ADR в TECHNICAL-SOLUTIONS.md)
</decisions>

<non-goals>
- Что явно НЕ входит в эту фичу (защита от scope creep на уровне фичи)
</non-goals>

<open-questions>
- Нерешённые вопросы. spec не может стать approved, пока список не пуст.
</open-questions>

<!--
LIFECYCLE: после status: approved файл ЗАМОРОЖЕН.
Изменение = ре-валидация с пользователем + bump version. Молчаливые правки запрещены.
Читатели: оркестратор, ревьюер. Исполняющий субагент spec НЕ читает —
его контракт — tasks/NN-foo.md.
-->
