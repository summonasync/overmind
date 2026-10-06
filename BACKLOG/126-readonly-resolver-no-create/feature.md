---
feature: 126
slug: readonly-resolver-no-create
status: done
priority: p1
version: 1
created: 2026-10-06
---

# Feature 126 — ReadOnly resolver: read tools never create project rows (create-free resolve path)

## Goal

Read-only MCP tools must never create rows. Today `ResolveProjectIdAsync` (and the shared resolver path it rides) falls through to the onboard resolve, which CREATES a project row when the cwd matches nothing — so a read like `read_project_manifest` against a directory that is not a registered project silently inserts a project. The ReadOnly contract on those tools lies.

## Requirements

- A create-free resolve path for read-only tools: explicit `create: false` semantics on the resolver (or a diskPath lookup that returns not-found instead of inserting).
- Read-only tools (read_project_manifest, and every tool whose descriptor says ReadOnly) use the create-free path; a non-match returns the honest not-found/ guidance error instead of a new row.
- Pin: a read tool against an unregistered cwd leaves the projects table unchanged (count before == count after).
- Mutating/onboard paths keep the create behavior.

## Non-goals

- Changing the onboard tool's create-on-miss (that is its job).
- Claim/lock semantics on non-existent projects (not-found errors stay not-found).

<log>
- 2026-10-06 [agent] — Van by claude claimed the feature
- 2026-10-06 [auto] — artifact created → ready
- 2026-10-06 [auto] — task 01 taken → inProgress
- 2026-10-06 [auto] — all tasks done + spec + plan validated → review
- 2026-10-06 [agent] — by user request in-session — armer notification 57fa59bc; result validated in the final sitting. Read tools never mint project rows: onboard create flag, 3 read doors create-free via the consolidated ProjectResolver, mutating doors pinned. Commits af6d052 / d39cb54 / 0db6dc9, gate 2253/0, spec sweep 8/8; recorded deviation T2 AC-3 (unticked per letter).
- 2026-10-06 [agent] — done via agent review by Van (claude), approved by human tap 2026-10-06
- 2026-10-06 [agent] — claim released on done-close
</log>
