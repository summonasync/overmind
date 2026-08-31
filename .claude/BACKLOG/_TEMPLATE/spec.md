---
feature: 001
slug: example-feature
status: draft        # draft | approved (approved = FROZEN)
version: 1
created: YYYY-MM-DD
---

# Spec 001 — <Feature title>

<decisions>
- Ключевые решения, принятые в brainstorm: что выбрали и почему (кратко; полный разбор — ADR в TECHNICAL-SOLUTIONS.md)
</decisions>

<open-questions>
- Нерешённые вопросы. spec не может стать approved, пока список не пуст.
</open-questions>

<requirements>
- Уточнённые в ходе брейншторма требования; базовые — в feature.md
</requirements>

<!--
LIFECYCLE: после status: approved файл ЗАМОРОЖЕН.
Изменение = ре-валидация с пользователем + bump version. Молчаливые правки запрещены.
Читатели: оркестратор, ревьюер. Исполняющий субагент spec НЕ читает —
его контракт — tasks/NN-foo.md.
-->
