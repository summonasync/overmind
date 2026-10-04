---
feature: 026
slug: entries-read-mode
status: inProgress
priority: p1
version: 2
created: 2026-09-20
---

# Feature 026 — Entries read mode: inline expand/collapse + fullscreen view by title click

<goal>
Read-модель для Entries (user request 2026-09-20, визуальный проход 0.1.8): сейчас entry открывается только в edit mode. Хочется:
1. Expand/collapse прямо в списке — строка entry раскрывается инлайн, тело рендерится полностью (markdown + цвета типов + тэги, «вся хуйня»).
2. Фулскрин read-просмотр — скорее всего клик по тайтлу открывает entry в полноэкранном read-only виде (как task read mode из 014), с кнопкой Edit оттуда.
</goal>

<requirements>
- idea stub: обсуждается, спека не начата
- Инлайн expand: клик по строке (или шеврону) раскрывает тело entry в списке; повторный — сворачивает; рендер как в preview (Markdown + type-цвета)
- Фулскрин: клик по тайтлу → xl read-only модалка (паттерн 014) с кнопкой Edit
- Edit-кнопка в строке остаётся как есть
- Не сломать现有的 edit-флоу
</requirements>

<non-goals>
- Изменения бэкенда (всё в entry response уже есть)
- Совместное редактирование/realtime
</non-goals>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
</log>
