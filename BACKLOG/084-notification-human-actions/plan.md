---
feature: 084
slug: notification-human-actions
status: validated
created: 2026-09-28
validated: 2026-09-28
---

# Plan 084 — Notification center: human actions (approval/question) — видимость, прямые ссылки на overmind, actionability

Разрез: 5 тасок, конвейер **01 → (02 ∥ 04) → 03 → 05**. Ревью: light ×4, closeout hard.

## Task 01 — Backend: nudge settings keys (light)
- `SettingsContracts.cs` / `SettingsApi.cs` — два новых ключа в AppSettings (KV, без миграции) по прецеденту `QdrantEndpoint`:
  `notificationNudgeMode` = quiet|nag|mini (default `quiet`), `notificationNudgeIntervalMinutes` = int > 0 (default `10`, meaningful only for nag).
- Валидация значений (400 на мусор), round-trip тесты (дефолты при пустом сторе, PUT/GET, invalid rejected).
- Файлы: src/Overmind.Host/Api/Contracts/SettingsContracts.cs, src/Overmind.Host/Api/SettingsApi.cs, tests/Overmind.Api.Tests/SettingsApiTests.cs.

## Task 02 — Web: center kind filter + bell awaiting-state (light)
- Notification center: kind-фильтр (approval/question/info, all) — посадочная поверхность для бейджа «ещё N»; open approvals link ведёт на центр с предустановленным фильтром approval (query/параметр компонента — без next/link, static-export rule).
- Белл: distinct awaiting-state (визуальное «горит», отдельно от unread counter) при наличии нерешённых approval — данные уже в существующем notifications query.
- Файлы: web/src/widgets/app-shell/ui/notification-center.tsx, notification-bell.tsx (+ types если надо).

## Task 03 — Web: zerling bubble + discipline state machine (light, сердце фичи)
- Новый компонент в app-shell: non-blocking speech bubble в углу с зерлингом (поза menacing или yelling — на выбор кодера по визуалу), контент: старейший нерешённый approval (FIFO) + Approve/Reject (reuse useNotificationMutations + canonical 088-реплаи) + «Open →» на таргет + бейдж «ещё N → open approvals» (на фильтр из 02).
- Дисциплина: режимы quiet/nag/mini из настроек (01): quiet — dismiss до следующего НОВОГО approval (dismiss-факт живёт в памяти вкладки/sessionStorage, переживает навигацию, не переживает закрытие вкладки); nag — re-pop каждые N минут; mini — схлоп в маленькую иконку-зерлинга (sleeping) в углу, клик разворачивает.
- Lifecycle: dead-target dimming (featureSlug отсутствует в кэше planning features query — дим, кнопки живы); TTL 14 дней константа — nag-поверхность игнорит approval старше; центр показывает всё.
- In-place refresh: решение показанного обновляет контент без повторного выезда/анимации.
- Тесты .mts: state machine transitions, FIFO pick, TTL gate, dead-target dim, badge count. lint + tsc + build.
- Файлы: web/src/widgets/app-shell/ui/ (bubble + hook), web/src/features/notifications (переиспользование), web/src/shared/ui/zerling.tsx (только переиспользование).

## Task 04 — Web: settings form controls (light)
- features/settings/components/settings-form.tsx: select quiet/nag/mini + numeric minutes (виден/активен при nag) — паттерн существующих полей формы, мгновенный PUT.
- Файлы: web/src/features/settings/{types,api}.ts + components/settings-form.tsx.

## Task 05 — Closeout: live walk + docs + result (hard)
- Live walk на изолированном хосте (scratch HOME, порт из легенды, kill по PID): notify approval → bubble вылез → approve → read-on-approve → bubble cleared; переключение режимов quiet/nag/mini; бейдж «ещё N» на двух approval; dead-target dim (снести фичу-таргет).
- Доки: CONTEXT.md/STRUCTURE.md — только если появился незадокументированный шов (коротко).
- Result-артефакт, validate, фича к done-гейту.

## Риски
- Bubble поверх любой страницы: дип-линки из bubble ведут через существующий notificationHref-паттерн (plain anchors).
- Settings UI поверхность: только хост-настройки (не проектные) — nudge режим глобальный.
- Тесты времени (TTL, nag-интервал): инжектить clock в хук, не спать в тестах.

<log>
- 2026-09-28 [agent] — validated with human proof: notification 3bebb19d-c8b0-4f5d-80dd-23814301e12d replied 2026-09-28 00:24:18 UTC
</log>
