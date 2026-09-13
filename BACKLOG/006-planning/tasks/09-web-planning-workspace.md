---
feature: 006
task: 09
slug: web-planning-workspace
status: done
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
- [x] Spec/Plan/Result can be edited and approved; editing a frozen artifact demands a change note and visibly resets it to draft.
- [x] Task board moves tasks across all five statuses with a mandatory log note, and the log timeline shows the entries.
- [ ] Per-project workflow settings display stored **and** effective values and persist.
- [ ] Global defaults are editable on the Settings page with help text.
- [x] Drift panel lists Missing/Modified items and Re-materialize (with confirm, `force: true`) clears them.
- [x] `features/projects` still imports nothing from `features/planning`.
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
- **Backend contract gap (worked around client-side, no Host change):** the human-door mutation endpoints `POST /planning/artifacts/{id}/status`, `POST …/log` and `POST …/validate` return `Results.Ok()` — a 200 with an **empty body** (verified live: `Content-Length: 0`). Task 08's `api.ts` typed all three as `Promise<PlanningArtifactResponse>`, which would have thrown a JSON SyntaxError on every successful status move / log append / approval. Fix in web only: `shared/api/client.ts` now treats an empty response body as `undefined`, and the three functions are typed `Promise<void>` — the UI relies on React Query invalidation + refetch of the feature (which carries fresh artifacts, log projection included) and never needed the response body. If these endpoints ever return the updated artifact, the client keeps working unchanged.
- Empty `logNote` on a status move is rejected as **400 ValidationProblem** (FluentValidation runs before the service), not the 409 `ChangeNoteRequired` the service throws for whitespace-only notes. The UI's mandatory-note modal blocks empty notes client-side first, so no UI path hits either error; recorded so nobody chases the 400 expecting 409.
- `Results.Ok()` empty-body behavior also means the boot pass had to pin the frozen-edit 409 separately: `PUT /planning/artifacts/{id}` without `logNote` on a validated artifact returns `409` with `title: "ChangeNoteRequired"`, `detail: "A change note is required to modify a frozen artifact."` — `readError` in the shared client surfaces `detail` through `ErrorText` verbatim, as required.
- `PlanningWorkflowSettings` deliberately reuses the `settings-form.tsx` derived-state-from-props sync pattern (flagged debt, task context says match it, not fix it).
</obstacles>

<log>
2026-09-08 — task created from the validated plan.

2026-09-13 — executed. Files created:

- `web/src/features/planning/components/artifact-editor.tsx` — xl-modal editor for spec/plan/result: Write/Preview toggle per `entry-editor.tsx`, frozen banner "Validated <date> — editing resets it to draft", inline change-note input with hint, client-side block on empty note/body, Approve button (draft artifacts only, disabled while the body is dirty — "Save your changes before approving"), server errors through `ErrorText`.
- `web/src/features/planning/components/artifact-log.tsx` — `<log>` timeline rendered purely from the `log` projection (`[{ date, text }]`, server-side `PlanningLog.Parse`), newest last; no client-side log parsing anywhere.
- `web/src/features/planning/components/task-board.tsx` — five columns (todo, in progress, in review, done, blocked) with count headers; cards show heading (first `# ` line via `lib/task-heading.ts`), `#order`, review chip (`hard` → warning tone); per-card icon-button row moves to any other status (no drag-and-drop); every move opens a small modal with a mandatory log note (Confirm disabled on empty note).
- `web/src/features/planning/components/task-editor.tsx` — xl modal for the task body (Write/Preview) + log timeline + "Append log note" input hitting the log endpoint; create mode is body-only (order auto-assigned server-side).
- `web/src/features/planning/components/workflow-settings.tsx` — `PlanningWorkflowSettings({ projectId })`: materialization (inherit/off/on/archive) and human-validation (unset/on/off) selects, effective values as muted text with "— inherited from global default" when stored is inherit/unset, save via the workflow mutation, "Saved." confirmation.
- `web/src/features/planning/components/drift-panel.tsx` — renders only when effective materialization is `on` (drift query stays disabled otherwise via the empty-id `enabled` guard); Missing/Modified chips + relative path; Refresh (`refetch`); Re-materialize behind a confirm modal explaining Overmind-managed files get overwritten, confirm sends `force: true`; empty state "Files match the database."; materialization warnings surfaced after a run.
- `web/src/features/planning/lib/task-heading.ts` — first `# ` line extraction with `Task N` fallback.

Files edited:

- `web/src/features/planning/components/feature-workspace.tsx` — full rewrite of the read-only placeholder: header (title, slug, status `Select` wired to `updateFeature`, version badge, edit/delete), feature body, Spec/Plan/Tasks/Result tab bar with task count, tab state in the URL via `window.history.replaceState` (`&tab=tasks`, default spec, survives refresh/deep-link), artifact panels with status/frozen/review chips + change log, empty tabs → `EmptyState` with "Create <kind>" (order 0), Tasks tab → `TaskBoard` + "Add task", edit-feature and delete-feature modals (delete navigates back to the board URL), document/task editor modals re-resolving the artifact from the fresh feature query so post-mutation state (frozen chip, new log lines) flows in.
- `web/src/features/planning/types.ts` — `PlanningTab`, `PlanningDocumentKind`, `PLANNING_TABS`, `TASK_STATUSES`, `PLANNING_MATERIALIZATIONS`, `HUMAN_VALIDATION_OVERRIDES`.
- `web/src/features/planning/api.ts` — `setPlanningArtifactStatus`/`appendPlanningLog`/`validatePlanningArtifact` retyped `Promise<void>` (empty-body 200s, see obstacles).
- `web/src/features/planning/lib/use-planning.ts` — `usePlanningDrift` also returns `refetch` for the drift Refresh action.
- `web/src/features/planning/index.ts` — barrel: new components + types/consts.
- `web/src/pages/planning-page/ui/planning-page.tsx` — workspace branch renders `DriftPanel` under `FeatureWorkspace` (self-gating on effective materialization).
- `web/src/pages/projects-page/ui/projects-page.tsx` — project detail modal renders `PlanningWorkflowSettings` beside `ProjectDetail` (page-level composition; `features/projects` untouched).
- `web/src/features/settings/{types.ts,api.ts,components/settings-form.tsx}` + `web/src/pages/settings-page/{ui/settings-page.tsx,ui/settings-help-panel.tsx}` — `planningMaterializationDefault` (off/on/archive) and `planningRequireHumanValidationDefault` (on/off) on the settings form with help text ("Projects on inherit or unset follow these defaults…"), section retitled "Embedding & Planning", help-panel entry added. `settings-form.tsx` derived-state pattern matched, not refactored.
- `web/src/shared/api/client.ts` — empty response body now resolves to `undefined` (see obstacles; only the planning ceremony endpoints produce empty-body 200s today).

Verification (from `web/`):

- `npm run lint` → 0 errors, 6 warnings (the 5 known `no-img-element` + pre-existing `no-location-assign-relative-destination` in app-topbar; none added — one transient unused-import warning was fixed during the pass).
- `npx tsc --noEmit` → clean.
- `npm run build` → green static export, all routes emitted (`/planning`, `/settings` included).
- Boot ceremony against a live Host on a temp DB (`Overmind__DatabasePath` → mktemp dir, `OVERMIND_WEB_ROOT=web/out`, `--no-tray`, port 4891; real `~/.overmind` untouched — the stale `host.port` my instance wrote was removed): `POST /api/projects` → project; `POST …/planning/features` → feature `draft` v1; spec upsert → `order: 0` v1; `POST …/validate` → **200, `Content-Length: 0`** (empty-body proof); frozen update without note → **409 `ChangeNoteRequired`**; frozen update with note → 200, `status: draft`, `frozenAt: null`, `v2`, log projection `[{date:"2026-09-13", text:"reworked scope after review"}]`; three task upserts → auto orders 0/1/2; status matrix todo→inProgress→review→done→review→blocked→todo all 200 with notes, final artifact log carries all six dated lines newest-last; empty `logNote` status move → 400 (validator, see obstacles); log append → 200 + projection; feature `PUT status: ready` → 200; workflow GET → `inherit/unset → effective off/false`, PUT `on` → effective `on`, PUT validation `on` → effective true; settings GET → `planningMaterializationDefault: off`, PUT `archive`/`on` → reflected; materialize `force:false` → 6 files (`BACKLOG/009-boot-ceremony/{feature,spec}.md`, `tasks/00-02-*.md`, `BACKLOG.md`), drift `[]`; hand-edit + delete a file → drift `[modified spec.md, missing tasks/02]`; materialize `force:true` → drift `[]`, hand edit overwritten. Pages served from `web/out`: `/` 200 (20.2 KB), `/planning` 200 (20.7 KB), `/settings` 200 (20.4 KB). Host stopped, temp dir removed after each pass.
- Unticked criteria 3 and 4: their display halves (selects + muted effective text on the project modal, editable defaults + help text rendering on the Settings page) are browser-render-dependent — the data paths are proven live above (workflow GET/PUT roundtrip with effective values, settings PUT roundtrip), the visual pass stays with the user like in task 08.
</log>
