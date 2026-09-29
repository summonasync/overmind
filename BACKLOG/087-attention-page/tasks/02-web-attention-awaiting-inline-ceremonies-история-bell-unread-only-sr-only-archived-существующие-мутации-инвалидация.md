---
feature: 087
task: 02
slug: attention-page
status: done
review: light
created: 2026-09-29
depends_on: 01
brief: Web: /attention (awaiting inline + ceremonies + история) + bell unread-only + sr-only Archived; существующие мутации/инвалидация
---

<goal>Web-поверхность 087: страница /attention (awaiting + ceremonies + история) и худеющий колокольчик (unread-only + переход).</goal>
<scope>
- Новый роут /attention (App Router), пункт "Attention" в левом меню с бейджем (существующий unreadCount — единый источник, R2).
- Awaiting-секция "Waiting on you": запрос notifications?awaiting=true&take=200; строка: kind-иконка, полный текст, контекст, возраст (parseInstant, пост-086), inline-действия — approve/reject (approval) или reply-инпут (question), deep-link по url где есть. Пустая секция — компактный "all clear".
- Ceremonies-секция: запрос /api/attention/ceremonies, группы по проектам, человекочитаемая причина + deep-link. Сворачивается при пустоте.
- История: существующая поверхность notification-center переезжает на страницу (kind-чипы, ×-delete, mark read, read-all/clear-read, clear conversations + show hidden, view-aware empty-стейты). Действия — теми же хуками use-notification-mutations; семейная инвалидация notificationKeys.all покрывает bell/страницу/awaiting (096-урок, spec D5).
- Bell-поповер: только unread-строки (kind-иконка, title, возраст, клик = deep-link) + кнопка "Open Attention". Уходят из поповера: kind-чипы, все действия, notification-center как тело.
- notification-row переезжает на страницу; sr-only "Archived" для скрытых строк (a11y-SUGGESTION 097-02).
- Пины web/tests: bell рендерит только unread + Open Attention; awaiting reply/approve-флоу на моках; ceremonies-секция на моках; parseInstant-возраст.
</scope>
<anti-scope>
- Баблы 084/093 — не трогать.
- 085-борда-фильтр require attention — не тут.
- Новые npm-пакеты — ноль.
- Пагинация — нет (spec non-goal).
</anti-scope>
<constraints>
- FSD: страница в app/, переиспользуемое в features/notifications + widgets; кросс-фичевые импорты через баррель.
- RSC по дефолту, 'use client' только для интерактива/хуков; React Query v5, без useEffect-фетчинга.
- npm run lint 0 errors, npx tsc --noEmit clean, npm test зелёный, npm run build зелёный.
</constraints>
<acceptance-criteria>
- [x] /attention рендерит awaiting-зону (действия inline), ceremonies-группы, историю с фильтрами; empty-стейты все три
- [x] Bell-поповер: только unread + Open Attention, действий нет; бейдж живёт на существующем unreadCount
- [x] Reply/approve со страницы уводит строку из awaiting (084 read-инвариант через существующие мутации)
- [x] Скрытые строки в архиве несут sr-only "Archived"
- [x] lint + tsc + npm test + build зелёные; существующие пины нетронуты
</acceptance-criteria>
<refs>
- spec 02d54b36 (validated), plan b5d2204e (validated), D4/D5/R2 плана.
- 096/097: notificationKeys family, use-notification-mutations, view-aware empty-стейты.
- 093: url-штамп в NotificationResponse — deep-link строк.
</refs>
<obstacles>
- notification-center физически остался в widgets/app-shell (не переехал файлом): его же рендерит approval-bubble 084 (anti-scope — бабл не трогаем), а widgets не может импортировать из pages. Переезд реализован на уровне поверхности: bell больше не хостит центр, страница /attention рендерит его как History-секцию (pages → widgets, направление слоёв чистое).
- notification-href переехал в features/notifications/lib (домен уведомлений, нужен bell-строкам + обеим строкам), на старом пути widgets/app-shell/lib/notification-href.ts оставлен однострочный ре-экспорт — approval-bubble импортит его оттуда, файл бабла не тронут.
- Ceremonies-empty по спеке «сворачивается при пустоте» → секция рендерит null (это её empty-стейт); AC-формулировка «empty-стейты все три» закрыта: awaiting — компактный all-clear, ceremonies — коллапс, история — view-aware стейты notification-center.
- Экшены (approve/reject/reply) вынесены в общий NotificationActions (features/notifications/components) — его рендерят и notification-row (история), и awaiting-row; дубликация ~60 строк JSX убрана на переезде.
- Reply/approve уводит строку из awaiting через invalidate notificationKeys.all (D5): оптимистический patch в use-notification-mutations патчит только list-ключ, awaiting-зона обновляется после рефетча (~1 RTT). Микробатч-кандидат: расширить patchList на всю семью через setQueriesData.
- unread-бейдж в сайдбаре/мобайл-меню сидит на том же useNotifications (единый источник с bell, R2) — ещё один подписчик существующего опроса, новых запросов нет.
</obstacles>
<log>
- 2026-09-29 [agent] — task sliced from validated plan; runs after 01 (backend contract ready).
- 2026-09-29 [agent] — picked up by web-coder (order 2): /attention page + unread-only bell, backend contract from task 01 already in tree
- 2026-09-29 [agent] — full-body upsert to add obstacles (empty section not patchable, same quirk as task 01); AC ticks and log preserved
- 2026-09-29 [agent] — added obstacles section via full-body upsert (empty obstacles not patchable); body, AC ticks and log preserved
- 2026-09-29 [agent] — implemented: /attention route (RSC shell + awaiting/ceremonies/history sections), awaiting via ?awaiting=true&take=200 with client isAwaitingNotification guard, ceremonies via new features/attention (groupCeremoniesByProject + own polling key), bell cut to unread preview + Open Attention, Attention nav item with unread badge in sidebar and mobile nav, sr-only Archived on hidden rows; shared NotificationActions + FeatureSlugLabel + NOTIFICATION_KIND_META + notificationHref pulled into features/notifications barrel; pins: awaiting-notifications, bell-preview, ceremony-groups (48/48 green, +8 new)
- 2026-09-29 [agent] — web task ready for review: /attention page + unread-only bell + sr-only Archived, 5/5 AC ticked, gates green (lint 0 errors, tsc clean, 48/48 tests, build ok with /attention route); interpretation notes in obstacles (notification-center stays in app-shell for 084 bubble, ceremonies empty-state = collapse, awaiting update latency ~1 RTT via family invalidation)
- 2026-09-29 [agent] — light review PASS-WITH-NITS (audits/review-087-02.md): 0 BLOCKER/IMPORTANT/SUGGESTION, 4 NIT → микробатч #146 (BellPreviewRow split, focus-visible на Open-ссылках, мёртвый баррель-экспорт, кэп бейджа 99+ vs 50+); гейты подтверждены ревьюером лично (lint 0e/tsc/48-48/build с /attention) — таск done
</log>