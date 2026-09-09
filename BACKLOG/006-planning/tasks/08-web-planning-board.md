---
feature: 006
task: 08
slug: web-planning-board
status: todo
review: light
created: 2026-09-08
---

# 006/08 — Web: planning slice, feature board, route

<goal>
Planning is reachable in the UI: a sidebar item, a `/planning` route, a feature board for the active project with status/task-progress/validation chips, and a create-feature flow — all on a proper FSD slice with React Query.
</goal>

<context>
`web/` is a Next.js 16 **static export** served by the Host: no server, no Server Actions, no `next/link` (the static-export client router is broken upstream — navigation is plain `<a href>`), route files are `page.page.tsx`, and `useSearchParams` must sit inside `<Suspense>`. Read `STRUCTURE.md` → "Frontend structure" and `web/brand-book.md` before styling.

Templates to copy:
- slice: `web/src/features/entries/{types.ts,api.ts,queries.ts,lib/use-entries.ts,index.ts}` — every `fetch` goes through `shared/api/client.ts`, query keys come from a factory in `queries.ts`, hooks return `{ data, loading, error }`, mutations invalidate by factory key.
- page: `web/src/pages/entries-page/ui/entries-page.tsx` (project-scoped via `?project=`, "select a project first" empty state) and `web/src/pages/kits-page/ui/kits-page.tsx` (info panel via `useInfoPanel`).
- route: `web/src/app/entries/page.page.tsx`.
- sidebar: `web/src/widgets/app-shell/ui/app-sidebar.tsx` (`NAV_ITEMS`, and how the Entries href carries the last active project) + `mobile-nav.tsx`.

Backend is live from tasks 02–03: `GET /api/projects/{id}/planning/features`, `POST` the same, `GET/PUT/DELETE /api/projects/{id}/planning/features/{slug}`, artifact routes under `/api/planning/*`, workflow under `/api/projects/{id}/planning/workflow`.
</context>

<scope>
`web/src/features/planning/types.ts` — DTO types mirroring the contracts exactly (camelCase, enums as string unions): `PlanningFeatureStatus`, `PlanningArtifactKind`, `PlanningArtifactStatus`, `PlanningReviewLevel`, `PlanningMaterialization`, `HumanValidationOverride`, `PlanningFeatureBoardItem`, `PlanningArtifactResponse`, `PlanningFeatureResponse`, `PlanningArtifactVersionResponse`, `PlanningWorkflowResponse`, `PlanningCurrentResponse`, `PlanningDriftItem`, `PlanningMaterializationReport`, plus form-value types.

`web/src/features/planning/api.ts` — thin functions over `apiGet/apiPost/apiPut/apiDelete`: board list, get feature, create feature, update feature, delete feature, list artifact versions, get artifact, create artifact, update artifact, set artifact status, append log, validate artifact, get/put workflow, get drift, materialize.

`web/src/features/planning/queries.ts` — `planningKeys` factory (`all`, `lists()`, `board(projectId)`, `feature(projectId, slug)`, `versions(artifactId)`, `workflow(projectId)`, `drift(projectId)`) + `queryOptions` per read, `enabled` guards on empty ids.

`web/src/features/planning/lib/use-planning.ts` — `usePlanningBoard(projectId)`, `usePlanningFeature(projectId, slug)`, `usePlanningMutations(projectId)` (create/update/delete feature, create/update/delete artifact, setStatus, appendLog, validate) with invalidation of the board + feature keys; `usePlanningWorkflow(projectId)` + `useWorkflowMutations(projectId)`; `usePlanningDrift(projectId)` + `useMaterializeMutation(projectId)`.

`web/src/features/planning/components/feature-board.tsx` — card per feature: slug + title, status chip (`Badge`), task progress `n/m done` with a thin progress bar, spec/plan validated chips, `updatedAt` relative text; click → `?project=<id>&feature=<slug>`; row action to edit/delete (delete behind the house confirm-modal pattern). Empty state: `EmptyState` with a "New feature" call to action.

`web/src/features/planning/components/feature-form.tsx` — slug + title + optional body (markdown `Textarea`), slug hint `nnn-slug`, validation errors surfaced through `ErrorText`.

`web/src/features/planning/index.ts` — barrel exporting the components, hooks and types used by pages.

`web/src/pages/planning-page/ui/planning-page.tsx` — `'use client'`, `Suspense`-wrapped content reading `?project=` and `?feature=`; without a project → the same "Select a project first" block as `entries-page`; with a project and no feature → board + `PageHeader` (title `Planning — <project name>`, action `New feature`, back-link `← All projects`); with `&feature=` → render a placeholder workspace container for now (`feature-workspace.tsx` arrives in task 09 — export a minimal component that shows the feature title, status and its artifacts as read-only cards so the route is never dead).
`web/src/pages/planning-page/ui/planning-info-panel.tsx` — counts (features by status, tasks done/total, validated specs/plans) in the house `InfoPanel` style; wired via `useInfoPanel`.
`web/src/pages/planning-page/index.ts` — barrel.
`web/src/app/planning/page.page.tsx` — route file rendering `<PlanningPage />`.

Navigation: `app-sidebar.tsx` gains a Planning item (a `lucide-react` icon in the existing set's spirit, e.g. `ListChecks`) placed after Entries, carrying the last active project id exactly like the Entries href does; `mobile-nav.tsx` gains the same link.

Verification: `npm run lint && npx tsc --noEmit && npm run build` clean; manual pass against a running Host — board renders real features, create + delete work, nav carries the project.
</scope>

<anti-scope>
- No artifact editors, Approve button, task kanban or log timeline (task 09) — the workspace stays read-only here.
- No workflow-settings UI, no drift panel, no Settings-page globals (task 09).
- No new npm packages, no `next/link`/`useRouter`, no `useEffect` data fetching.
- No changes to other features' internals; do not import across features (pages compose, features don't).
</anti-scope>

<constraints>
- `npm run lint && npx tsc --noEmit && npm run build` clean (the 5 known `no-img-element` warnings stay as they are — do not add new ones).
- TypeScript strict, no `any`. `'use client'` only where hooks/interactivity require it.
- All data access through `shared/api/client.ts`; all query keys through the factory.
- FSD import direction only (`pages → features → shared`); the slice has an `index.ts` barrel and consumers use it.
- Tailwind tokens from `web/brand-book.md` — no raw hex colors.
- No git commits — report the changed-file list.
</constraints>

<acceptance-criteria>
- [ ] `/planning` is reachable from the sidebar and mobile nav, carrying the active project.
- [ ] Board shows status, task progress and spec/plan validation chips from real API data.
- [ ] Create/edit/delete a feature works with optimistic-free invalidation (React Query refetch).
- [ ] `?feature=<slug>` renders a non-dead workspace placeholder.
- [ ] Info panel shows planning metrics; lint/tsc/build are clean.
</acceptance-criteria>

<refs>
- @../spec.md — "UI" (board, feature page, sidebar section), R6
- @../plan.md — Task 08 + Risks (static-export routing)
- @./02-planning-api.md — the HTTP shapes being consumed
- web/src/features/entries/*, web/src/pages/entries-page/*, web/src/widgets/app-shell/ui/app-sidebar.tsx
- STRUCTURE.md — "Frontend structure (web/src, FSD)"
</refs>

<obstacles>
</obstacles>

<log>
2026-09-08 — task created from the validated plan.
</log>
