---
feature: 042
slug: onboard-observability
status: review
priority: p1
version: 1
created: 2026-09-22
---

# Feature 042 — Onboard observability: материализация и режим валидации видны в ответе `overmind_onboard` ДО первого вызова

<goal>
Убрать два «действия вслепую» из первого контакта агента с проектом: (1) материализация нигде не описана — агент узнаёт «куда пишутся файлы, когда и сработала ли» только поиском по диску; (2) режим валидации неизвестен до первого `planning_validate` — а если включён human gate, это не ошибка агента, а гейт, и знать об этом надо ДО построения плана. Оба ответа просятся полями в `overmind_onboard` — точке, где агент всё равно бывает первым делом.
</goal>

<requirements>
- Ответ `overmind_onboard` несёт блок материализации: включена ли для проекта, куда пишет, когда следующий прогон.
- Ответ `overmind_onboard` несёт режим валидации spec/plan (auto-validate vs human gate).
- Формулировки согласованы с фактическим поведением (см. 043 про расхождения документации и наблюдаемого).
</requirements>

<non-goals>
- Сам материализатор и его настройки — уже существуют; 042 только про видимость.
- UI-настройки материализации/валидации.
- Документирование автопереходов и provenance статусов — 043.
</non-goals>

<refs>
- FEATURELIST/06-instruction-gaps.md — пункты 1 и 3: поиск файлов «вслепую» (git status, find -newermt, ~/.overmind), неизвестный режим валидации.
- FEATURELIST/08-migration-field-report.md — «planning_validate succeeded silently, no way to know which mode a project is in except by attempting the call».
- FEATURELIST/07-field-evidence-strengths.md — вывод: материализация — фича, которая схлопнет гибрид «файлы+борда», «ровно поэтому её стоит добить».
- 032-status-model-self-documentation — onboard уже несёт lifecycle; расширяем тот же ответ, тот же паттерн.
</refs>

<log>
- 2026-09-23 [auto] — artifact created → inProgress
- 2026-09-23 [auto] — all tasks done + spec + plan → review
</log>
