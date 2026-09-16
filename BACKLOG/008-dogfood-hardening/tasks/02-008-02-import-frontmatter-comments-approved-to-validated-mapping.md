---
feature: 008
task: 02
slug: dogfood-hardening
status: todo
review: light
created: 2026-09-16
---

# 008/02 — Import: frontmatter comments + approved-to-validated mapping

<goal>Frontmatter status со значением «approved» (включая вариант с инлайн-комментарием) импортируется как validated для spec/plan; значения без комментариев не меняются.</goal>

<context>
Фича 008-dogfood-hardening (dogfood 0.1.0 findings). Субагент читает только этот файл. Спека validated: решения IMP-1..IMP-5, UI-3/4, PERF-1 зафиксированы в ней; ревьюер читает @../spec.md.
</context>

<scope>- src/Overmind.Infrastructure/Planning/BacklogImporter.cs — Token(): срез инлайн-комментария (пробел+#) и trim
- MapSpecOrPlanStatus: approved → Validated
- Тесты Overmind.Infrastructure.Tests: срез комментария, маппинг, неизменность прочих значений</scope>

<anti-scope>- MapFeatureStatus и MapTaskStatus не трогаем
- Поведение validated-артефактов при ре-импорте не меняем</anti-scope>

<constraints>
- .NET 10 / C# 14 (таски 01-04, 06), Next.js 16 + Tailwind 4 (таска 05)
- Новые пакеты — только через nuget-licensing-guard / явное одобрение пользователя
</constraints>

<acceptance-criteria>
- [ ] Тест: «approved        # draft | approved» → Validated
- [ ] Тест: «draft» и пустое значение работают как раньше
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
</log>