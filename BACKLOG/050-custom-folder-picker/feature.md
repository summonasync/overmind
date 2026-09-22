---
feature: 050
slug: custom-folder-picker
status: draft
priority: p2
version: 1
created: 2026-09-22
---

# Feature 050 — Custom folder picker в web UI: своя модалка вместо системного диалога (виндовый через powershell+WinForms — хуйня)

<goal>
Замена системного пикера папок на собственный в web UI. Сейчас `POST /system/browse-folder` (`SystemApi.cs`) спавнит внешний процесс: на Windows это `powershell -Sta` с WinForms `FolderBrowserDialog` — легендарно древний диалог (дерево без адресной строки и quick access), окно без владельца всплывает где попало и прячется за браузером, плюс лишний powershell-процесс на каждый клик (и потенциальные AV/GPO-триггеры на спавн). Пользователь назвал это прямо: «он в винде хуйню творит». Кастомный пикер в UI даёт одинаковый предсказуемый UX на всех трёх ОС и убирает спавн процессов.
</goal>

<requirements>
- Сервер: read-only эндпоинт листинга директорий — дети папки (имя, is-dir, скрытость), корни системы: Windows — логические диски, Linux — `/` и `$HOME`, macOS — `/Volumes` и `$HOME`.
- Фронт: модальный пикер — навигация по дереву, breadcrumbs, назад, ручной ввод пути с валидацией, текущий выбор виден целиком.
- Отказ в доступе к папке (UnauthorizedAccess и т.п.) — пустой список с пометкой, не падение и не пустой модалка-тишина.
- Заменяет `browseFolder()` во всех трёх поверхностях: `project-form.tsx` (регистрация проекта), `project-tree.tsx` (tree roots), `kits-page.tsx` (импорт китов).
- Нативный путь (`/system/browse-folder`) остаётся как fallback — решение о полном сносе в спеке (Linux zenity/kdialog работают нормально).
- Дизайн — в существующем стиле приложения, модалка поверх текущих форм без смены их флоу.
</requirements>

<non-goals>
- Выбор файлов (там, где нужны файлы — не трогаем).
- Создание/переименование папок из пикера.
- Закладки/избранное в первой итерации — кандидат на потом по полевому спросу.
</non-goals>

<refs>
- `src/Overmind.Host/Api/SystemApi.cs:175-188` — текущий спавн powershell+FolderBrowserDialog (диалог-лок, таймаут, чтение stdout).
- `web/src/shared/api/system.ts:8` — клиент; вызовы: `web/src/features/projects/components/project-form.tsx:36`, `web/src/features/projects/components/project-tree.tsx:70`, `web/src/pages/kits-page/ui/kits-page.tsx:37`.
- Полевой контекст: юзер бьётся об это на винде в живой работе (2026-09-22), вокруг прогона 003b-инсталлера.
</refs>