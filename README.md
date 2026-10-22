# Overmind

> Один разум на весь рой. All your agents share one mind — finally on purpose.

## RU — Что это вообще

**Overmind** — десктопное приложение (Windows/macOS/Linux, .NET), которое даёт твоим AI-агентам — Claude Code, Kimi Code, Codex, Gemini CLI, Antigravity — общий постоянный контекст. Заметки, архитектурные решения, гейты, гварды и скилл-киты живут в одной локальной базе, а не пересказываются каждому агенту в каждой сессии заново.

### История

Как-то раз один разработчик увидел [OpenViking](https://github.com/volcengine/OpenViking) — контекстную базу для агентов от Volcengine с файловой парадигмой `viking://`. Идея понравилась настолько, что захотелось свою. Не понравилась только настройка: внешние сервисы, ключи, конфиги — слишком много движений ради «запомни, что мы решили».

Так родился план: та же идея, но **download-and-run**. Скачал — запустил — работает. Без обязательного Docker'а, без ключей, без квестов.

### Имя

Название выбиралось турниром на четыре раунда. В финале сражались `shared-braincell`, `the-agents-dungeon`, `mimirs-head`, `the-construct` и другие достойные. Победил **Overmind** — разум из StarCraft, который управляет роем зерглингов. Потому что ровно это и происходит: твои Клоды и Кими — зерглинги, а тут у них одна голова на всех.

### Фишка

- **Один процесс** — трей-апп на .NET, который поднимает локальный HTTP API и отдаёт веб-UI. Браузер — это и есть окно приложения.
- **Агенты подключаются по стандарту** — тонкий stdio MCP-адаптер, который любой MCP-клиент спавнит одной строкой в конфиге. Контекст адресуется своей URI-схемой: `overmind://projects/<id>/gates`.
- **Скилл-киты** — поименованные наборы скиллов/гейтов/гвардов. Активировал кит на проект — приложение само материализует файлы в `.claude/skills/`, `.kimi-code/skills/` и т.д. База — источник правды, диск — управляемый кэш. Переключил кит — файлы пересобрались.
- **Поиск всегда работает** — SQLite FTS5 из коробки, офлайн, ноль зависимостей. Хочешь семантику — подними Qdrant в Docker, выбери embedding-провайдера в настройках (локальный ONNX / Ollama / OpenAI-совместимый). Нет Docker'а — честный баннер «векторный поиск выключен», FTS продолжает пахать.
- **Проекты как дерево папок** — в сайдбаре живое дерево диска: обычные папки приглушены, проекты кликабельны, регистрация проекта прямо на месте. Корни дерева задаёт пользователь, клик по проекту делает его активным — записи дальше открываются в его контексте.

### Статус

Альфа. MVP отгружен (`001-overmind-mvp`, 9 тасков: HTTP API, скилл-киты с материализацией, FTS + опциональная семантика, MCP-адаптер, веб-UI, трей-хост), сверху легли `004-projects-tree-view` и `005-tree-workspace`. В работе `006-planning` — церемония feature → spec → plan → task → result как сущность первого класса (готовы 3 из 11 тасков: домен, схема, human door API). Дальше `002-kit-library`, затем `003-bundled-installer`. Живой срез — на борде планирования (веб-UI или MCP); `BACKLOG/` — материализованное зеркало контрактов.

### Roadmap

| Прио | Скоп | Статус |
|------|------|--------|
| v1 (MVP) | Мульти-проектное хранилище контекста, заметки/решения/гейты/гварды/скиллы, FTS + опциональная семантика, трей + веб-UI, stdio MCP-адаптер, материализация китов в проект | ✅ отгружен (`001`) |
| Дерево проектов | Сайдбар-дерево реальных папок, регистрация на месте, пользовательские корни + активный проект | ✅ отгружен (`004`, `005`) |
| Церемония планирования | feature → spec → plan → task → result в БД, agent door + MCP-поверхность, веб-редактор, материализация в репо (off/on/archive), импорт существующего `BACKLOG/` | 🔄 в работе (`006`, 3/11) |
| Библиотека китов | Импорт китов из локальной папки или git-ссылки (включая приватные репо с сохранёнными креденшелами) | ⬜ в плане (`002`) |
| Установщик | Инсталлятор win/mac с курируемыми китами в комплекте, едет на пайплайне импорта `002` | 🔒 заблокирован (`003`) |
| P1 | Материализация китов во внешнюю папку + лаунчер агентов (`claude --add-dir`, `kimi --skills-dir`) | ⬜ отложен |
| P2 | Рантайм-доставка скиллов через MCP (`get_skill`, `overmind://skills/`, bootstrap-скилл) | ⬜ отложен |
| P2/P3 | Флоу инициализации агентов, онбординг проектов | ⬜ отложен |

---

## EN — Short version

**Overmind** is a Windows/macOS/Linux desktop app (.NET) that gives AI coding agents — Claude Code, Kimi Code, Codex, Gemini CLI, Antigravity — a shared, persistent context store. Project notes, decisions, gates, guards, and skill-kits live in one local database instead of being re-explained to every agent in every session.

- Single tray app process hosting a localhost HTTP API and a web UI — the browser is the window.
- Agents connect via a thin stdio MCP adapter; context is addressable through a `overmind://` URI scheme (`overmind://projects/<id>/gates`).
- Skill-kits are managed in the DB and materialized into projects' native skill directories (`.claude/skills/`, `.kimi-code/skills/`, …). Disk is a managed cache, the DB is the source of truth.
- Full-text search (SQLite FTS5) always works, offline, zero setup. Semantic search via local Qdrant (Docker) is optional — pick an embedding provider in settings (local ONNX / Ollama / OpenAI-compatible).
- Projects are browsed as a real disk folder tree in the sidebar, with user-defined roots and register-in-place; clicking a project makes it the active context.

Inspired by [OpenViking](https://github.com/volcengine/OpenViking)'s context-as-a-filesystem idea, rebuilt to be download-and-run.

**Status:** alpha — the MVP is shipped (`001-overmind-mvp`), plus the projects tree (`004`, `005`) and the planning ceremony (`006` and successors). The latest wave (`137`) shipped parallel-wave coordination — lane rosters, claim-time lane checks, a one-call wave snapshot, the directive channel and entry read-acks — bringing the MCP surface to 55 tools. The planning board (web UI / MCP) is the live index; the `BACKLOG/` tree holds the materialized contracts.

## Запуск

```bash
dotnet run --project src/Overmind.Host
```

Хост — один процесс: проверка единственного экземпляра, SQLite, выбор порта, Kestrel, трей. Повторный запуск при живом инстансе не поднимает второй процесс — он открывает браузер на адресе существующего и завершается.

Что происходит при старте: создаётся `~/.overmind/` → база SQLite (миграции автоматически) → порт (по умолчанию 4761, если занят — первый свободный) → запись `~/.overmind/host.port` → Kestrel → иконка в трее. Меню трея: **Open Overmind** (веб-UI в браузере по умолчанию) и **Quit** (плановая остановка, порт освобождается). Остановка также работает по Ctrl+C / SIGTERM. Любая ошибка старта — сообщение в консоль и ненулевой код выхода.

UI открывается на `http://overmind.localhost:<port>` — хостнейм `.localhost` резолвится в loopback во всех современных браузерах без настройки. API и MCP-адаптер продолжают ходить на `127.0.0.1` напрямую.

- **База данных**: `~/.overmind/overmind.db` (SQLite + FTS5, создаётся сама).
- **Файл порта**: `~/.overmind/host.port` — по нему MCP-адаптер и повторный запуск находят живой инстанс.
- **Headless-режим** (без трея, для отладки/CI): `dotnet run --project src/Overmind.Host -- --no-tray`.
- **Известное ограничение macOS**: иконка трея не появляется при запуске через `dotnet run` вне подписанного `.app`-бандла. Упаковка — post-MVP.

## Установка на Windows

Готовый инсталлятор — `overmind-<версия>-setup.exe` (Inno Setup), собирается `build/windows/pack-win.ps1` на Windows-машине.

- **Скачивание и SmartScreen.** Билд не подписан (нет сертификата — личный инструмент, не коммерческий продукт), поэтому при запуске Windows покажет "Windows protected your PC" / SmartScreen. Это ожидаемо, не баг: жми **More info** → **Run anyway**, чтобы продолжить установку.
- **Мастер установки.** По умолчанию — установка для текущего пользователя (`%LocalAppData%\Programs\Overmind`, без прав администратора); есть опция поставить на всю машину (`C:\Program Files\Overmind`, требует UAC). Четыре задачи в визарде: **автозапуск при входе в систему** (галка по умолчанию — добавляет `Overmind` в `HKCU\...\Run`), **добавить `overmind-mcp` в PATH** (галка по умолчанию — чтобы `overmind-mcp` можно было запустить из любой оболочки по короткому имени), **зарегистрировать Overmind у AI-агентов на этой машине** (галка по умолчанию — запускает `overmind-mcp install`, который **пишет в конфиг-файлы агентов**: адаптер детектит, какие из шести поддерживаемых агентов реально стоят на машине, и правит конфиг только у них; в типичном случае это Claude Code и/или Kimi Code — конкретный набор зависит от того, что у тебя установлено), **ярлык на рабочем столе** (без галки по умолчанию). Ярлык в Пуск создаётся всегда. Регистрацию можно потом добавить или снять вручную в любой момент: `overmind-mcp install` / `overmind-mcp install --remove` (лежит в `{папка установки}\mcp\`, после установки на PATH — если включена задача PATH — доступен как `overmind-mcp` из любой оболочки). **Уже запущенный агент новую регистрацию не увидит** — конфиг он читает при старте, так что после установки (или после ручного `install`) агента (напр. Claude Code) нужно перезапустить. **Важно для установки на всю машину:** подтверждай UAC-запрос своим собственным админ-аккаунтом, а не чужим — автозапуск, поиск порт-файла при апгрейде и диалог удаления данных при аноинсталле ориентируются на `%USERPROFILE%`/`HKCU` того аккаунта, который согласился на элевацию, а не на аккаунт, из-под которого реально работаешь; если это разные аккаунты, автозапуск тихо зарегистрируется не туда, а плавная остановка Host'а при апгрейде молча не сработает.
- **Удаление и регистрация агентов.** Деинсталлятор перед удалением файлов сам вызывает `overmind-mcp install --remove`, снимая регистрацию у тех же агентов — так после удаления Overmind ни один агент не остаётся с записью на уже не существующий exe. Если сама регистрация/снятие регистрации у агента не удалась (например, конфиг-файл занят другим процессом) — это не блокирует ни установку, ни удаление: приложение прекрасно работает и без регистрации у агентов, это просто дополнительное удобство.
- **Обновление.** Повторный запуск `setup.exe` поверх существующей установки — апгрейд на месте: инсталлятор сам останавливает работающий Host через `POST /api/shutdown` (плановая остановка, не `taskkill`) перед заменой файлов, затем может перезапустить.
- **Удаление.** Через "Установленные приложения" / Панель управления, как обычная программа. Инсталлятор спрашивает отдельным диалогом, удалять ли пользовательские данные (`%USERPROFILE%\.overmind` — база, настройки, скачанные ONNX-модели); **по умолчанию — Нет**, данные остаются на диске для следующей установки. Тихое удаление (`/VERYSILENT`) никогда не трогает `~/.overmind`, если явно не передан `/DELETEDATA`.
- **Данные пользователя** всегда лежат в `%USERPROFILE%\.overmind`, а не в папке установки — переживают и апгрейд, и обычное удаление.

### Подключение агентов (MCP)

Пример конфигурации MCP-клиента (Claude Code, Kimi Code и т.д.):

```json
{
  "mcpServers": {
    "overmind": {
      "command": "dotnet",
      "args": ["run", "--project", "src/Overmind.Mcp"],
      "env": { "OVERMIND_PROJECT_ID": "<project-id>" }
    }
  }
}
```

`OVERMIND_PROJECT_ID` — id проекта (виден в UI или через `GET /api/projects/`); без него адаптер возьмёт единственный существующий проект. Адрес API задавать не нужно: адаптер сам находит хост через `~/.overmind/host.port`. Контекст читается агентом по URI-схеме `overmind://projects/<id>/gates`, `overmind://projects/<id>/guards` и т.д.

#### Идемпотентные повторы мутаций (X-Operation-Id)

Мутирующие planning-инструменты принимают опциональный параметр `operationId` (GUID): `planning_upsert_artifact`, `planning_upsert_tasks`, `planning_patch_artifact`, `planning_set_status`, `planning_append_log`, `planning_edit_log`, `planning_set_feature_status`, `planning_update_feature`, `planning_set_feature_priority`. Он уезжает заголовком `X-Operation-Id`, и Host ведёт реестр ответов (24 часа):

- **тот же `operationId` + тот же payload** → Host вернёт сохранённый ответ с `"replayed": true`; повторного применения нет, версия не растёт;
- **тот же `operationId` + другой payload** → `409 OperationIdConflict` — id минтся заново под каждую логическую операцию;
- **не-GUID** → `400 InvalidOperationId`;
- сохраняются только ответы 2xx — упавшая мутация не запоминается, повтор с тем же id выполнится по-настоящему.

Контракт ретраев для агента: транспортная ошибка адаптера всегда содержит маркер `retryable transport failure:` — только этот класс ошибок стоит ретраить, и только повтором **того же** вызова с тем же `operationId`. Бизнес-ошибки (4xx, `STOP:`-гейты) маркер не несут — их ретраить бессмысленно. С `operationId` повтор идемпотентен в любом исходе: Host либо replayнет сохранённый ответ, либо честно ответит `409`. Текст ошибки после маркера может добавлять вердикт read-back-а адаптера («write committed at vN — do not resend» / «no trace» / «outcome unknown») — при живом `operationId` он ни на что не влияет, replay защищает и так.

Провода совместимы в обе стороны: без `operationId` заголовок вообще не отправляется; **старый Host** просто игнорирует незнакомый заголовок; **новый Host со старым адаптером** работает как раньше. Lockstep не требуется — но replay возможен только когда обе стороны новые.

## Repository layout

```
BACKLOG/nnn-slug/             one folder per feature
  feature.md                  what and why
  spec.md                     validated design decisions and requirements (frozen)
  plan.md                     validated implementation plan (frozen)
  tasks/NN-slug.md            execution contract per task
CONTEXT.md                    system design: components, communication rules, AI framework
STRUCTURE.md                  layout + architecture rules + code conventions (read before writing code)
TECHNICAL-SOLUTIONS.md        ADRs: what was chosen over what, and why
audits/                       skill-produced reports, named <skill>-*.md
design/                       brand and sprite sources
src/Overmind.Core/            domain: entities, enums, abstractions — zero dependencies
src/Overmind.Infrastructure/  EF Core (SQLite + FTS5), vector search (Qdrant), embeddings, materialization, planning services
src/Overmind.Host/            composition root: loopback Kestrel, minimal APIs, tray, SPA hosting
src/Overmind.Mcp/             stdio MCP adapter — no project references, talks HTTP to the Host
tests/                        mirrors src: Core / Infrastructure / Api / Mcp tests
web/                          Next.js 16 static-export SPA (FSD), served by the Host
```

## License

TBD
