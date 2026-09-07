# Overmind

> Один разум на весь рой. All your agents share one mind — finally on purpose.

## RU — Что это вообще

**Overmind** — десктопное приложение (Windows/macOS, .NET), которое даёт твоим AI-агентам — Claude Code, Kimi Code, Codex, Gemini CLI, Antigravity — общий постоянный контекст. Заметки, архитектурные решения, гейты, гварды и скилл-киты живут в одной локальной базе, а не пересказываются каждому агенту в каждой сессии заново.

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

### Статус

Pre-alpha. Сейчас в репо живут спека, план и бэклог — см. `BACKLOG/001-overmind-mvp/`. Код появляется по мере выполнения плана.

### Roadmap

| Прио | Скоп |
|------|------|
| v1 (MVP) | Мульти-проектное хранилище контекста, заметки/решения/гейты/гварды/скиллы, FTS + опциональная семантика, трей + веб-UI, stdio MCP-адаптер, материализация китов в проект |
| P1 | Материализация китов во внешнюю папку + лаунчер агентов (`claude --add-dir`, `kimi --skills-dir`) |
| P2 | Рантайм-доставка скиллов через MCP (`get_skill`, `overmind://skills/`, bootstrap-скилл) |
| P2/P3 | Флоу инициализации агентов, онбординг проектов |

---

## EN — Short version

**Overmind** is a Windows/macOS desktop app (.NET) that gives AI coding agents — Claude Code, Kimi Code, Codex, Gemini CLI, Antigravity — a shared, persistent context store. Project notes, decisions, gates, guards, and skill-kits live in one local database instead of being re-explained to every agent in every session.

- Single tray app process hosting a localhost HTTP API and a web UI — the browser is the window.
- Agents connect via a thin stdio MCP adapter; context is addressable through a `overmind://` URI scheme (`overmind://projects/<id>/gates`).
- Skill-kits are managed in the DB and materialized into projects' native skill directories (`.claude/skills/`, `.kimi-code/skills/`, …). Disk is a managed cache, the DB is the source of truth.
- Full-text search (SQLite FTS5) always works, offline, zero setup. Semantic search via local Qdrant (Docker) is optional — pick an embedding provider in settings (local ONNX / Ollama / OpenAI-compatible).

Inspired by [OpenViking](https://github.com/volcengine/OpenViking)'s context-as-a-filesystem idea, rebuilt to be download-and-run.

**Status:** pre-alpha — spec and backlog live in `BACKLOG/001-overmind-mvp/`, code is landing per plan.

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

## Repository layout

```
BACKLOG.md                  feature index
BACKLOG/001-overmind-mvp/   MVP feature folder
  feature.md                what and why
  spec.md                   approved design decisions and requirements
  plan.md                   implementation plan
  tasks/                    execution contracts per task
```

## License

TBD
