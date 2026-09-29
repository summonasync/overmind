---
feature: 087
task: 01
slug: attention-page
status: done
review: light
created: 2026-09-29
brief: Backend: ?awaiting=true + take (дефолт 50, #140) + GET /api/attention/ceremonies (read-only, причины из существующих полей, диплинк 093) + пины
---

<goal>Backend read-surface для 087: awaiting-фильтр списка уведомлений + take-параметр (закрывает #140) + read-only ceremonies-эндпоинт по всем проектам.</goal>
<scope>
- NotificationsService.GetOverviewAsync: опциональный awaiting-предикат `ReplyText == null && Kind IN (approval, question) && HiddenAt == null` + параметр take (int, дефолт 50). Фильтрация в SQL (EF-транслируемая), не в памяти. unreadCount не трогается.
- NotificationsApi (list): query-параметры `?awaiting=true` и `?take=` (валидация: 1..500). Дефолтное поведение (без параметров) байт-совместимо с текущим.
- Новый read-only эндпоинт `GET /api/attention/ceremonies` + сервис (Infrastructure): по всем проектам фичи, ждущие человека. Причины из существующих полей: spec draft с непустым телом → "spec awaiting validation"; plan draft при validated spec → "plan awaiting validation"; feature.Status == review → "result awaiting validation"; незакрытые PlanningFeatureDependency (blocker не done/review) → "blocked by N". done-фичи не показываются. SQL-фильтрация, не in-memory (R1).
- Ответ ceremonies: projectId, projectName, featureSlug, number, title, reasons[], url (диплинк-формат 093: ?project=&feature=) — собрать через существующий NotificationLinks/BrowserUri хелпер, НЕ третья копия форматера (R3).
- Пины в Overmind.Api.Tests: awaiting отбирает только unanswered approval/question (answered reply, hidden, info, read — не попадают); take-дефолт 50 сохраняется для старых вызовов; take=200 отдаёт больше; ceremonies — по причине на фикстурную фичу, done не показывается, blocked-причина с числом; url в ceremonies-ответе в формате 093.
- Program.cs: одна строка маппинга нового API.
</scope>
<anti-scope>
- Notification model/колонки/миграции — ноль.
- MCP-поверхность ceremonies — нет (человеческая зона, спека).
- 077-двери (replies, validate) — не трогать.
- Пагинация/курсоры — нет.
</anti-scope>
<constraints>
- dotnet build 0/0, полный тест-сьют зелёный, dotnet format --verify-no-changes на тронутых файлах.
- Идиомы файла NotificationsService сохранены (set-based, parameterized, DateTime.UtcNow один раз).
- Комментарии только для неочевидных решений, English-only.
</constraints>
<acceptance-criteria>
- [x] GET /api/notifications?awaiting=true возвращает только unanswered approval/question (hidden/answered/read/info исключены) — пин
- [x] take-параметр: дефолт 50 (старые вызовы байт-совместимы), take=200 отдаёт окно больше — пин
- [x] GET /api/attention/ceremonies: spec/plan/review/blocked-причины считаются по fixture-фичам, done не показывается — пины
- [x] ceremonies url в диплинк-формате 093 (?project=&feature=) — пин
- [x] dotnet build 0/0 + полный сьют зелёный
</acceptance-criteria>
<refs>
- spec 02d54b36 (validated), plan b5d2204e (validated), D1-D3/R1/R3 плана.
- 093-approval-reach: url-формат + NotificationLinks.
- 097: HiddenAt-семантика, includeHidden-шейп.
</refs>
<obstacles>
- AC-формулировка «read исключены» vs предикат спеки: валидированный предикат (spec Backend-секция, продублирован в scope) — `ReplyText == null && Kind IN (approval, question) && HiddenAt == null`, ReadAt в нём отсутствует; spec-пояснение: «Unanswered = не-проканониченный ответ (084: reply ⇒ read)». Реализовано строго по предикату: строка «прочитана, но без ответа» (mark-read без reply) ОСТАЁТСЯ в awaiting — только reply канонизирует ответ. Пин List_AwaitingTrue_ReturnsOnlyUnansweredApprovalAndQuestion фиксирует это явно (readUnanswered присутствует). Если человек хочет исключать read-unanswered — это одна строка предиката + правка пина.
- Секция obstacles у таски была пустой (`<obstacles></obstacles>`) и не находилась planning_patch_artifact (400: section not found) — obstacles добавлены полным upsert тела, AC-тики и лог сохранены.
</obstacles>
<log>
- 2026-09-29 [agent] — task sliced from validated plan; runs first (01 → 02 → 03).
- 2026-09-29 [agent] — backend task picked up: awaiting filter + take + ceremonies endpoint
- 2026-09-29 [agent] — implementation done: full-body upsert to add obstacles section (empty section not patchable); AC checkboxes and stored log preserved
- 2026-09-29 [agent] — implemented: awaiting predicate + take (1..500, default 50) in GetOverviewAsync/List; GET /api/attention/ceremonies via new CeremoniesService (SQL-side EXISTS/IN filtering, reasons computed on the filtered set, url via NotificationLinks in Host); pins added (awaiting, take default/widen/400, ceremonies reasons/done-excluded/blocked-count/url). build 0/0; full suite green 1656 passed (Core 225, Infrastructure 535, Api 508, Mcp 388); dotnet format --verify-no-changes clean on touched files
- 2026-09-29 [agent] — backend task ready for review: awaiting+take filter, ceremonies endpoint, pins; build 0/0, full suite green, format clean; 5/5 AC ticked; one interpretation note in obstacles (read-unanswered stays in awaiting per validated predicate)
- 2026-09-29 [agent] — light review PASS-WITH-NITS (audits/review-087-01.md): 0 BLOCKER/IMPORTANT, 2 NIT → микробатч (#143 дупликация ceremony-предикатов SQL/memory, #144 unpinned awaiting+includeHidden combo); арбитраж ReadAt — корректно по валидированному предикату, пин фиксирует; независимый прогон 1656/1656 — таск done
</log>