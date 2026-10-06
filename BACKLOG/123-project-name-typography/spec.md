---
feature: 123
slug: project-name-typography
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Spec 123 — Project name typography: distinct display treatment vs page headings

<goal>
Restructure the project-page header: the project name sits ABOVE the tab bar as the branded top element, tabs (Dashboard / Entries / Issues) follow, and below the tabs each tab shows its own description. Typography (brand purple + mono, compact) dresses the name; the restructure itself is the feature. Today the name renders as an `<h1>` inside page content identical to the page title below it — no hierarchy, no brand, duplicated labels.
</goal>

<scope>
- **Name above tabs.** The project name moves into the shell-level tab strip (`web/src/widgets/app-shell/ui/project-tabs.tsx`): rendered above the tab bar, inside the same horizontal padding, only when a project is resolved (`?project=` param, else last-active). Brand treatment: `text-primary` (#a855f7 token), mono stack (`--font-mono`), compact scale (≤ text-base, e.g. `font-mono text-sm font-semibold tracking-wide`), truncation for long names.
- **Layout order pinned, top to bottom:** 1) project name, 2) tab bar, 3) description block. Applies to all three project-tab routes: `/dashboard?project=…`, `/entries?project=…`, `/issues?project=…`.
- **Description block (tab title omitted).** A new lightweight `TabHeader` component (`web/src/shared/ui/tab-header.tsx`) renders the current tab's description plus page actions — no visible tab title, since the tab label sits directly above (decision: omit, see obstacles). Pages swap `PageHeader` for it on project views:
  - `planning-page.tsx` dashboard view — description + actions («← All projects», «New feature»).
  - `entries-page.tsx` — description + actions («← All projects», «New entry»).
  - `issues-page.tsx` project view — description + actions; the current `Issues Board — {project.name}` title duplication with the name above disappears.
- **`ProjectHeader` removal.** `web/src/features/projects/components/project-header.tsx` and its export in `features/projects/index.ts` are deleted; its three usages (planning-page ×2, entries-page) go away.
- **Per-tab static copy** (drafted, English — exact strings below for human approval):
  - Dashboard: `Plan features as spec, plan, tasks and result. The board order is the queue — first card is next up.`
  - Entries: `Capture knowledge, decisions and skills — turn experience into a lasting advantage.`
  - Issues: `Cross-project bug intake. Agents file issues from any project; triage, follow up, or promote to a task.`
- **Feature workspace view** (`/dashboard?project=…&feature=…`): the name stays visible above the tabs but demoted to a non-heading element; the feature title in `feature-workspace.tsx` remains the page h1; the Dashboard description block does not render there.
- **Brand book:** `web/brand-book.md` gets the documented pattern (name above tabs, per-tab description, tab title omitted).
</scope>

<anti-scope>
- No rebrand of `ProjectNameBadge` chips (notifications, bell preview, approval bubble, awaiting rows, global issue rows) — Badge info tone stays.
- No changes to project names in data lists, selects, filters, search rows, kit `<option>`s.
- No changes to no-project views (planning/entries/issues without `?project=`): they keep their current `PageHeader` + hint layout — the restructure targets the project workspace header only.
- No changes to `AppTopbar`, `AppSidebar`, `MobileNav`, `InfoPanel`, kit pages, search, settings.
- No changes to the feature title block inside `feature-workspace.tsx` — it is untouched and stays the h1 of the feature view.
- No new font packages, font files or `next/font` — system stacks stay zero-payload.
- No browser tab title, favicon or `document.title` work.
</anti-scope>

<constraints>
- Colors resolve from theme tokens only (`text-primary` / `--color-primary`); no hardcoded hex in components.
- Font treatment uses existing `--font-sans` / `--font-mono` from `@theme` in `web/src/app/globals.css`.
- Dark-only app: treatment holds on `#0a0a0f`; `text-primary` keeps WCAG AA (≥ 4.5:1) at the rendered size.
- Brand purple is identifier, not state (065): it does not vary with status, selection or hover.
- Layout order is exactly: name → tabs → description block; no extra spacing layer between them beyond the tab strip's existing padding block.
- No CLS: static CSS and markup, no async font loading; first paint and hydrated render identical.
- A11y heading decision (recorded): the project name is the single `<h1>` on root tab views (the page IS the project workspace); on the feature workspace view the name renders as `<p>` and the feature title is the single h1. Exactly one h1 per rendered view.
</constraints>

<acceptance-criteria>
- [ ] On `/dashboard?project=…` the visual order top-to-bottom is: project name (brand purple, mono, compact), tab bar, then the Dashboard description with its actions; the name sits above the tabs.
- [ ] Same order and treatment on `/entries?project=…` and `/issues?project=…`, with the Entries and Issues copy strings from this spec.
- [ ] The tab title is omitted from the description block on all three project views — no duplicated label between the tab bar and the block below it.
- [ ] The name renders as the single h1 on root tab views; on the feature workspace view it renders as p and the feature title is the single h1 — exactly one h1 per view, no other heading levels regress.
- [ ] `ProjectHeader` is deleted with no dangling imports or exports.
- [ ] Treatment resolves from `--color-primary` and existing font tokens; no webfont requests; no hydration mismatch.
- [ ] `ProjectNameBadge` chips, list/select surfaces, no-project views and the feature title block render exactly as before.
- [ ] `npm run lint`, `npx tsc --noEmit`, `npm run test` and `npm run build` (static export) pass in `web/`.
</acceptance-criteria>

<refs>
- `web/src/widgets/app-shell/ui/project-tabs.tsx` — the tab strip; gains the name; already owns project resolution (`?project=` → last-active fallback).
- `web/src/widgets/app-shell/ui/app-shell.tsx` — renders `ProjectTabs` above `<main>`; no change expected.
- `web/src/shared/ui/page-header.tsx` — current title+description+actions pattern; stays for no-project views.
- `web/src/shared/ui/tab-header.tsx` — new component (this feature).
- `web/src/features/projects/components/project-header.tsx`, `web/src/features/projects/index.ts` — removed.
- Pages: `web/src/pages/planning-page/ui/planning-page.tsx`, `web/src/pages/entries-page/ui/entries-page.tsx`, `web/src/pages/issues-page/ui/issues-page.tsx`.
- `web/src/features/planning/components/feature-workspace.tsx` — feature title h1, untouched, becomes the sole h1 of the feature view.
- `web/src/app/globals.css` `@theme`, `web/brand-book.md` §5/§6, feature-number identity (`feature-number-badge.tsx`, 065).
</refs>

<obstacles>
- **Drafted copy strings need human approval.** The three per-tab descriptions above are new static copy (Dashboard/Entries/Issues — the real tab set per `PROJECT_TAB_ROUTES`). Entries/Issues paraphrase existing page copy; Dashboard shortens `PLANNING_DESCRIPTION`. Approve exact wording at result validation or propose edits.
- **Tab-title omission flagged.** Human said the tab name in the bottom block is optional («не обязательно»); leaning omit because the label sits directly above — recorded as the decision here. Overridable at validation.
- **Structural surprise resolved:** the name must render above the tab bar, which lives in the app shell, not in page content — so the name moves from `features/projects` into `widgets/app-shell/ui/project-tabs.tsx` and `ProjectHeader` dies. The name's h1/p role switches on the same component via the `?feature=` param.
- **Feature-view description suppressed:** showing the Dashboard description above a feature workspace would stack three competing blocks; the block renders on root tab views only. Deviation from a literal reading of the amendment — flagged.
- **Font dependency still open (carried from v1):** a loaded display typeface remains an npm/licensing decision for the human; this feature stays on system stacks.
</obstacles>

<log>
- 2026-10-06 [agent] — spec drafted from feature body + code inspection of all project-name render sites (draft, awaiting consolidated human validation).
- 2026-10-06 [agent] — v2 per human amendment: name above tab bar (shell-level), per-tab description block, tab title omitted, ProjectHeader removed; drafted per-tab copy for approval.
- 2026-10-06 [agent] — validated with human proof: notification fb4b0d62-80b0-4184-8c9b-635e4917e395 replied 2026-10-06 13:13:32 UTC
</log>
