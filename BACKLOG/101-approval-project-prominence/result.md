---
feature: 101
slug: approval-project-prominence
status: validated
created: 2026-10-03
validated: 2026-10-03
---

# Result 101 — Project prominence: LARGE project name on approval surfaces + always-visible header on feature page/dashboard/entries

## Outcome per task

- **01 — Shared primitive.** `shared/lib/project-display.ts` (pure resolver + `PROJECT_NAME_FALLBACK`) + `features/projects`: `ProjectNameBadge` (compact, Badge tone=info), `ProjectHeader` (title-position h1 + optional secondary line), `useProjectName` hook — all consuming the existing `projectListQueryOptions` cache, zero new fetches. Fallbacks pinned by 8 node tests (known → name, unknown → truncated id, blank id → generic label, blank stored name → truncated id, never-empty, trim — the trim test caught a real resolver defect pre-review).
- **02 — Approval surfaces.** `ProjectNameBadge` in the primary position on all three families: notification-center rows (badge primary, agent demoted to muted secondary beside the deep link), topbar bell preview rows (badge first), attention awaiting cards (badge primary, agent muted), global approval toast (badge as headline, agent muted beneath). Unread borders, mark-read, delete, reply actions, aria labels — untouched.
- **03 — Content headers.** `ProjectHeader` on top of feature page (`?feature=` view), dashboard view, entries view; the `— {project name}` suffix removed from the affected `PageHeader` titles (project now lives in the large header above, no duplication).
- **04 — Closeout (this artifact).** Consistency pass + result + architect handoff. No live dogfood — click-check batched post-release per the batch strategy.

## Decisions & deviations

1. **Placement adjudication (task 01).** Components + hook live in `features/projects` (public API), pure resolver in `shared/lib`. Task body said "shared/ui or shared/lib"; spec allowed "FSD shared or widgets layer" — shared/ui importing the projects query cache would be a hard FSD direction violation and duplicating the query breaks the no-duplicate-fetch AC. Consumers import from `@/features/projects` like they already did for `useProjectList`.
2. **Bang suffix with removal condition.** House `cn()` is a plain join (no tailwind-merge) and shared `Badge` bakes `text-xs` — a plain `text-sm` override is a silent no-op (verified: `.text-sm` emitted before `.text-xs` in the built CSS). The three large-variant consumers use `text-sm!` (verified `!important` in the built stylesheet). **Removal condition:** exactly 3 consumers today; a FOURTH triggers promoting a `size` prop onto shared `Badge` and deleting the triplet.
3. **Legacy "Unknown project" visibility.** Notifications with absent/null `projectId` (legacy rows) render the generic-label fallback badge rather than hiding it — spec: never crash, never silently empty.
4. **PageHeader suffix removal (task 03).** Dashboard/entries titles dropped the `— {project}` suffix; `Issues Board` header keeps its suffix by explicit spec exclusion ("Issues Board detail is NOT in scope — noted for consistency follow-up if wanted").
5. **deadTarget dimming verdict (consistency pass).** Toast at `deadTarget`: badge stays bright while the agent line + text dim. Agreed with current state — the dimming follows the semantics (the agent session died, the project did not), and the warning chip sits beside the bright headline; no one-liner applied.

## Verification

- Review cycles: 01 PASS-WITH-NITS (+ micro round: `"use client"` pair, `normal-case`, padded-id pins), 02 PASS-WITH-NITS, 03 PASS-WITH-NITS. Architect light: dispatched separately by the orchestrator after this artifact.
- Consistency sweep on the final tree: exactly 3 `text-sm!` consumers; zero ad-hoc project-name renders inside the spec's six surfaces (remaining inline `project.name` renders are outside scope: kit workspace sections, search rows, projects catalog, Issues Board header — microbatch candidates).
- Gates on the final tree: lint **0 errors** / 6 pre-existing warnings, `tsc --noEmit` **0**, tests **62 pass / 0 fail** (incl. notification-href regressions from 028 and 10 new project-prominence tests), production build success with unchanged route table.

## Post-release click-check (no live dogfood — batch after release)

1. **Bell preview**: notification originating from Mindlash Studio / BobHouse → project badge first with the real name (not mangled casing), then kind icon + text.
2. **Notification center row**: project badge primary in title position; agent + feature slug as muted secondary; unread border + Mark read behave as before.
3. **Attention card**: same hierarchy on an awaiting approval/question.
4. **Global toast**: fire an approval → project name is the headline; agent muted beneath; dismiss/approve/reject untouched.
5. **deadTarget toast**: approval whose agent session ended → badge bright, "target gone" chip, agent + text dimmed.
6. **Feature page of a NON-active project** (`?project=<other>&feature=<slug>`): project name large at top, above the workspace, no stale "— Project" duplication.
7. **Dashboard / Entries**: persistent project header; titles read just "Dashboard" / "Entries".
8. **Fallback path**: open a surface with an unknown/deleted projectId (e.g. stale link after project deletion) → truncated id or "Unknown project", never a crash or empty gap.

<log>
- 2026-10-04 [agent] — drafted from the closeout consistency pass; outcome per task, five decisions/deviations, gates evidence, eight-point post-release click-check. Draft — user validates after the architect pass.
- 2026-10-03 [agent] — 2026-10-04 [agent] — architect N3 caveat recorded for validation: the per-surface hierarchy was verified by STATIC review (JSX placement audited by the reviewer) — the repo has no DOM/RTL runner, so no component-level render tests exist; the post-release click-check (8 points) is the runtime proof.
</log>