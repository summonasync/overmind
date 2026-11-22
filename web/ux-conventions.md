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

### 4.1 Navigation set (184)

`tests/journal-nav-convention.test.mts` pins the nav surfaces the same way:

- the topbar and the mobile nav carry the **same ordered item set** (Journal sits after Attention, icon `NotebookPen`);
- **no href carries a `.html` suffix.** This is load-bearing, not cosmetic: the journal's bare-`N` quick key is scoped by `pathname === "/journal"`, so one suffixed href silently kills the shortcut even though the static export serves `/journal.html`;
- every href is backed by a real `src/app/<segment>/page.page.tsx`;
- neither app-shell file declares its own `NAV_ITEMS` — the two surfaces are checked against one independent expected list, so a third hand-kept copy fails the test.

Two more pins live beside them: `tests/journal-stream.test.mts` fails when a feature imports a sibling feature's internals (the barrel rule in `STRUCTURE.md`), and `tests/primary-cta-contrast.test.mts` fails when the primary CTA's label/fill pair drops below 4.5:1 in **either** state — rest and hover are measured separately, because no single static label colour passes both fills.

### 4.2 Journal graph route (237)

`tests/journal-graph.test.mts` pins the route's grammar the same source-text way:

- **Orphans-first is a first-class filter, not a buried toggle.** The day-one field is mostly a pile of unlinked nodes, so `orphansOnly` sits beside the other filters at the top level. It is mutually exclusive with `focus`: turning one on clears the other client-side (`withOrphansOnly`/`withFocus`), because the endpoint refuses the pair outright (`OrphansWithFocus`).
- **The filters are the URL.** The seven query keys (`GRAPH_QUERY_KEYS` in `lib/graph-filters.ts`) are the endpoint's own parameter names, committed with `router.replace` — a chip click is not a history entry the owner backs out of one by one — so a reload, a back gesture and a pasted link open the identical field. The drift pin reads both the filter grammar and the query builder off disk and fails when the two lists diverge.
- **Canvas island conventions.** Click selects (component state, never a route change); double-click on a note navigates to `/journal?note=<id>` carrying `from=journal-graph` plus the field's filters (`gq`), so the note view's back link returns to the same field, and its label says "Back to graph map"; drag pins server-side and a failed PUT re-reads the Host's positions instead of promising a pin; hover dims the rest of the field to 30%; a server refusal names its filters — the sentence renders verbatim with a framing line that says the list is a superset, never culprit-only.
- **Reduced motion and keyboard parity.** `prefers-reduced-motion: reduce` settles the whole field before the first frame — same links, same list, nothing animating; every over-ceiling rung states the real count in its notice instead of pretending to animate. The canvas is focusable and keyboard-operable (arrows walk nodes, Enter opens the panel), and the node list below it holds the same data as the explicit keyboard path — the list is the guarantee, the map is the augmentation.
- **The ladder names its rungs (271).** `layoutModeFor` picks the tier on the node count — live simulation through 1000, one chunked settle through 5000, project clusters past that — and each tier notice names the rung it crossed with the real node count, never claiming to animate what only renders; the tier-2 notice keeps saying input is handled between slices. Above the first rung labels are earned, not given: only hubs (link degree ≥ 4) label until the zoom passes 1.25, then every node does. In the cluster tier a collapsed cluster dot opens its members on click and an open one collapses back to the settled anchor frame; expansion is view-local — a reload opens the field collapsed — and the node list below the canvas stays the primary path at every rung.

Run all of them: `node --test "tests/**/*.test.mts"` from `web/`.

Header-shape consolidation (`PageHeader`/`TabHeader`/hand-rolled workspace header → one primitive) and other follow-up candidates are catalogued in the research doc, not covered here.
