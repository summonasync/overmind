---
feature: 063
slug: worktree-feature-binding
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 063 — Worktree/branch ↔ feature binding: agents locate task code and status super fast

> Grooming 2026-10-04 (user in session). Convention in place: branches/worktrees carry `feat/NNN-slug` matching board slugs — zero-input discovery is possible.

## Goal

An agent gets the code location for a task in ONE call: which branches/worktrees exist for feature NNN, where they live, how fresh they are — combined with the feature's ceremony status. No grep-ing the repo layout, no guessing.

## Decisions

- **Discovery lives in the MCP adapter** (it runs on the agent's machine — the host cannot see the local git): `planning_feature_code(slug, projectId?)` runs `git worktree list --porcelain` + branch listing from the adapter's cwd/workspace, matches by slug and by NNN prefix; combines with a host read of the feature (status/phase via the existing doors) into one answer.
- **Report per match:** kind (worktree/branch), name, path, last commit short hash + date, stale flag (worktree path missing on disk = stale, reported as such, not dropped). Multiple matches → all listed, none guessed. No match → honest empty with the convention hint («expected feat/NNN-slug»).
- **Read-only** — no mutation of git state, no binding storage.

## Non-goals

- V2 structured link (branch field on the feature, sync'd by the creator) — only if convention-based discovery proves insufficient; writes to git; non-`feat/` conventions (configurable pattern is a v2 thought).

<log>
- 2026-10-04 [agent] — groomed with the user in session: adapter-side discovery (host cannot see agent's machine), zero-input v1, stale reported honestly, v2 link deferred.
- 2026-10-04 [agent] — validated with human proof: notification 9afebe79-0940-4ada-b47c-20edde280c64 replied 2026-10-04 12:00:46 UTC
</log>