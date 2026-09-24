---
feature: 030
slug: feature-dependencies
status: done
priority: p0
version: 1
created: 2026-09-20
---

# Feature 030 — Feature dependencies: structured blockedBy/blocks (не текстом в body)

<goal>
Структурированные зависимости между фичами: blockedBy/blocks как данные (не текст в body) с ENFORCEMENT — фича с незакрытыми зависимостями не берётся в работу раньше их закрытия — и с первоклассным редактированием: отдельный UI-редактор для человека и MCP-тулзы add/remove/list для агента, а не текст-блок в теле.
</goal>

<context>
Mindlash-фидбек (2026-09-20): зависимости жили текстом в body. 2026-09-24 юзер дважды усилил: (1) enforcement — «не можем брать её раньше, чем связанные будут сделаны»; (2) «отдельный UI, а не просто блок в спеке/фиче/плане — удобный UI мне и MCP тебе, чтобы добавлять, убирать и читать».

<removed 2026-09-24 — superseded by user decisions: «предупреждение, не запрет» заменено на enforcement; текстовая форма — на отдельную сущность с редактором>
</context>

<sketch>
- PlanningFeature: коллекция ссылок blockedBy/blocks (slug + resolved id, тот же проект; межпроектные — 028, вне скоупа v1).
- Редактирование как сущность: UI — редактор зависимостей на карточке/воркспейсе (селект фич, добавить/убрать, стрелки/чипы «ждёт N»); MCP — отдельные тулзы add_dependency/remove_dependency/list_dependencies (или параметры create/update feature); НЕ текст-редактирование body.
- Enforcement: planning_current_task не выдаёт таск из фичи с незакрытыми blockedBy (стоп-стейт с перечнем незакрытых; фича читается, выдача — нет); planning_set_current отказывает с диагностикой; override для осознанного обхода — только human door (UI), не MCP.
- Автопромоуты не зависят от blocked (зависимость — про «кто следующий», не про статусы).
- Materializer пишет связи в frontmatter feature.md, importer читает обратно; борда: фильтр «blocked», чипы.
</sketch>

<open-questions>
- Циклы: запрет на create (валидация) — предпочтительно; fallback — допуск+предупреждение.
- current_task: полный стоп или стоп-с-перечнем (read-only видимость)? Рекомендация: выдача стоп, чтение открыто.
- Закрытие зависимости: done-only или review тоже? Рекомендация: done-only.
</open-questions>

<status>
Draft; поднята в очереди юзер-запросом 2026-09-24 (enforcement + отдельный UI/MCP-редактор). Спека после 067-069/071.
</status>

<log>
- 2026-09-24 [ui] — status moved to 'ready' via ui
- 2026-09-24 [auto] — artifact created → inProgress
- 2026-09-24 [auto] — all tasks done + spec + plan → review
- 2026-09-24 [agent] — by user request in-session — post-session validation confirmed: 6/6 tasks + reviews (task 4 hard APPROVED WITH WARNINGS), result artifact complete, full suite re-run green (Core 203, Infra 427, Api 382, Mcp 318), web lint/tsc/build clean
</log>
