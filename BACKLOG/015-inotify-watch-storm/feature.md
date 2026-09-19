---
feature: 015
slug: inotify-watch-storm
status: draft
version: 1
created: 2026-09-19
---

# Feature 015 — Bug: host + mcp adapter inotify-watch the whole cwd tree

<goal>
Bug (live dogfood evidence 2026-09-19, другой агент + наша верификация): каждый overmind-mcp инстанс держит inotify-вотч на КАЖДУЮ директорию дерева своего cwd (живые замеры: kimi-инстанс 2955 = числу директорий репо Overmind; спейры Mindlash 10.7k/6.3k), а Overmind.Host — 43 312 вотчей, потому что его cwd при запуске был /home/johan-boolman (44 001 директория). Флот агентов + хост съедают лимит fs.inotify.max_user_watches (65536) под завязку — тесты Squeezr падают на нехватке watches. Корень: Host.CreateApplicationBuilder / WebApplication.CreateBuilder ставят content root = cwd процесса, а PhysicalFilesWatcher за reloadOnChange appsettings вешает вотч на каждую директорию дерева content root'а (доказано strace: адаптер из /tmp/wtest с 3 директориями = ровно 3 вотча). Починить оба бинаря: контент-рут не должен быть чужим проектом/домашкой.
</goal>

<requirements>
- idea stub: обсуждается, спека не начата
- Адаптер (Overmind.Mcp): конфиг ему не нужен — запинить ContentRootPath (HostApplicationBuilderSettings) на AppContext.BaseDirectory или отключить reloadConfigOnChange; вотчей на проект агента быть не должно вообще (приёмка: strace-замер ~0-2 вотчей из любого cwd)
- Хост (Overmind.Host): то же лечение, но аккуратно — appsettings.json грузится рядом с бинарём, а fallback webRoot резолвится от ContentRootPath (src/Overmind.Host/Program.cs:84-87); run.sh пинает OVERMIND_WEB_ROOT, dev-режим (dotnet run) — нет. Решение на спеку: pin ContentRootPath + поправить webRoot fallback, или reloadConfigOnChange=false
- Тест/страж: автотест или smoke-чек, что процесс не регистрирует рекурсивных вотчей (strace/fdinfo-чек в build/smoke.sh?)
- Дока: run.sh/install.sh заметка — cwd при запуске больше не влияет
</requirements>

<non-goals>
- Системный sysctl (fs.inotify.max_user_watches) — это гигиена машины юзера, не код продукта
- Управление флотом чужих агентов (claude spares) — не наша зона
</non-goals>