---
feature: 021
slug: task-brief
status: done
priority: p2
version: 2
created: 2026-09-19
---

# Feature 021 — Task brief description visible in the tasks table

<goal>
Бриф-описание у таски (user request 2026-09-19): у каждой таски есть короткое описание (1-2 строки), видимое прямо в табличке тасок на странице фичи — чтобы не открывать каждую, чтобы понять, что она делает.
</goal>

<requirements>
- idea stub: обсуждается, спека не начата
- Поле brief/summary у task-артефакта (additive-only контракт Host↔MCP: добавление поля ок, переименование запрещено)
- Источник брифа на спеку: явное поле при создании vs авто-извлечение из <goal> таски (первые N символов / первая строка)
- UI: колонка/подстрока в таблице тасок; редактируемо
- MCP/API: brief в DTO доски/воркспейса (additive)
</requirements>

<non-goals>
- Полноценные rich-описания/картинки в таблице
</non-goals>