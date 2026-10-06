---
feature: 123
slug: project-name-typography
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Plan 123 — Project name typography: distinct display treatment vs page headings

<goal>
Move the project name above the tab bar in the shell, replace the page title row with a per-tab description block, delete `ProjectHeader` — keeping the brand typography from v1 as the name's treatment.
</goal>

<approach>
Three moves, one component deleted.

**1. Name above tabs — `web/src/widgets/app-shell/ui/project-tabs.tsx`.** Inside the existing padded wrapper, above the tab bar, render the resolved project's name with the brand treatment. Project resolution already lives here (`projectParam ?? lastActiveProject`, plus the `NO_PROJECT_HINT` state) — the name renders only when a project resolves. The element is `h1` on root tab views and `p` when `?feature=` is present (feature workspace view keeps the feature title as its h1):

```tsx
const isFeatureView = Boolean(searchParams?.get("feature"));
const Tag = isFeatureView ? "p" : "h1";
```

```tsx
<Tag className="truncate font-mono text-sm font-semibold tracking-wide text-primary">{name}</Tag>
```

Exact classes tunable within spec constraints (`text-primary`, existing stack, ≤ `text-base`, truncate). `useProjectName` is imported from `@/features/projects` — a shell widget consuming a feature hook is consistent with existing app-shell imports (`info-panel-context` pattern).

**2. Description block — new `web/src/shared/ui/tab-header.tsx`.** Sibling of `PageHeader` without the title: description paragraph + optional actions row, same outer rhythm (`mb-6`, flex wrap). Roughly:

```tsx
export function TabHeader({ description, actions }: TabHeaderProps) {
  return (
    <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
      <p className="text-sm text-muted">{description}</p>
      {actions ? <div className="flex items-center gap-2">{actions}</div> : null}
    </div>
  );
}
```

**3. Page swaps.** On project views only:
- `planning-page.tsx`: drop `ProjectHeader` (both branches); dashboard view `PageHeader` → `TabHeader` (description = Dashboard copy string, actions unchanged); feature branch keeps `FeatureWorkspace` as-is; no-project branch keeps `PageHeader`.
- `entries-page.tsx`: drop `ProjectHeader`; project-view `PageHeader` → `TabHeader` (Entries copy, actions unchanged); no-project branch untouched.
- `issues-page.tsx`: project-view `PageHeader` → `TabHeader` (Issues copy, actions unchanged); no-project branch keeps `PageHeader` + `GlobalIssueList`.
- `features/projects/index.ts`: remove the `ProjectHeader` export; delete `components/project-header.tsx`.
</approach>

<files>
1. `web/src/widgets/app-shell/ui/project-tabs.tsx` — name above tab bar (h1/p switch, brand treatment).
2. `web/src/shared/ui/tab-header.tsx` — new description+actions block.
3. `web/src/pages/planning-page/ui/planning-page.tsx` — ProjectHeader out, TabHeader in on the dashboard view.
4. `web/src/pages/entries-page/ui/entries-page.tsx` — same.
5. `web/src/pages/issues-page/ui/issues-page.tsx` — same on the project view.
6. `web/src/features/projects/components/project-header.tsx` — deleted.
7. `web/src/features/projects/index.ts` — export removed.
8. `web/brand-book.md` — §5/§6 line: project workspace header pattern (name above tabs, per-tab description, tab title omitted).

Copy strings live as constants beside their consumers (or in `tab-header.tsx`'s consumers) — exact wording is pinned by the spec, not re-derived in code review.
</files>

<css-approach>
Tailwind utility classes on elements; tokens from `@theme` in `web/src/app/globals.css`. No `@layer base`, no globals.css edits, no new abstractions beyond `TabHeader`. The vertical rhythm comes from existing spacing (`mb-6` pattern of `PageHeader`); the name block adds only a small margin below itself before the tab bar.
</css-approach>

<verification>
From `web/`, in order:
1. `npm run lint && npx tsc --noEmit` (catches dangling `ProjectHeader` imports).
2. `npm run test` (node --test; `project-display.test.mts` and `sidebar-projects.test.mts` must stay green — neither pins header markup).
3. `npm run build` (static export).

Manual pass:
- `/dashboard?project=…` — order: name (purple, mono) → tabs → description + «← All projects»/«New feature»; exactly one h1 (the name).
- `/entries?project=…`, `/issues?project=…` — same order, their copy strings, one h1.
- `/dashboard?project=…&feature=…` — name visible (p), tabs, feature title is the h1; no Dashboard description block.
- No-project `/dashboard`, `/entries`, `/issues` — unchanged legacy layout.
- DevTools: no font requests, no hydration warnings; notifications/chips unchanged.
</verification>

<pin-strategy>
No string-pinned tests exist for any header or tab markup (verified: no test references `ProjectHeader`, `ProjectTabs`, `text-2xl`, `page-header`) and none are added — all changes are presentation; `node --test` here has no DOM harness. Lib-level suites (`project-display`, `sidebar-projects`) are untouched and act as the regression net for name resolution. The exact copy strings are pinned in the spec, not in tests — the human approves them at result validation.
</pin-strategy>

<risks>
- The name now renders in the shell before `main` — reviewers must confirm no CSS depends on `ProjectHeader`'s old DOM position and that `useProjectList` firing inside `ProjectTabs` causes no new waterfall (the query is already warm from the sidebar; React Query dedupes).
- Removing the visible page title on project views is the point of the feature but the biggest UX delta; flagged for the human at result validation alongside the copy strings.
- Hydration: `ProjectTabs` already reads `useSearchParams` under `Suspense` and `lastActiveProject` in an effect — the name inherits exactly that pattern; first paint shows the `?project=`-derived name only, no mismatch.
- Reaching for a font package or restructuring beyond the listed files — STOP, anti-scope.
</risks>

<log>
- 2026-10-06 [agent] — plan drafted alongside spec; decomposition into tasks deferred until after the consolidated human validation sitting.
- 2026-10-06 [agent] — v2 per amendment: shell-level name above tabs, new TabHeader component, three page swaps, ProjectHeader deleted; gates and manual routes updated.
- 2026-10-06 [agent] — validated with human proof: notification fb4b0d62-80b0-4184-8c9b-635e4917e395 replied 2026-10-06 13:13:32 UTC
</log>
