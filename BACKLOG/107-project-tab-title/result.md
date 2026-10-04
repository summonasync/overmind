---
feature: 107
slug: project-tab-title
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Result 107 — Tab title shows the open project («{Project} — Overmind»)

## What shipped

- Single `document.title` owner: `useTabTitle(awaitingCount)` (web/src/widgets/app-shell/lib/use-tab-title.ts) — computes `title = f(projectName, awaitingCount)` and writes it in one effect. Project name resolves reactively from the `?project=` param via `useSearchParams` + the 101 resolver data (`useProjectList`).
- Pure composition `titleFor(projectName, awaitingCount)` (web/src/widgets/app-shell/lib/tab-title.ts): `{Project} — Overmind` with a project, plain `Overmind` without/unresolvable; awaiting `(N)` prefix composes through the existing `awaitingTitle` (087 semantics untouched).
- Graceful fallback: `resolvedProjectName` (shared/lib/project-display.ts) returns `null` for missing/unknown/blank-name projects → title degrades to plain `Overmind` (no «Unknown project» noise, per spec decision).
- Old capture-once hook `use-awaiting-title.ts` deleted — microbatch #134's stale-base constraint is resolved by design (the base is reactive now, ownership is singular).
- `ApprovalBubble` mounts the owner (same awaiting count source as before); `AppShell` wraps it in `Suspense fallback={null}` — the codebase pattern for `useSearchParams` consumers (matches ProjectTabs).

## Verification

- `npm run lint` — 0 errors (6 pre-existing warnings in zerling/app-shell/app-sidebar/app-topbar: `<img>`, location.assign; none in touched code).
- `npx tsc --noEmit` — clean.
- `npm test` — 99/99 pass (new: tab-title.test.mts title matrix; project-display.test.mts extended with resolvedProjectName cases).
- `npm run build` — green, 12/12 static pages prerendered (Suspense boundary resolves the useSearchParams bailout).
- Single writer verified by grep: `document.title` appears only in use-tab-title.ts; no references to the deleted hook remain.

## Files

- web/src/widgets/app-shell/lib/tab-title.ts (new)
- web/src/widgets/app-shell/lib/use-tab-title.ts (new)
- web/src/widgets/app-shell/lib/use-awaiting-title.ts (deleted)
- web/src/widgets/app-shell/ui/approval-bubble.tsx (hook swap)
- web/src/widgets/app-shell/ui/app-shell.tsx (Suspense wrap)
- web/src/shared/lib/project-display.ts (resolvedProjectName)
- web/tests/tab-title.test.mts (new), web/tests/project-display.test.mts (extended)

<log>
- 2026-10-04 [agent] — drafted after implementation; all gates green, awaiting review dispatch.
- 2026-10-04 [agent] — [orchestrator] Light review PASS-WITH-NITS (0 blockers, 2 cosmetic nits → microbatch #174/#175). Reviewer re-ran all gates green independently. Report: audits/review-107-01.md.
</log>