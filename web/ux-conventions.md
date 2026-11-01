# UX Conventions — Overmind Web

Layout conventions pinned by feature 183. Brand tokens and palette live in `brand-book.md`; this file owns placement rules. Research background: `../audits/research-183-01.md`.

## 1. Back Links: Left, Above Content

Every page-level "go up" link renders as the first row of the page body, left-aligned, above the title/header — never inside a header `actions` slot, never on the right.

- Primitive: `src/shared/ui/page-back-link.tsx` (`href` + `label`; the `←` arrow and the muted/underline/focus styling live only there).
- Surfaces: planning, entries, issues pages; feature workspace (both branches).
- Rule: a new back surface imports `PageBackLink`. Do not hand-roll `←` links or copy the class string.

## 2. Page-Level Action Buttons: Left, Same Axis

Primary page actions (`New feature`, `New entry`, `New project`, `New kit`, `Manage projects`) render on the left, flowing after the title/description text — not anchored to the right edge.

- Mechanics: `src/shared/ui/tab-header.tsx` and `src/shared/ui/page-header.tsx` lay text and `actions` out on one left-flowing flex row (no `justify-between`/`justify-end`).
- Out of scope: content toolbars under a title (feature workspace status/priority/Edit/Delete row), modal and wizard-internal buttons, `EmptyState` actions.
- Issues page deliberately stays action-free at page level.

## 3. Tree: Folders Without Projects Are Muted

In the projects tree, folders containing zero Overmind projects render muted; folders containing at least one project keep normal styling.

- Where: `src/features/projects/components/project-tree.tsx`.
- Predicate: `projectFolders` set — server `TreeRoot.containsProjects` for roots plus client-derived `projectAncestorPaths(rootPaths, project.diskPath)` for any depth.
- Presentation: `opacity-70` on the row, `aria-disabled` semantics — rows stay expandable, focusable, browsable. Never tie a `disabled` attribute to the predicate (the only chevron guard remains `disabled={!entry.accessible}`).

## 4. How This Is Pinned

`tests/back-link-convention.test.mts` and `tests/action-buttons-tree-convention.test.mts` (plain `node --test`, no framework) fail on drift:

- back link bypasses the primitive, mounts below the header, or a `←`/class string reappears outside `page-back-link.tsx`;
- header primitives regain `justify-between`/`justify-end` or render actions before text;
- tree muting loses the predicate, the `aria-disabled` semantics, or gains a hard `disabled`.

Run: `node --test tests/back-link-convention.test.mts tests/action-buttons-tree-convention.test.mts` from `web/`.

Header-shape consolidation (`PageHeader`/`TabHeader`/hand-rolled workspace header → one primitive) and other follow-up candidates are catalogued in the research doc, not covered here.
