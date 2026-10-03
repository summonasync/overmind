---
feature: 028
slug: cross-project-bug-intake
status: done
priority: p1
version: 1
created: 2026-09-20
---

# Feature 028 — Cross-project bug intake: чужой код → чужая борда (bug-сущность + onboarding правило)

<goal>
Протокол «чужой проект»: агент, работающий в проекте A (Mindlash Studio), которому нужен фикс/изменение в проекте B (Overmind), не лезет в чужой код — заходит в чужую борду и заводит bug-запись, которую команда B разбирает и конвертирует в работу. Onboarding MCP явно этому учит.
</goal>

<context>
Mindlash Studio догфудит Overmind и как доску, и как зависимость. Живой кейс: агент Mindlash'а натыкается на проблему Overmind — сегодня явного пути сообщить нет: правило нигде не задокументировано, сущности под чужой баг нет (note/decision/gate/guard без статуса и очереди; task-артефакт требует фичу на чужой борде). Пользовательская формулировка 2026-09-20: «хочешь залезть в чужой код — залезь в чужую борду и создай note/error, возможно нужна новая сущность bug».
</context>

<sketch>
- Onboarding quickstart (overmind_onboard) + глобальный CLAUDE.md-хинт: «нужен чужой код → чужая борда: list_projects → bug-запись на их проекте; их репо не трогать».
- Сущность bug: author=проект-источник, статус open→triaged→closed, видна отдельной очередью в вебе; промоушн в task-артефакт на подходящей фиче (правило 10: баг = готовый контракт) вместо ручного пересказа.
- Уведомление команде-владельцу через overmind_notify (фича 020) с deep-link на bug.
- Открытые вопросы (закрыть на brainstorm): отдельная сущность vs типизированный note; пермишены чужих агентов (write-only в bugs?); lifecycle bug→task; видимость/фильтры в вебе.
</sketch>

<status>
Draft до спеки; спека после brainstorm с юзером (паттерн 007). Очередь: после 027-догфуда. Скоординировать с 020 (notifications) при проектировании.
</status>

<log>
- 2026-10-02 [auto] — artifact created → inProgress
- 2026-10-03 [auto] — all tasks done + spec + plan validated → review
- 2026-10-03 [agent] — done by user request in-session: result c909e954 validated (notification proof da5f1f33 «approved» 2026-10-03T21:04), chat mandate «Флип, коммит, пуш»; 5/5 tasks done with full review cycles (4 blockers caught and closed), architect APPROVED-WITH-WARNINGS (W1 promote-lock + W2 404s closed), final tree 1857/1857 + web 52/52
</log>
