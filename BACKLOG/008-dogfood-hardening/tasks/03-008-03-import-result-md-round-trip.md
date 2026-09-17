---
feature: 008
task: 03
slug: dogfood-hardening
status: done
review: light
created: 2026-09-16
---

# 008/03 — Import: result.md round-trip

<goal>result.md импортируется как артефакт kind=result (order 0); round-trip с материализатором замкнут; повторный импорт идемпотентен.</goal>

<context>
Фича 008-dogfood-hardening (dogfood 0.1.0 findings). Субагент читает только этот файл. Спека validated: решения IMP-1..IMP-5, UI-3/4, PERF-1 зафиксированы в ней; ревьюер читает @../spec.md.
</context>

<scope>- src/Overmind.Infrastructure/Planning/BacklogImporter.cs — result.md в обходе папки фичи, маппинг статуса по словарю kind result
- Тесты Overmind.Infrastructure.Tests: импорт result.md, повторный импорт без дублей</scope>

<anti-scope>- Материализатор не трогаем (он уже пишет result.md)
- Схему БД не меняем</anti-scope>

<constraints>
- .NET 10 / C# 14 (таски 01-04, 06), Next.js 16 + Tailwind 4 (таска 05)
- Новые пакеты — только через nuget-licensing-guard / явное одобрение пользователя
</constraints>

<acceptance-criteria>
- [ ] Тест: result.md появляется в БД как kind=result, order 0
- [ ] Тест: повторный импорт не создаёт дубль
- [ ] Тест: result.md больше не попадает в ignored-лист
- [ ] dotnet test зелёный
</acceptance-criteria>

<refs>
- @../feature.md — what/why фичи
- @../spec.md — decisions IMP/UI/PERF (ревьюер)
- @../plan.md — подход и риски (ревьюер)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-17 — 008/03 started: coder subagent on result.md import round-trip
- 2026-09-17 — 2026-09-17 — coder — result.md imports as kind=result order 0, shares the spec/plan document path (TryParseDocumentAsync/MapDocumentStatus renames); idempotent via Kind-match; +4 Infrastructure tests; 535/535 green
- 2026-09-17 — 2026-09-17 — light review — PASS clean; result status vocabulary is {draft,validated} per Core AllowedStatuses, round-trip safe (materializer cannot emit other statuses); observation to hardening: validated-artifact body round-trip is not byte-stable (materializer omits the imported H1) — re-import after materialize reads as a body change
- 2026-09-17 — AC met: result.md imports as kind=result order 0, re-import idempotent, no longer ignored; build 0/0, 535/535; review PASS
</log>