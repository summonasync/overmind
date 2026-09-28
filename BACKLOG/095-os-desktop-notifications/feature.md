---
feature: 095
slug: os-desktop-notifications
status: draft
priority: p1
version: 1
created: 2026-09-28
---

# Feature 095 — OS desktop notifications: системные тосты для approval/question агентов (host-side, linux) с диплинком в Overmind

OS-level desktop notifications для нотификаций агентов (linux desktop): approval/question от агентов должны пробиваться за пределы браузера — системный тост через notify-send/dbus с абсолютным URL (host port/URL resolved), клик → открывает Overmind на нужной поверхности (project → feature → tab).

Кандидат-механика (на спеке): host-side интеграция в tray-host, dedup/debounce против агентских флудов, настройка on/off в проекте/глобально.

Границы: событийный тост ВНУТРИ открытого Overmind — это 093-approval-deep-link-toast; персистентная видимость висящих аппрувов внутри веба — 084. Эта фича — только про ОС-уровень.

<refs>
- 084-notification-human-actions (развод поверхностей зафиксирован 2026-09-28: OS → сюда).
- 020-agent-notifications (источник событий).
</refs>