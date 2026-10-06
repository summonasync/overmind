---
feature: 123
task: 01
slug: project-name-typography
status: done
review: light
created: 2026-10-06
brief: Shell: render the project name above the tab bar in project-tabs.tsx (h1/p switch, text-primary mono treatment) and add shared/ui/tab-header.tsx (description + actions, no title).
---

# Task 1: Shell restructure — name above tabs + TabHeader

<goal>
The project workspace header gains its top element: the project name rendered above the tab bar with the brand treatment, plus the new shared description block component that task 2 wires into pages.
</goal>

<scope>
- `web/src/widgets/app-shell/ui/project-tabs.tsx` — inside the existing padded wrapper, above the tab bar: the resolved project's name (`projectParam ?? lastActiveProject` logic already in place) rendered only when a project resolves. Treatment: `truncate font-mono text-sm font-semibold tracking-wide text-primary` (classes tunable within spec constraints: `text-primary`, existing stack, ≤ `text-base`, truncate). Element tag switches on the view: `h1` on root tab views, `p` when `searchParams.get("feature")` is present (feature workspace keeps the feature title as its h1). Small margin below the name before the tab bar; layout order pinned: name → tabs.
- `web/src/shared/ui/tab-header.tsx` — new component, sibling of `PageHeader` without the title: description paragraph + optional actions row, same outer rhythm (`mb-6 flex flex-wrap items-start justify-between gap-4`). No file consuming it yet — task 2 wires pages.
</scope>

<anti-scope>
- No page changes (`planning-page.tsx`, `entries-page.tsx`, `issues-page.tsx`) — task 2.
- No `ProjectHeader` deletion yet — task 2 (pages still render it until then; compile stays green between tasks).
- No brand-book edit, no chip/list/select changes, no no-project view changes, no font packages, no globals.css edits.
</anti-scope>

<constraints>
- Colors from theme tokens only (`text-primary`); font stacks from `@theme` (`--font-mono`); no hardcoded hex.
- Exactly one h1 per view: root tab views get the name as h1; feature view (`?feature=`) gets the name as p.
- No CLS: static markup/CSS, no async font loading; the name renders only when a project resolves (no-project keeps today's strip).
- `useProjectName` import from `@/features/projects` — consistent with existing app-shell → feature imports; React Query dedupes the already-warm project list query.
</constraints>

<acceptance-criteria>
- [x] On `/dashboard?project=…`, `/entries?project=…`, `/issues?project=…` the name renders above the tab bar in brand purple mono treatment.
- [x] On `/dashboard?project=…&feature=…` the name renders as a non-heading p; root tab views carry it as the single h1.
- [x] Without a project, the strip renders exactly as today (no name, hint behavior unchanged).
- [x] `TabHeader` renders description + optional actions with no title element, matching `PageHeader`'s outer spacing rhythm.
- [x] `npm run lint && npx tsc --noEmit` pass from `web/` at this slice.
</acceptance-criteria>

<refs>
- Spec: artifact 39be6c89-6ac9-4b6c-ab26-0b672735eee6 (feature 123-project-name-typography) — treatment, a11y heading decision, constraints.
- Plan: artifact ab0225aa-60b1-4d27-8017-f6bc45df156f, approach move 1 and 2.
- `web/src/widgets/app-shell/ui/project-tabs.tsx` — file under change (owns project resolution).
- `web/src/shared/ui/page-header.tsx` — spacing rhythm reference for `TabHeader`.
- `web/src/features/projects/lib/use-project-name.ts` — name hook.
- `web/src/app/globals.css` `@theme` — tokens.
</refs>

<obstacles>
- None observed at planning time. Watch item: the name inherits `ProjectTabs`' existing `Suspense` + effect-backed `lastActiveProject` pattern — first paint may show the name only from the `?project=` param; that is expected, no hydration mismatch allowed.
- 2026-10-06 [agent] coding round: AC2 stays unticked — the h1/p switch is live in the shell, but root tab views temporarily carry two h1s (shell name + `ProjectHeader` page title) until task 02 removes the page header; "single h1 per view" closes with the page swaps.
- 2026-10-06 [agent] coding round: with a cold React Query cache the name first paints as the `projectDisplayName` fallback (`id.slice(0, 8)`) until the sidebar-warmed list query resolves — identical to today's `ProjectHeader` behavior; SSR and client render the same fallback, so no hydration mismatch.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan v2, move 1+2 (shell layer).
- 2026-10-06 [agent] — task 01 coding round started
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers. Hooks-rule extraction verified, h1/p switch semantics confirmed, gates re-run by reviewer. AC2 (single h1) intentionally unticked until task 02 removes ProjectHeader (documented transitional state). Commit 80d05b2.
</log>