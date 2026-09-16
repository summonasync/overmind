---
feature: 008
task: 01
slug: dogfood-hardening
status: done
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
- 2026-09-16 — 2026-09-17 — coder — BacklogImportCommand prints report.Skipped per line after summary (+5); IVT seam for tests; 2 Host tests pin order/level/prefixes; obstacle: Serilog static Log.Logger races with WebApplicationFactory tests — serialized via DisableParallelization collection
- 2026-09-16 — 2026-09-17 — light review (dotnet-code-reviewer) — PASS-WITH-NITS, 1 SUGGESTION: transitive Serilog reference in Api.Tests should get an explicit pin (recorded to BACKLOG hardening, not fixed mid-task); audits/dotnet-code-reviewer-2026-09-17-008-01-import-cli-per-line-skipped-list.md
- 2026-09-16 — AC met: per-line skipped output verified live on this repo's BACKLOG (01b + _TEMPLATE lines visible); build 0/0, tests 528/528; review PASS-WITH-NITS, no blockers
</log>