---
feature: 014
slug: task-read-edit-modes
status: done
priority: p2
version: 1
created: 2026-09-19
---

# Feature 014 — Task open modes: read view + edit

<goal>
Два варианта открытия таски в UI (user request 2026-09-19, догфуд 0.1.3): (1) READ — просмотр таски на чтение (отрендеренный markdown/preview, без риска случайных правок, удобно читать контракт); (2) EDIT — режим редактирования. Сейчас таска открывается сразу в редактирование, читать контракт неудобно.
</goal>

<requirements>
- idea stub: обсуждается, спека не начата
- Read mode: rendered-предпросмотр тела таски (markdown + XML-секции церемонии), read-only по умолчанию
- Edit mode: явное действие (кнопка/таб) переключает в редактор; защита от случайных правок в read mode
- UX-вопрос на спеку: табы Read/Edit внутри одного окна vs два отдельных действия на карточке таски
- Учесть размер окна из 012 (edit-окна у нас «иллюминаторы») — read view тоже должен быть полноразмерным
</requirements>

<non-goals>
- Совместное редактирование / realtime
</non-goals>