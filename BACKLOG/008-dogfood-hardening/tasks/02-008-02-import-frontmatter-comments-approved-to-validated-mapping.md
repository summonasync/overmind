---
feature: 008
task: 02
slug: dogfood-hardening
status: done
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
- 2026-09-16 — 008/02 started: coder subagent on importer frontmatter comment strip + approved→validated mapping
- 2026-09-17 — 2026-09-17 — coder — Token() strips inline comments at first ' #' before the whitespace cut; MapSpecOrPlanStatus: approved -> Validated; +3 Infrastructure tests (strip, default-draft, hash-prefix); 531/531 green
- 2026-09-17 — 2026-09-17 — light review — PASS-WITH-NITS; WARNING (pre-existing, recorded to BACKLOG hardening): PlanningArtifact.Restore throws on Validated+FrozenAt=null, so a spec/plan with approved/validated but no created/validated fields fails the whole feature import
- 2026-09-17 — AC met: comment strip + approved->validated pinned by tests incl. edge cases; build 0/0, 531/531; review PASS-WITH-NITS, no blockers
</log>