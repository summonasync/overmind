---
feature: 123
slug: project-name-typography
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Result 123 — Project name typography: distinct display treatment vs page headings

## What landed

The amended design landed in full:

- **Project name above the tab bar.** Shell-level change in `web/src/widgets/app-shell/ui/project-tabs.tsx` — the resolved project's name renders above the tab strip with the brand treatment (`text-primary` + mono, compact, truncate), via a `ProjectName` subcomponent (hooks correctness inside the tab strip's conditional rendering). Heading role switches per view: `h1` on root tab views, `p` on the feature workspace view — single-h1 invariant holds on every view.
- **Tab bar** unchanged in position, now sandwiched: name → tabs.
- **`TabHeader`** — new `web/src/shared/ui/tab-header.tsx`: description + actions, no title (tab label sits directly above; omission decision recorded in spec obstacles). Wired into all three project views with the human-pinned copy:
  - Dashboard: `Plan features as spec, plan, tasks and result. The board order is the queue — first card is next up.`
  - Entries: `Capture knowledge, decisions and skills — turn experience into a lasting advantage.`
  - Issues: `Cross-project bug intake. Agents file issues from any project; triage, follow up, or promote to a task.`
- **`ProjectHeader` deleted** with its barrel export (`features/projects/index.ts`) — zero dangling references; pages carry no project-name h1 anymore.
- **`brand-book.md` §5** documents the pattern (name above tabs, per-tab description, tab title omitted).

## Verification

- Gates green from `web/`: `npm run lint && npx tsc --noEmit`, `npm run test` (254 tests), `npm run build` (static export).
- Light reviews ×2 — 0 blockers: task 01 commit `80d05b2` (shell) APPROVED; task 02 commit `7aefdb8` (pages) APPROVED, pinned copy byte-exact, single-h1 invariant re-checked after `ProjectHeader` removal.
- No-project views, chips, feature workspace title: untouched, per anti-scope.

## Human follow-ups (result validation)

- Copy wording re-approvable: the three description strings above are the pinned drafts — edit freely at validation if the tone is off.
- Browser eyeball of the new header hierarchy on `/dashboard?project=…`, `/entries?project=…`, `/issues?project=…`, and `/dashboard?project=…&feature=…` (name demoted to p there).

## Findings

- Non-blocking: entries inline-description asymmetry filed to microbatch ledger as #248 — out of this feature's scope.

<log>
- 2026-10-06 [agent] — result drafted: both tasks done (80d05b2, 7aefdb8), gates green, reviews 0 blockers ×2; awaiting human result validation (copy re-approval + eyeball).
</log>
