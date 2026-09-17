---
feature: 008
task: 06
slug: dogfood-hardening
status: done
review: light
created: 2026-09-16
---

# 008/06 — Entries API: split query (PERF-1)

<goal>MultipleCollectionIncludeWarning не появляется в логе хоста при GET /api/projects/{id}/entries.</goal>

<context>
Фича 008-dogfood-hardening (dogfood 0.1.0 findings). Субагент читает только этот файл. Спека validated: решения IMP-1..IMP-5, UI-3/4, PERF-1 зафиксированы в ней; ревьюер читает @../spec.md.
</context>

<scope>- src/Overmind.Host/Api/EntriesApi.cs:56 (ListByProject) — AsSplitQuery на запросе с Tags + SkillFiles
- Api-тесты остаются зелёными</scope>

<anti-scope>- Изменение формы ответа EntriesApi
- Другие запросы с Include не трогаем (только задокументированный ворнинг)</anti-scope>

<constraints>
- .NET 10 / C# 14 (таски 01-04, 06), Next.js 16 + Tailwind 4 (таска 05)
- Новые пакеты — только через nuget-licensing-guard / явное одобрение пользователя
</constraints>

<acceptance-criteria>
- [ ] Живой прогон: ворнинг отсутствует в логе после GET /entries
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
- 2026-09-17 — 008/06 executed inline by orchestrator (one-line EF change)
- 2026-09-17 — 2026-09-17 — orchestrator (inline) — AsSplitQuery on both ListByProject multi-collection queries (plain + search branch); build 0/0, 538/538; live check: fresh host on temp HOME, GET /entries and /entries?q= both 200, MultipleCollectionIncludeWarning count in log = 0
- 2026-09-17 — AC met: warning absent from live host log on both /entries paths; tests green. One-liner change, self-verified live per AC; no review spawn (Simple task)
</log>