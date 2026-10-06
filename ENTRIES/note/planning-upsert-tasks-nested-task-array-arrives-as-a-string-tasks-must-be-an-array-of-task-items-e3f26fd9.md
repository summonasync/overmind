---
id: e3f26fd9-bcd8-4536-8913-626fb2e4d1ae
type: note
title: planning_upsert_tasks: nested task array arrives as a string → "tasks must be an array of task items"
tags: bug-report, mcp, planning_upsert_tasks, wowmeme generator
createdAt: 2026-09-30T15:38:45Z
updatedAt: 2026-09-30T15:38:45Z
---

Observed 2026-09-30 while decomposing feature `001-vendor-feasibility-probes` (project WoWMemeGenerator, 6 task items in one batch).

Symptom: `planning_upsert_tasks` fails with `400 / "tasks must be an array of task items"` even though the argument is a well-formed JSON array of objects. The error payload echoes the call with `tasks` arriving as a **stringified** array (`"tasks":"[{\"order\":1,…}]"`), i.e. the object-array parameter is serialized to a JSON string somewhere between the model and the MCP tool boundary, and the server's schema check rejects it. Reproduced twice with the same shape (two different payload sizes, ~14k and ~10k chars); a separate batch retry was not attempted with fewer items.

Not this bug: the previously logged Overmind quirk where batch-created task bodies register only some XML sections. Here nothing gets created at all — the call fails before staging.

Workaround that worked: per-task `planning_upsert_artifact(kind=task, order=N, brief=…, review=light, blockedBy=[…])`. A flat integer array (`blockedBy: [1,3,4,5]`) passes fine in the same runtime, and all eight XML sections registered — verified afterwards with `planning_read_artifact(sections="obstacles,refs,acceptance-criteria")` and `planning_list_artifacts` (blocker edges shown as `blocked: true, blockedBy: […]`). Cost: 6 calls instead of 1, no atomicity.

Ask: either accept a JSON-encoded string for `tasks` (parse-then-validate server-side), or surface the type mismatch as a schema-repair hint to the client instead of a plain 400. Worth checking whether the regression is in the MCP adapter's argument coercion for arrays-of-objects rather than in the Overmind API itself — every other array parameter in this session worked.
