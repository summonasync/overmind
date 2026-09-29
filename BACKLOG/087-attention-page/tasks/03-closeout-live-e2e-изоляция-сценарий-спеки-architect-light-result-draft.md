---
feature: 087
task: 03
slug: attention-page
status: done
review: light
created: 2026-09-29
depends_on: 02
brief: Closeout: live e2e (изоляция, сценарий спеки) + architect light + result draft
---

<goal>Closeout 087: live e2e по верификации спеки, architect light review, result-артефакт.</goal>
<scope>
- Live e2e на изолированном хосте (scratch HOME, свой порт, kill по PID; прод не трогать — легенда): question → появляется в awaiting на /attention → reply со страницы → строка уходит из awaiting (084-инвариант); ceremonies-секция показывает фикстурную фичу с причиной; deep-link открывает фичу; bell показывает unread-only; take=200/awaiting живьём.
- Архитектор light review всей фичи (audit-файл в audits/ по конвенции).
- Result-артефакт: outcome, верификация (dotnet + web + live e2e), deviations, follow-ups в микробатч.
</scope>
<anti-scope>
- Новый продуктовый код — только если ревью/e2e поймали дефект.
- Деплой/коммит — вне таски (юзер-ask).
</anti-scope>
<constraints>
- Изоляция live-хоста обязательна (host.port-инцидент).
- Незакрытые WARNING архитектора — либо фикс-раунд, либо задокументировать в result с обоснованием.
</constraints>
<acceptance-criteria>
- [x] Live e2e пройден по сценарию спеки, протокол в result
- [x] Architect light verdict в audits/, 0 BLOCKER
- [x] Result-артефакт написан (draft), фича в review
</acceptance-criteria>
<refs>
- spec 02d54b36 (validated) — раздел Verification.
- 096/097 closeout-прецеденты (изоляция, протокол e2e).
</refs>
<obstacles></obstacles>
<log>
- 2026-09-29 [agent] — task sliced from validated plan; runs after 02.
- 2026-09-29 [agent] — orchestrator takes closeout: live e2e (isolated host) + architect light + result draft
- 2026-09-29 [agent] — [orchestrator] live e2e зелёный на изолированном хосте (HOME=/tmp/overmind-087-e2e, порт 4897, kill по PID 679135; прод 4761 цел): (1) проект+question агент-дверью → url-штамп 093 живой; (2) GET ?awaiting=true&take=200 → вопрос в awaiting; (3) take=0 → 400; (4) POST reply → строка УШЛА из awaiting (084-инвариант серверно); (5) фича 001 + spec draft → ceremonies: ['spec awaiting validation'], number 001, диплинк ?project=&feature=; (6) done-фича исключена из ceremonies. UI-слой (bell unread-only, awaiting-инпуты, ceremonies-секция) — юзер клик-чек при выкладке. Архитектор light запущен.
- 2026-09-29 [agent] — closeout done: live e2e 6/6 (изоляция 4897, прод цел), architect APPROVED-WITH-WARNINGS (audits/architect-review-2026-09-29-087.md), WARNING-1/2 + SUGGESTION-3 закрыты фикс-раундом оркестратора (гейты: 1657/1657, web 48×48), result 117b87c2 draft — AC 3/3
</log>