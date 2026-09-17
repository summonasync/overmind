---
feature: 008
task: 05
slug: dogfood-hardening
status: done
review: light
created: 2026-09-16
---

# 008/05 — Web: sidebar separation + colored ceremony sections

<goal>Сайдбар визуально отделён от фона (тень/сепаратор); markdown-превью раскрашивает церемониальные XML-секции: имя тега и содержимое в цвете секции (goal — зелёный, requirements — жёлтый, non-goals — алый, остальные по токенам brand-book), HTML-комментарии скрыты.</goal>

<context>
Фича 008-dogfood-hardening (dogfood 0.1.0 findings). Субагент читает только этот файл. Спека validated: решения IMP-1..IMP-5, UI-3/4, PERF-1 зафиксированы в ней; ревьюер читает @../spec.md.
</context>

<scope>- web/src/widgets/app-shell (сепарация сайдбара, токены brand-book)
- web/src/shared/lib/markdown (или компонент Markdown) — раскраска секций в ОДНОМ месте: превью используется в entries, artifact-editor, task-editor
- web/brand-book.md при необходимости дополнить токенами секций</scope>

<anti-scope>- Новые npm-зависимости запрещены
- Редизайн навигации (табы проекта) — НЕ эта фича
- Хардкод hex в разметке</anti-scope>

<constraints>
- .NET 10 / C# 14 (таски 01-04, 06), Next.js 16 + Tailwind 4 (таска 05)
- Новые пакеты — только через nuget-licensing-guard / явное одобрение пользователя
</constraints>

<acceptance-criteria>
- [ ] Сайдбар визуально отделён на всех страницах
- [ ] Превью тела фичи/спеки/таски показывает секции в цвете, теги открытия/закрытия одним цветом
- [ ] HTML-комментарии в превью не видны
- [ ] npm run lint && npx tsc --noEmit && npm run build чистые
</acceptance-criteria>

<refs>
- @../feature.md — what/why фичи
- @../spec.md — decisions IMP/UI/PERF (ревьюер)
- @../plan.md — подход и риски (ревьюер)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-17 — 008/05 started: next-js-coder subagent on sidebar separation + colored ceremony sections
- 2026-09-17 — 2026-09-17 — next-js-coder — sidebar separated (bg-surface + border + shadow-2xl); ceremony sections color-coded once in shared markdown (18 sections, --color-ceremony-* tokens in globals.css, brand-book updated); comments stripped; lint/tsc/build clean
- 2026-09-17 — 2026-09-17 — light review (next-js-code-reviewer) — PASS-WITH-NITS; warnings to hardening: comment strip is fence-unaware (mangles <!-- --> inside code fences), section-open heuristic colors the doc tail on unmatched opening tag (fine for machine-generated ceremony bodies)
- 2026-09-17 — AC met: sidebar separated on all pages, sections colored per user palette via tokens, comments hidden, single shared implementation; lint 0 errors, tsc clean, static build ok; review PASS-WITH-NITS
</log>