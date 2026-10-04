---
feature: 063
slug: worktree-feature-binding
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 063 — Worktree/branch ↔ feature binding: agents locate task code and status super fast

> Grooming 2026-10-04. 2 tasks, 01 → 02.

## Task chain

1. **01 — Adapter tool + tests.** `planning_feature_code(slug, projectId?)`: adapter-side `git worktree list --porcelain` + branch listing from the workspace cwd; match by slug and NNN prefix; per-match report (kind/name/path/last commit hash+date/stale flag); combined with the host feature read (status/phase) into one answer; no match → honest empty + convention hint; stub tests (worktree output parsing, stale worktree, multiple matches, no match).
2. **02 — Closeout.** Description teaching (what the tool returns, what convention it expects); architect light; result with post-release click-check (run from this very repo against 091/094).

## Verification

- Stub matrix tests (01), gates; architect light (02).

<log>
- 2026-10-04 [agent] — plan groomed: 2 tasks; discovery adapter-side by design (host cannot see the agent's machine).
- 2026-10-04 [agent] — validated with human proof: notification 9afebe79-0940-4ada-b47c-20edde280c64 replied 2026-10-04 12:00:46 UTC
</log>