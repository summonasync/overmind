---
feature: 123
task: 02
slug: project-name-typography
status: done
review: light
created: 2026-10-06
depends_on: 01
brief: Wire pages: swap PageHeader for TabHeader with pinned copy on planning/entries/issues, remove ProjectHeader + its export, add the brand-book line, run all gates.
---

# Task 2: Page wiring, ProjectHeader removal, brand book, gates

<goal>
Pages adopt the restructured header: description block under the tabs, `ProjectHeader` gone, brand book documents the pattern, all verification gates green.
</goal>

<scope>
- `web/src/pages/planning-page/ui/planning-page.tsx` — remove both `ProjectHeader` usages; dashboard view `PageHeader` → `TabHeader` (Dashboard copy + existing actions «← All projects» / «New feature»); feature branch and no-project branch keep their layouts.
- `web/src/pages/entries-page/ui/entries-page.tsx` — remove `ProjectHeader`; project-view `PageHeader` → `TabHeader` (Entries copy + existing actions); no-project branch untouched.
- `web/src/pages/issues-page/ui/issues-page.tsx` — project-view `PageHeader` → `TabHeader` (Issues copy + existing actions); the `Issues Board — {project.name}` title disappears with it; no-project branch untouched.
- `web/src/features/projects/components/project-header.tsx` — delete; remove the export from `web/src/features/projects/index.ts`; no dangling imports.
- `web/brand-book.md` — §5/§6 line documenting the project workspace header pattern (name above tabs, per-tab description, tab title omitted).

Pinned copy (verbatim, do not invent or re-word):
- Dashboard: `Plan features as spec, plan, tasks and result. The board order is the queue — first card is next up.`
- Entries: `Capture knowledge, decisions and skills — turn experience into a lasting advantage.`
- Issues: `Cross-project bug intake. Agents file issues from any project; triage, follow up, or promote to a task.`
</scope>

<anti-scope>
- No changes to no-project branches (they keep `PageHeader` + hint), `feature-workspace.tsx`, `ProjectNameBadge` chips, list/select surfaces, kit pages, topbar/sidebar, browser tab titles.
- No new font packages, no `next/font`, no globals.css edits.
</anti-scope>

<constraints>
- Copy strings exactly as pinned above; they are human-approved spec content, not coder copywriting.
- Actions rows transfer as-is from the current `PageHeader` usages.
- Exactly one h1 per view (task 1 already establishes the name as h1/p — pages must not introduce competing heading levels).
</constraints>

<acceptance-criteria>
- [x] `/dashboard?project=…` renders: name → tabs → Dashboard description + «← All projects» / «New feature»; exactly one h1.
- [x] `/entries?project=…` renders the Entries copy + «← All projects» / «New entry» under the tabs; exactly one h1.
- [x] `/issues?project=…` renders the Issues copy + actions under the tabs; the duplicated board title is gone; exactly one h1.
- [x] `/dashboard?project=…&feature=…` shows no Dashboard description block; the feature title remains the page h1.
- [x] No-project views of all three pages render exactly as before.
- [x] `project-header.tsx` is deleted, `index.ts` export removed, zero dangling references.
- [x] `web/brand-book.md` documents the header pattern.
- [x] `npm run lint && npx tsc --noEmit`, `npm run test`, `npm run build` all pass from `web/`.
</acceptance-criteria>

<refs>
- Spec: artifact 39be6c89-6ac9-4b6c-ab26-0b672735eee6 — copy strings, anti-scope, acceptance.
- Plan: artifact ab0225aa-60b1-4d27-8017-f6bc45df156f, approach move 3, files 3–8, verification and pin-strategy sections.
- Task 1 (order 1) — provides `TabHeader` and the shell-level name; compile dependency.
- `web/src/shared/ui/page-header.tsx` — stays for no-project views.
- `web/brand-book.md` §5 Typography, §6 Layout.
</refs>

<obstacles>
- None observed at planning time. If any page's actions row does not fit the `TabHeader` actions slot cleanly, keep the actions inside `TabHeader` (layout contract) and record the quirk here rather than widening scope.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan v2, move 3 + cleanup + brand book + gates; blocked by task 1 (TabHeader import).
- 2026-10-06 [agent] — task 02 coding round started
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers. Pinned copy byte-exact (grep -F), single-h1 invariant holds, ProjectHeader removed clean, no-project branches untouched, gates re-run 254/254. Commit 7aefdb8.
</log>