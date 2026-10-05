---
feature: 071
slug: backlog-import-ui
status: validated
created: 2026-10-05
validated: 2026-10-05
---

# Result 071 — Backlog import через UI: кнопка на странице настроек проекта (scan-превью → импорт → отчёт), HTTP-обёртка BacklogImporter

Волна закрыта: человек импортирует BACKLOG-папки в проект прямо из веба — scan-превью → импорт папки → отчёт, поверх того же `BacklogImporter`, что и агентская дверь. 3/3 таска done с ревью; архитектор light APPROVED-WITH-WARNINGS (0 блокеров).

## Что село

- **01 — HTTP-двери** (1548c2a): human-дверь `PlanningImportApi` (POST scan с per-entry состояниями + POST import, PathGuard-семантика) и агентская `AgentPlanningApi` (ParseFeatureAsync + ImportFeatureAsync) — обе поверх единственного DI-зарегистрированного `BacklogImporter`; scan — human-only по спеке. Ревью PASS-WITH-NITS (гейты ревьюера: Api 607/0, Infra 611/0).
- **02 — Web UI** (a94963a + фикс f9f4b2e): секция «Backlog import» в проектном detail-модале (это settings-сёрфейс приложения): Scan-кнопка, список папок с бейджами new / already imported / error, expandable-превью, per-folder Import; re-import — через inline warning-strip, называющую состояние (вложенный Modal дрался бы за Escape с детальным модалом — modal.tsx вешает window keydown на каждый открытый Modal; отклонение подтверждено ревью и запинено тестом). После импорта — зелёные report-строки и авто-рескан. Ревью PASS-WITH-NITS, W1 закрыт: session-local set импортированных папок держит confirm через лаг рескана (+4 пина, 138/138).
- **03 — Closeout** (8d89544 + f67669f): консистенс-пасс записан (таблица дверей: один импортер, единственный маппер репорта, двухслойный containment — валидатор + PathGuard внутри импортера, отказы вне рута 400 в обеих дверях; claim-гейт для бессессионного человека — осознанный no-op). Архитектор light: **APPROVED-WITH-WARNINGS**, W1 wave-среза закрыт на месте — три приватные копии маппера `MaterializationException→IResult` схлопнуты в общий `MaterializationErrors.Map` (KitsApi мапил `Refused` в 500, планировочные — в 409; теперь канон один), N2 закрыт (re-import инвалидирует feature-detail квери).

## Гейты финального дерева

- web: lint 0 ошибок / 6 pre-existing warnings, tsc 0, тесты **138/138** (+25 новых за волну), build 12/12.
- dotnet: build 0 warnings, **Api 623/623** (полный прогон на финальном дереве), Infra 633/633, Core 316/316; Mcp 451/451 (дерево соседней волны 104).

## Post-release click-check (по плану, после деплоя)

1. Открыть проект → detail-модал → секция Backlog import → Scan на реальном проекте (Squeezr) — список папок с бейджами, ошибки парсинга видны в строках.
2. Import одной новой папки → зелёная report-строка, бейдж переворачивается в «already imported» после авто-рескана, карточка появляется на борде, зеркало материализуется.
3. Import уже импортированной папки → confirm-полоса называет состояние и фичу; Cancel ничего не пишет.

## Хвосты (осознанные, в микробатч-леджер)

- **Carried caveat 071/01**: ParseFeatureAsync регистрирует неизвестные frontmatter-ключи в БД — сайд-эффект за рамкой «no writes» спеки; обе двери несут одинаковое поведение, кавеат записан на спеке (frozen).
- Микробатч: arch-N1 (import-секция слепа к claim-стейтусу — человек может переимпортить фичу живого агента), arch-N3 (ImportFolderList в барреле без потребителей), review-N1 (параллельный импорт двух папок технически возможен), review-N2 (confirm-полоса без aria-live), review-N3 (фокус после импорта), попутно — Escape-изоляция `modal.tsx` (#206).

<log>
- 2026-10-05 [agent] — Result drafted: 3/3 tasks done (01 PASS-WITH-NITS, 02 PASS-WITH-NITS + W1 fixed, 03 closeout with architect APPROVED-WITH-WARNINGS + W1/N2 fixed in-tree). Gates on final tree: web lint 0e/6w, tsc 0, 138/138, build 12/12; dotnet build 0w, Api 623/623. Post-release click-check checklist inside. Waiting for the human tap.
</log>