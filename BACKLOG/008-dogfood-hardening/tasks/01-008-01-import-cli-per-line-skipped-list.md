---
feature: 008
task: 01
slug: dogfood-hardening
status: inprogress
review: light
created: 2026-09-16
---

# 008/01 — Import CLI: per-line skipped list

<goal>При --import-backlog пользователь видит построчно, КАКИЕ файлы skipped/ignored/unknown-status, а не только счётчик.</goal>

<context>
Фича 008-dogfood-hardening (dogfood 0.1.0 findings). Субагент читает только этот файл. Спека validated: решения IMP-1..IMP-5, UI-3/4, PERF-1 зафиксированы в ней; ревьюер читает @../spec.md.
</context>

<scope>- src/Overmind.Host/BacklogImportCommand.cs — вывод report.Skipped построчно (INF) после summary
- Host-тест на формат вывода</scope>

<anti-scope>- Модель BacklogImportReport не меняем
- Логику импортёра не трогаем (это таски 02-04)</anti-scope>

<constraints>
- .NET 10 / C# 14 (таски 01-04, 06), Next.js 16 + Tailwind 4 (таска 05)
- Новые пакеты — только через nuget-licensing-guard / явное одобрение пользователя
</constraints>

<acceptance-criteria>
- [ ] Импорт этого репо печатает все skipped-записи построчно (префиксы skipped/ignored/unknown сохранены)
- [ ] dotnet build + dotnet test зелёные
</acceptance-criteria>

<refs>
- @../feature.md — what/why фичи
- @../spec.md — decisions IMP/UI/PERF (ревьюер)
- @../plan.md — подход и риски (ревьюер)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-16 — 008/01 started: coder subagent on BacklogImportCommand skipped-list output
</log>