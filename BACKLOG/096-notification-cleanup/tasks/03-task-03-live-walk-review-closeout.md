---
feature: 096
task: 03
slug: notification-cleanup
status: done
review: hard
created: 2026-09-28
depends_on: 01, 02
brief: Live e2e на dev-хосте + architect-review (hard) + result + фича в review
---

# Task 03 — Live walk + review + closeout

<goal>
Доказать фичу вживую и закрыть её по церемонии: e2e-свидетельства, architect-review, result.
</goal>

<scope>
- Live e2e на изолированном dev-хосте (паттерн 084: scratch cwd DB, OVERMIND__BindUrl, cleanup по exact PID — никогда pkill -f): agent-door notify → human DELETE → notify×2 → read-all (badge 0) → notify → clear-read (центр пуст) → replied-инвариант чек (reply → clear-read сносит). Свидетельства в result.
- dotnet test полный + web lint/tsc/build полные.
- audits/architect-review-YYYY-MM-DD-096.md (light-режим: границы слайса, dependency direction).
- Result-артефакт по <outcome>+<evidence>, validate с human-пруфом, фича → review через человеческую дверь.
</scope>

<anti-scope>
- Новые фичи/измения контракта по ходу — только фикс найденных блокеров в рамках спеки.
</anti-scope>

<constraints>
- Прод-хост (4761) не трогать. Коммит волны — только по явному слову юзера.
</constraints>

<acceptance-criteria>
- [x] Live e2e сценарий зелёный, свидетельства в result.
- [x] Полные тест-сьюты зелёные (backend + web checks).
- [x] architect-review записан в audits/, блокеры закрыты или зафисены.
- [x] Result валидирован, фича в review, юзер уведомлён.
</acceptance-criteria>

<refs>
- BACKLOG/084-notification-human-actions/result.md — паттерн live-walk-свидетельств.
- plan a41a4350 — pipeline/risks.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-28 [agent] — closeout task cut from validated plan a41a4350; blockedBy tasks 01+02.
- 2026-09-28 [agent] — closeout started: live walk on isolated dev host + architect-review (light structural) + result.
- 2026-09-28 [agent] — closeout complete: live walk green (7-step evidence in result f2d64dd6), architect light APPROVED-WITH-WARNINGS resolved via spec v2 + fix-round (audits/architect-review-2026-09-28-096.md), result validated, full Api 492/492 + web 27/27, all AC ticked.
</log>