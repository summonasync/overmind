---
feature: 004
task: 02
slug: sidebar-tree
status: done
review: light
created: 2026-09-08
---

# 004/02 — Sidebar tree + register-in-place flow

<goal>
The sidebar's Projects item expands into a lazy disk folder tree: project folders are highlighted and link to `/?project=<id>`, inactive folders can be registered as projects in place, and the Projects page auto-opens the project passed via `?project=<id>`.
</goal>

<context>
Backend contract is delivered by task 01 (`GET /api/system/tree-roots`, `GET /api/system/list-directory?path=`). Read task 01's file for the exact response shapes before coding against them. The app is a static Next.js export with MPA navigation (plain `<a href>` — the Next 16 static-export client router is broken upstream, do NOT use next/link or useRouter for navigation). Data access: React Query + same-origin fetch via `shared/api/client.ts` (ADR-A).
</context>

<scope>
`web/`:
- Move `browseFolder` from `features/system` to `shared/api` (closes architect-review WARNING-4); update imports (project-form).
- `features/projects/queries.ts`: add `tree: (path: string) => [...] as const` to the project keys factory. Hooks: `useTreeRoots()`, `useDirectory(path: string | null)` — `enabled` only when a path is provided (node expanded), `staleTime: 60_000`.
- `features/projects/components/project-tree.tsx`: recursive lazy tree. Node states: collapsed/expanded/loading/error (inline, per node, doesn't break siblings). Project node: `text-primary`, folder-open icon, `<a href="/?project=<id>">`. Inactive node: muted, expand chevron, plus a small register icon-button (Plus) with tooltip "Register as project". Truncate long names, `title` attr for full path. Visual depth indent ~12px/level.
- `widgets/app-shell/ui/app-sidebar.tsx`: Projects nav item gains a chevron; expanded shows tree roots and the recursive tree beneath. Expansion state in React state (session-only, no localStorage).
- Register flow: clicking register opens the existing create-project modal (reuse the component the Projects page uses) with Disk Path prefilled to the node path. On success: `queryClient.invalidateQueries` for project lists AND the whole tree key prefix — the new project node must light up without manual refresh.
- Projects page: read `?project=<id>` on mount and auto-open that project's detail (same UI the list item click opens).
- Projects flat list on the page stays unchanged otherwise.
</scope>

<anti-scope>
- No backend changes (task 01 owns them).
- No file nodes, no drag-and-drop, no bulk register, no localStorage persistence of expansion.
- No next/link / useRouter — MPA only.
</anti-scope>

<constraints>
- FSD: tree components + hooks in `features/projects`; sidebar wiring in `widgets/app-shell`; api client in `shared/api`. No cross-feature imports.
- `npm run lint && npx tsc --noEmit && npm run build` clean (existing no-img warnings acceptable).
- No `useEffect` for data fetching; React Query only. The random-Kerrigan rAF pattern is the template for mount-only client effects.
- Brand-book styling (dark zerg theme, primary accent for active/highlighted).
</constraints>

<acceptance-criteria>
- [ ] Tree expands lazily; project nodes highlighted + navigate; inactive nodes muted.
- [ ] Register flow creates a project with prefilled path; tree reflects it without manual refresh.
- [ ] `/?project=<id>` auto-opens the project detail.
- [ ] lint/tsc/build clean; manual smoke against the real host recorded in <log>.
</acceptance-criteria>

<refs>
- @../spec.md — R1–R6
- @../plan.md — task 02
- tasks/01-disk-enumeration-api.md — backend contract
- web/brand-book.md — styling
- BACKLOG/001-overmind-mvp/tasks/07-web-ui.md — MPA navigation obstacles (why no next/link)
</refs>

<obstacles>
- Executor sub-agent run was interrupted mid-flight after the code landed but before verification/bookkeeping; orchestrator verified (lint/tsc/build) and smoke-tested instead.
- Hidden directories are skipped by the backend by design — anything under e.g. `AppData` is unreachable in the tree. By design per spec, noted so it doesn't get reported as a bug.
- Project nodes are leaf nodes (no expansion inside a project folder) — MVP scope, revisit if nesting projects inside projects becomes a thing.
</obstacles>

<log>
2026-09-08 — task created from approved plan.
2026-09-08 — implementation landed (executor sub-agent, interrupted before verification): ProjectTree recursive lazy component, useTreeRoots/useDirectory hooks, tree key factory, sidebar chevron + register modal with prefilled ProjectForm, /?project=<id> deep link via useSearchParams + Suspense, browseFolder moved to shared/api (closes review WARNING-4), invalidation on project mutations covers lists + tree prefix.
2026-09-08 — verified by orchestrator: lint 0 errors (6 known warnings), tsc clean, build clean. Headless-Chrome smoke (4/4 PASS): tree lazy-expands E:\ → project node highlighted with /?project= href; deep link auto-opens detail; register modal prefilled from inactive node; created project lit up in tree without manual refresh (temp project + dir cleaned up after).
</log>
