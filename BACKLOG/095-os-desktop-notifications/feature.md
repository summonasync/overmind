---
feature: 095
slug: os-desktop-notifications
status: inProgress
priority: p1
version: 1
created: 2026-09-28
---

# Feature 095 — Attention surfaces: OS desktop toasts + web sound + browser tab attention для approval/question

Attention surfaces для approval/question нотификаций агентов — три уровня пробивания к юзеру за пределами видимой страницы, одна фича (скоуп расширен 2026-10-03 по юзер-просьбе: web-звук + вкладка браузера присоединены к OS-тостам).

<requirements>
1. OS-level desktop notifications (linux desktop): системный тост через notify-send/dbus с абсолютным URL (host port/URL resolved), клик → открывает Overmind на нужной поверхности (project → feature → tab). Кандидат-механика (на спеке): host-side интеграция в tray-host, dedup/debounce против агентских флудов, настройка on/off в проекте/глобально.
2. Web: звуковой пинг при новой approval/question нотификации — короткий, без новых пакетов; mute-тоггл в UI, состояние в localStorage.
3. Web: вкладка браузера требует внимания, пока есть непосещённые approval/question — мигающий document.title со счётчиком висящих + favicon-бейдж (canvas-точка); снимается при прочтении/действии/фокусе вкладки.
</requirements>

Границы (обновлены 2026-10-03): событийный тост ВНУТРИ видимой страницы — 093-approval-deep-link-toast; персистентная видимость висящих аппрувов внутри веба (колокольчик/attention-страница) — 084. Эта фича — внимание ЗА пределами видимой страницы: ОС, звук, вкладка браузера.

<refs>
- 084-notification-human-actions (развод поверхностей зафиксирован 2026-09-28, границы обновлены 2026-10-03).
- 020-agent-notifications (источник событий).
- 093-approval-deep-link-toast (внутристраничный тост — не сюда).
</refs>

<log>
- 2026-10-03 [agent] — user request in-session 2026-10-03: scope extended from OS-only to all attention surfaces — web sound ping (mute toggle, localStorage) + browser tab attention (flashing title with pending count + favicon badge, cleared on read/action/focus) joined the OS toast work; boundaries vs 093 (in-page toast) and 084 (in-web persistent visibility) restated.
- 2026-10-04 [auto] — artifact created → inProgress
</log>
