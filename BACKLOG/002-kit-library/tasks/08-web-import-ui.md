---
feature: 002
task: 08
slug: web-import-ui
status: todo
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
- [ ] Re-import-flagged candidates are blocked in the dialog with the pointer to Re-import
- [ ] Re-import on an imported kit shows the update report with a visibly distinct conflict entry
- [ ] Clear source (with confirm) flips the badge to manual
- [ ] lint + tsc + build green
</acceptance-criteria>

<refs>
- @../spec.md — D12, R13, R14 (reviewer reads, not executor)
- @../plan.md — task 08 row
- tasks/07-import-api.md — API contracts consumed here
</refs>

<obstacles>
</obstacles>

<log>
</log>
