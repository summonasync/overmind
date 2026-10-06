---
id: 796b614b-b6e4-45a8-9f1f-503a7213fa15
type: note
title: planning_update_feature demands a log prefix that claim/auto-transition wrote out of band
tags: agent-api, append-only, log, planning, reported-by-qwen, worktree
createdAt: 2026-10-04T16:51:43Z
updatedAt: 2026-10-04T16:51:43Z
---

# Feature-body resend fails on a log prefix the agent could not have known

**Date:** 2026-10-04. **Reporter:** Qwen Code (vendor `qwen`), session in project **Slurpy.AI**
(`78df983a-83c3-4a8f-9977-496ec03bc1a8`), feature `eslint-10-followups`.

## What happened

`planning_update_feature` failed on a feature whose stored `<log>` was empty when read:

```
overmind API returned 400 (POST /api/agent/projects/{id}/planning/features/{slug}/update):
The log section is append-only: a resent body must carry the stored log lines as an exact ordered
prefix, but log line 0 is the first to diverge (stored 1 lines, incoming 0). Append via
planning_append_log, edit other sections via planning_patch_artifact, or resend the full body with
every stored log line untouched and new lines only at the end.
```

Live sequence:

1. `planning_read_feature(includeArtifacts=false)` — body ends at `## Notes`, no `<log>` block, `version: 1`.
2. `planning_claim_feature(alias=Qwen, vendor=qwen)` — appended `- 2026-10-04 [agent] — Qwen by qwen claimed the feature`.
3. `planning_upsert_artifact(kind=spec)` — appended `- 2026-10-04 [auto] — artifact created -> inProgress`.
4. `planning_update_feature(body)` with the step-1 body — the 400 above.

The claim and the auto-transition write feature log lines **out of band**: neither
`planning_claim_feature` (returns only the claim payload) nor `planning_upsert_artifact` (returns
only the artifact payload) tells the caller what it appended to the feature.

## Why it matters

- Any agent following the documented ceremony order (claim -> spec -> touch the body) hits this
  every time. That is the happy path, not an edge case.
- The 400 quotes counts (`stored 1 lines, incoming 0`) but not the stored line text, so the error by
  itself is unrecoverable — the client must re-read first. Compare the `acceptance-criteria` 400 in
  `planning_patch_artifact`, which does dump the visible list with real indices.
- `planning_append_log` is no escape for a feature body: its aliases are `<slug>/S`, `<slug>/P`,
  `<slug>/R`, `<slug>/T<n>` — nothing addresses the feature itself.
- The exactness is what kept this safe (a stale resend could silently drop a line the agent never
  saw). The check is right; the ergonomics are not.

## Asks (any one closes it)

1. A body **without** a `<log>` section means "leave the log untouched" (body-only patch), not "log is empty, diverge at line 0".
2. Or include the stored log lines in the 400 diagnostics.
3. Or have `planning_claim_feature` / auto-transitions return the feature log line they appended.
4. Or accept the bare `<slug>` (or `<slug>/F`) as an alias in `planning_append_log`, so a feature log line can be added without resending the body.

## Secondary, same session: a worktree reads a frozen mirror

`overmind_onboard` reports `targetDir=/mnt/Ubuntu-Dev/Projects/Slurpy.AI/BACKLOG`, and the pass
after the spec create wrote `BACKLOG/eslint-10-followups/feature.md`, `spec.md`, `BACKLOG.md` there.
The session runs in a git worktree of the same repo, which carries its own **committed** copy of
`BACKLOG/` from the branch point: `.../.mindlash/worktrees/eslint-10-followups/BACKLOG/eslint-10-followups/feature.md`
reads `status: draft` with no `<log>`, while the board is `inProgress` with two log lines.

So in a worktree the disk mirror is silently stale, and the append-only rule references lines that
live in a different directory than the one the agent is working in. Ask: flag non-target checkouts
in `planning_materialize_status` (or in onboard) as "mirror here is not live". Ask 1 also covers the
practical impact.
