---
feature: 087
slug: attention-page
status: validated
created: 2026-09-29
validated: 2026-09-29
---

# Spec 087 — Attention-страница в левом меню: все вопросы и attention агентов в одном месте (не только колокольчик)

## Goal

Заменить колокольчик-центр полноценной страницей **Attention** в левом меню: всё, что ждёт человека (awaiting approvals/questions, история уведомлений, зависшие церемонии проектов) — в одном сканируемом месте, с действиями inline. Колокольчик ужимается до unread-превью со ссылкой на страницу.

## User decisions (Socratic, 2026-09-29)

1. **Страница заменяет колокольчик с историей.** Вся текущая поверхность notification-center (история, kind-фильтры, ×-delete, mark read, read-all/clear-read, clear conversations, show hidden) переезжает на страницу. Колокольчик = ТОЛЬКО unreads + переход на страницу (без действий в поповере — действия живут на странице). Approval/question баблы (084/093, top-center) не трогаем — отдельная поверхность.
2. **Структура страницы: awaiting-зона сверху + история ниже.** Сверху закреплённая секция «Ждут тебя» (unanswered approvals + questions), ниже — история newest-first с существующими фильтрами.
3. **Источники: уведомления + зависшие церемонии.** Страница агрегирует (a) notifications (020/096/097-модель, без её изменения), (b) фичи проектов с зависшей церемонией. Blocked-фичи (030-зависимости) в awaiting-зоне НЕ фигурируют — только церемониальные стейты.
4. **#140 решается здесь:** серверный awaiting-фильтр + поднятый take для страницы (200). Полная пагинация — non-goal (объёмы малы, вернуться при росте).
5. **Нейминг: «Attention»** (левое меню англоязычное: Dashboard, Projects, Settings).

## Scope

### Web — страница /attention

- Новый роут `/attention` (App Router, RSC-shell + client-список), пункт «Attention» в левом меню с бейджем-счётчиком (тот же unreadCount, что у колокольчика).
- **Awaiting-секция «Waiting on you»** (сверху): unanswered approvals + questions (серверный фильтр `?awaiting=true`), каждая строка: kind-иконка, полный текст, агент/проект-контекст, возраст (post-086 parseInstant), действие inline — approve/reject (approval) или reply-инпут (question), deep-link по `url` (093) где есть. Пустая секция = компактный «all clear»-стейт.
- **История** (ниже): бывший notification-center целиком — kind-чипы, ×-delete, mark read, read-all/clear-read, clear conversations + show hidden архив, view-aware empty-стейты. Порядок строк newest-first.
- **Ceremonies-секция** (между awaiting и историей, сворачиваемая при пустоте): группы по проектам; строка = фича с зависшей церемонией + человекочитаемая причина + deep-link на фичу (диплинк-формат 093: `?project=&feature=`).
- Действия мутируют теми же хуками (use-notification-mutations) — семейная инвалидация notificationKeys.all покрывает bell, страницу и баблы (096-урок). Поллинг 017 — без изменений.

### Web — колокольчик (упрощение)

- Поповер: список unread-строк (kind-иконка, title, возраст; клик строки = deep-link если url есть) + кнопка «Open Attention». Бейдж-счётчик остаётся.
- Из поповера уходят: kind-чипы, действия (✓/✗/reply/×), read-all/clear-read, clear conversations, show hidden, notification-center как тело поповера.
- notification-row уезжает на страницу; dim-архивная семантика сохраняется; при переносе чинится a11y-SUGGESTION 097-02 (sr-only «Archived» для скрытых строк).

### Backend

- **Awaiting-фильтр:** `GET /api/notifications?awaiting=true` → `ReplyText == null && Kind IN (approval, question) && HiddenAt == null`, newest-first. Unanswered = не-проканониченный ответ (084: reply ⇒ read; ✓/✗ = reply). Список-эндпоинт получает take-параметр (дефолт 50 — текущее поведение, page-запрос шлёт 200).
- **Ceremonies read-endpoint:** `GET /api/attention/ceremonies` — один round-trip: по всем проектам фичи, где церемония ждёт человека. Причины (пересчитываются на сервере из существующих полей, без новых сущностей/колонок):
  - spec draft с непустым телом → «spec awaiting validation»;
  - plan draft при validated spec → «plan awaiting validation»;
  - feature в `review` → «result awaiting validation»;
  - feature `isBlocked` (030, незакрытые blockers) → «blocked by N».
  - `done`-фичи не показываются. Ответ: projectId, projectName, featureSlug, number, title, причины[], deepLink url (формат 093).
- Read-only эндпоинт; MCP-поверхность не нужна (человеческая зона). Notification model (сущность/колонки) не меняется.

## Non-goals

- Пагинация истории (см. D4), бесконечный скролл.
- OS-тосты/баннеры (095 — отдельная фича), борда-фильтр «require attention» (085).
- MCP/агентская дверь к ceremonies (человеческая поверхность; агент видит то же через planning_*).
- Изменение notification model, баблов 084/093, 077-инвариантов.
- Real-time push (поллинг 017 остаётся).

## Verification

- dotnet build + полный тест-сьют зелёный; новые пины: awaiting-фильтр (только unanswered approval/question, скрытые/answered не попадают), take-параметр (дефолт 50 не ломает существующие вызовы), ceremonies-эндпоинт (по одной причине на fixture-фичу, done не показывается, blocked-причина считается).
- Web: lint + tsc + npm test + build; пины: bell-unread-only рендер, awaiting-инпут/reply-флоу на странице, ceremonies-секция с мок-данными.
- Live e2e на изолированном хосте: вопрос → страница показывает в awaiting → reply со страницы → строка уезжает из awaiting (read-инвариант 084) → ceremonies-секция показывает валидированную тестом фичу → deep-link открывает фичу.

## Refs

- 084-notification-human-actions (done) — действия, read-инвариант, баблы.
- 093-approval-reach (done) — url-штамп в notify, диплинк-формат (?project=&feature=&tab=).
- 096/097 (done) — delete/read/clear/hide-механики, семейная инвалидация.
- #140 (микробатч) — закрывается серверным фильтром + take.
- 020 (notification entity/doors), 077 (proof), 017 (polling), 030 (isBlocked), 064/024 (web patterns).
- SUGGESTION 097-02 (sr-only «Archived») — чинится при переносе notification-row.

<log>
- 2026-09-29 [agent] — spec drafted from 5 Socratic decisions (surface split, awaiting+history layout, notifications+ceremonies sources, #140 raise-take+server filter, naming "Attention").
- 2026-09-29 [agent] — validated with human proof: notification 90e018d2-af1e-42eb-9ce8-ea42bb5045ee replied 2026-09-29 14:16:23 UTC
</log>