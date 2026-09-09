---
feature: 006
task: 09
slug: web-planning-workspace
status: todo
review: light
created: 2026-09-08
---

# 006/09 — Web: feature workspace, editors, workflow settings, drift

<goal>
The ceremony is fully usable by a human in the browser: edit and approve spec/plan, drive tasks across statuses on a light kanban, read the log timeline, configure per-project workflow and global defaults, and see plus fix materialization drift.
</goal>

<context>
Task 08 built the slice (`features/planning`), the board and the `/planning?project=&feature=` route with a placeholder workspace. This task replaces the placeholder and adds the settings surfaces.

Editor template: `web/src/features/entries/components/entry-editor.tsx` — Write/Preview toggle, `Markdown` renderer from `shared/lib/markdown`, `Textarea` filling the modal body; hosted in an xl `Modal` (`shared/ui/modal.tsx`, `size="xl"`, portal + Escape).
Confirm/delete template: the two `Modal` blocks at the bottom of `pages/entries-page/ui/entries-page.tsx`.
Settings template: `web/src/features/settings/{types.ts,api.ts,components/settings-form.tsx}` + `pages/settings-page/ui/settings-page.tsx`.
Projects page composition: `pages/projects-page/ui/projects-page.tsx` renders `ProjectDetail` — this task adds the workflow panel **from the page**, because `features/projects` must not import `features/planning`.

Known frontend debt to respect, not repeat: `settings-form.tsx` uses a derived-state-from-props pattern that a review already flagged — match the surrounding code, don't refactor it here.
</context>

<scope>
`features/planning/components/feature-workspace.tsx` — header (title, slug, status select, version, edit/delete actions) + tabs **Spec / Plan / Tasks / Result**; tab content from the feature's artifacts; a tab with no artifact shows an empty state with "Create <kind>" (order 0). Tab state in the URL (`&tab=tasks`) so a refresh keeps the place, defaulting to Spec.

`features/planning/components/artifact-editor.tsx` — xl modal markdown editor (Write/Preview, reused toggle pattern) for spec/plan/result:
- when the artifact is frozen (`status: validated`), saving requires a change note: an inline "Change note" input appears with the hint that saving resets the artifact to draft and requires re-approval; submitting it empty is blocked client-side and the server 409 `ChangeNoteRequired` is surfaced through `ErrorText`;
- **Approve** button (visible for spec/plan/result): calls validate, shows the frozen chip afterwards; a frozen artifact's editor banner reads "Validated <date> — editing resets it to draft".

`features/planning/components/task-board.tsx` — five columns (`todo`, `in_progress`, `review`, `done`, `blocked`) with cards; each card shows the task heading (first `# ` line of the body), order, review chip (`hard` highlighted); status moves are buttons on the card (chevron menu or a small row of arrow buttons — no drag and drop); a move prompts for the required log note in a small modal.

`features/planning/components/task-editor.tsx` — xl modal for the task body + a `<log>` timeline rendered from the artifact response's `log` projection (`[{ date, text }]`, produced server-side by `PlanningLog.Parse` — plan amendment A2), newest last, plus an "Append log note" input that calls the log endpoint. **No client-side log parser**: re-implementing the `<log>` grammar in TypeScript would drift from the server one, so if the projection is missing a field the UI needs, that is a backend contract gap to record in `<obstacles>`, not something to parse out of `body` locally.

`features/planning/components/workflow-settings.tsx` — `PlanningWorkflowSettings({ projectId })`: two selects (materialization `inherit|off|on|archive`, human validation `unset|on|off`), the effective values shown as muted text ("Effective: on — inherited from global default"), save via the workflow mutation.

`features/planning/components/drift-panel.tsx` — shown when the effective materialization is `on`: list of drift items (`Missing`/`Modified` chips + relative path), a Refresh action, and **Re-materialize** which opens a confirm modal explaining that Overmind-managed files will be overwritten; the confirm sends `force: true`. Empty drift state: "Files match the database."

`pages/planning-page/ui/planning-page.tsx` — swap the placeholder for `FeatureWorkspace`; render `DriftPanel` under the workspace when materialization is effectively `on`.
`pages/projects-page/ui/projects-page.tsx` — render `PlanningWorkflowSettings` for the selected project beside `ProjectDetail` (page-level composition, no cross-feature import).
`features/settings/{types.ts,api.ts,components/settings-form.tsx}` + `pages/settings-page/ui/settings-page.tsx` — add the two global defaults (`planningMaterializationDefault`: off/on/archive; `planningRequireHumanValidationDefault`: on/off) with help text explaining that projects on `inherit`/`unset` follow them; `settings-help-panel.tsx` gets a matching short entry.

Barrel: export the new components from `features/planning/index.ts`.

Verification: `npm run lint && npx tsc --noEmit && npm run build` clean; manual pass — create a feature, write a spec, Approve it, edit it (change note → back to draft), add three tasks, move one to `in_progress` and one to `blocked`, append a log note and watch the timeline, flip the project materialization to `on`, edit a written file by hand, see it in the drift panel and re-materialize it.
</scope>

<anti-scope>
- No drag-and-drop kanban, no comments/discussion threads, no notifications (spec non-goals).
- No version-history browser UI (the endpoint exists; a diff viewer is a later feature).
- No changes to the Entries/Kits/Search slices; no refactoring of `settings-form.tsx` beyond adding the two fields.
- No backend changes: if a contract does not fit the UI, record it in `<obstacles>` and raise it instead of patching the Host here.
</anti-scope>

<constraints>
- `npm run lint && npx tsc --noEmit && npm run build` clean; no new lint warnings.
- TypeScript strict, no `any`; `'use client'` only where needed.
- All fetches via `shared/api/client.ts`; all keys via `planningKeys`; mutations invalidate board + feature keys.
- No `useEffect` for data fetching (browser APIs only — e.g. tab state syncing with the URL is fine through `useSearchParams`).
- Plain `<a href>` navigation; Tailwind tokens from `web/brand-book.md`; reusable primitives from `shared/ui` instead of new one-off controls.
- No git commits — report the changed-file list.
</constraints>

<acceptance-criteria>
- [ ] Spec/Plan/Result can be edited and approved; editing a frozen artifact demands a change note and visibly resets it to draft.
- [ ] Task board moves tasks across all five statuses with a mandatory log note, and the log timeline shows the entries.
- [ ] Per-project workflow settings display stored **and** effective values and persist.
- [ ] Global defaults are editable on the Settings page with help text.
- [ ] Drift panel lists Missing/Modified items and Re-materialize (with confirm, `force: true`) clears them.
- [ ] `features/projects` still imports nothing from `features/planning`.
</acceptance-criteria>

<refs>
- @../spec.md — "UI" (full editor), R6; "Materialization" (drift + Re-materialize)
- @../plan.md — Task 09
- @./08-web-planning-board.md — slice, keys, hooks, route
- web/src/features/entries/components/entry-editor.tsx, web/src/shared/ui/modal.tsx, web/src/shared/lib/markdown.tsx
- web/src/features/settings/*, web/src/pages/projects-page/ui/projects-page.tsx
- STRUCTURE.md — "Frontend structure (web/src, FSD)"
</refs>

<obstacles>
</obstacles>

<log>
2026-09-08 — task created from the validated plan.
</log>
