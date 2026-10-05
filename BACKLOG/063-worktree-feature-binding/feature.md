---
feature: 063
slug: worktree-feature-binding
status: inProgress
priority: p1
version: 1
created: 2026-09-23
---

# Feature 063 — Worktree/branch ↔ feature binding: agents locate task code and status super fast

User request 2026-09-23 (session): bind `.claude/worktrees/*` worktree titles and feature branches (`feat/NNN-slug`) to Overmind planning features, so any agent can **find the code related to a task super fast** and gather its status without grep-ing the repo layout.

Convention already in place: branches and worktrees carry the feature number + slug (`feat/043-status-provenance`), matching board slugs — which makes a zero-input v1 possible.

V1 sketch (read-only discovery):
- MCP surface (e.g. `planning_feature_code(slug)` or a block in `planning_read_feature`): list worktrees under the project root (`.claude/worktrees/`, `git worktree list`) and branches whose names match the feature slug or NNN prefix; report branch name, worktree path, existence, last commit short hash + date.
- Stale/deleted worktrees reported as such; multiple matches listed, not guessed.

V2 (structured link): store the binding on the feature (branch field), kept in sync by the agent that creates the worktree — only if convention-based discovery proves insufficient.

Value: agent gets task artifact + spec context + exact code location + git status in one hop; status collection ("what's the state of 043") becomes a single call.

<log>
- 2026-10-04 [auto] — artifact created → inProgress
- 2026-10-05 [agent] — Van by claude claimed the feature
</log>
