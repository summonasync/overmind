---
feature: 002
task: 08
slug: web-import-ui
status: done
review: light
created: 2026-09-14
---

# 002/08 — Web UI: kit import + origin surface

<goal>
The kits UI exposes the whole import story: "Import from folder" → OS folder picker → scan preview dialog (candidate checkboxes, editable names, per-kit issues, re-import flags) → confirm → per-kit import report. Kit list and detail show origin badges (imported + source path + imported-at, manual otherwise), imported kits get "Re-import" (with the update report surfaced) and "Clear source" actions.
</goal>

<context>
Frontend house rules (see AGENTS.md): Next.js 16 static export, FSD layering under `web/src`, React Query v5 with centralized query keys per feature, no `useEffect` data fetching, no direct backend calls from client components except through the existing same-origin `/api` client layer in `shared/api`/`features/*/api`. The API contracts come from task 07 (`POST /api/kits/import/scan`, `/api/kits/import`, `/api/kits/{id}/reimport`, `/api/kits/{id}/clear-source`) — code against the real DTOs in `src/Overmind.Host/Api/Contracts/`. The folder picker already exists: `browseFolder` in `web/src/shared/api/system.ts`. Kit list/detail live in `web/src/pages/kits-page/`, `web/src/widgets/kit-workspace/`, `web/src/features/kits/`. Executor reads ONLY this file.
</context>

<scope>
- `web/src/features/kits/` — api functions + React Query hooks for the four endpoints (extend the existing kits query-key factory; invalidate kit lists/details after import/reimport/clear-source)
- Kits page: "Import from folder" button → `browseFolder()` → scan → preview dialog:
  - candidate list with checkboxes (pre-checked, re-import-flagged candidates shown as "already imported — use Re-import" and disabled)
  - editable kit name per candidate with collision highlighting
  - per-kit issue lists and truncated flag surfaced
  - confirm → import → per-kit report view (success/failure per kit with reasons)
- Origin badges in kit list and kit detail (kind, source path tooltip/text, imported-at); "Re-import" button on imported kits → result report display (added/updated/kept-local/conflicted/removed with per-entry outcomes, conflicts visually distinct); "Clear source" with confirm
- Carry-over from 002/05 (obstacle #2): `AGENT_PLATFORMS` in `web/src/shared/api/types.ts` lags the backend enum — add `"qwen"` and check nothing else hardcodes the platform list
- Carry-over from 002/04 (`audits/review-002-04.md`, IMPORTANT-1 UI side): the synthetic "Kit Library" project appears in project lists — coordinate with the task 07 delete guard; if the API marks it, show it distinctly (badge) rather than hiding it silently. Record the chosen behavior in `<log>`
- Loading/error/empty states consistent with the existing house components (`shared/ui` — check what entries/planning pages use)
</scope>

<anti-scope>
- No backend changes; if a contract gap appears, record it in `<obstacles>`, don't patch the API from this task
- No `.omkit` export/import UI (002b)
- No redesign of existing kit screens beyond the import button + badges + actions
- No new npm packages
</anti-scope>

<constraints>
- `npm run lint && npx tsc --noEmit && npm run build` green in `web/` (existing 6 lint warnings about `<img>` are known/intentional — keep zero errors)
- TypeScript strict, no `any`
- No auto-commits
</constraints>

<acceptance-criteria>
- [ ] Full flow works against the running Host: pick folder → preview with two candidates → rename one → import → both kits appear with origin badges
- [x] Re-import-flagged candidates are blocked in the dialog with the pointer to Re-import (disabled checkbox + name input, "Already imported — use Re-import on the kit instead."; verified statically)
- [x] Re-import on an imported kit shows the update report with a visibly distinct conflict entry (conflicted rows get `border-danger/40 bg-danger/10` + danger outcome badge; other outcomes use plain raised rows)
- [x] Clear source (with confirm) flips the badge to manual (confirm modal → `POST /kits/{id}/clear-source` → `kitKeys.all` invalidation re-renders list + detail)
- [x] lint + tsc + build green (0 lint errors / 6 known `<img>` warnings, tsc clean, static export build succeeds)
</acceptance-criteria>

<refs>
- @../spec.md — D12, R13, R14 (reviewer reads, not executor)
- @../plan.md — task 08 row
- tasks/07-import-api.md — API contracts consumed here
</refs>

<obstacles>
- Contract gap: `ProjectResponse` carries no `isLibrary`-style marker — the synthetic "Kit Library" project is only identifiable by its name (internal `LibraryProjectName` const in `KitImporter`, never surfaced in the contract). Chose minimal local behavior: name-based detection (`web/src/features/projects/lib/kit-library.ts`) + "Library" info badge in the projects list — visible, not hidden. The backend 409 delete guard (kit-linked entries) still protects it regardless of the badge. Proper fix is an API marker field; not patched here per anti-scope.
- [resolved 2026-09-15] Contract quirk: `KitImportSelectionRequest.relativePath` is consumed by the backend as the candidate NAME (`new KitImportSelection(k.RelativePath, k.Name)` → importer matches `c.Name == CandidateName`, Ordinal). The UI therefore sends `candidate.name` in the `relativePath` field — correct for all candidates including the root candidate (whose relative path is `""`). Field is misnamed in the contract; left as-is per anti-scope. → RESOLVED by the 07-side contract fix: the backend now matches selections by the candidate's true relative path, scan candidates carry `relativePath`, and the UI sends `candidate.relativePath`; the name-echo workaround is no longer in effect (sending a name there fails with `candidateNotFound`).
- Environment: fresh worktree had no `web/node_modules` and no `next-env.d.ts` (gitignored, Next-generated). `npm ci` (lockfile only, no new packages) + `npm run build` generated it; standalone `tsc --noEmit` before the first build fails on missing image module declarations for that reason alone.
</obstacles>

<log>
2026-09-15 — implemented the full kit-import surface on the web:

- `web/src/shared/api/types.ts`: added `"qwen"` to `AgentPlatform` + `AGENT_PLATFORMS` (002/05 carry-over). Grep confirmed no other hardcoded platform lists — `AGENT_PLATFORMS` is the single source, its only consumer is `skill-files-editor`.
- `web/src/features/kits/types.ts`: extended `KitResponse` with `sourceKind`/`sourcePath`/`importedAtUtc`; added scan preview / import report / re-import report DTOs matching `KitImportContracts.cs` (camelCase enum strings).
- `web/src/features/kits/api.ts`: `scanKitFolder`, `importKitFolder`, `reimportKit`, `clearKitSource` against the four task-07 endpoints.
- `web/src/features/kits/lib/use-kit-import.ts`: mutations hook; scan/import/reimport/clearSource invalidate `kitKeys.all` (lists + details + drift) on success.
- `web/src/features/kits/lib/import-labels.ts`: failure-kind / scan-issue / re-import-outcome label+tone maps, `formatImportedAt`.
- Import dialog flow: `kit-import-dialog.tsx` (phase switch: scanning / scan error / wizard / report) + `kit-import-wizard.tsx` (pre-checked candidates, editable names with collision + local-duplicate highlighting, truncated banner, empty state, selection counter) + `kit-import-candidate-row.tsx` (checkbox, name input, skills with platform tags, gate/guard counts, per-issue warning rows) + `kit-import-report.tsx` (per-kit success/failure with failure-kind labels and error text). Kits page header gets "Import from folder" → `browseFolder()` → open dialog + fire scan.
- Origin surface: `kit-origin-badge.tsx` (imported=info w/ path+date tooltip, bundled=skill, manual=neutral) in kit list rows; new `KitSourceSection` in the kit detail panel (source path + imported-at, Re-import button → `kit-reimport-report.tsx` modal with counts + per-entry outcomes, conflicted entries visually distinct; Clear source behind a confirm modal).
- Kit Library badge decision: no API marker exists (see obstacles), so the projects list shows a "Library" info badge via name-based local detection — distinct, not hidden; delete remains guarded server-side by the 409.
- Verification (`web/`): `npm run lint` → 0 errors / 6 known `<img>` warnings; `npx tsc --noEmit` → clean; `npm run build` → static export succeeds (9 routes).
- First AC (full flow against running Host) left unticked: no browser available in this run — live verification happens in task 09 e2e.

2026-09-15 — follow-up fixes after light review (`audits/review-002-08.md` IMPORTANT-1, IMPORTANT-2) + the 07-side contract change:

- `web/src/features/kits/types.ts`: `KitScanCandidateResponse` gains required `relativePath` (backend now returns it; root candidate = `""`).
- `kit-import-wizard.tsx`: import selections now send `relativePath: candidate.relativePath` (was `candidate.name` — the backend matches by relative path and pins the old shape with `Import_CandidateNameInsteadOfRelativePath_FailsCandidateNotFound`).
- IMPORTANT-1: collision highlight is live — the wizard pulls current kit names via `useKitList()` and recomputes the existing-name collision on every keystroke (trimmed, case-insensitive, mirroring the backend's OrdinalIgnoreCase duplicate check); the scan-time `nameCollision` flag contributes only while the draft name is still the original candidate name. Row props split into `duplicateName` (local duplicates among selected) and `existingNameCollision`.
- IMPORTANT-2: `formatImportedAt` is exported from the `@/features/kits` barrel; `kit-source-section.tsx` imports it via the barrel instead of deep-importing `lib/import-labels`.
- Verification (`web/`): lint 0 errors / 6 known warnings; tsc clean; build ok.
</log>
