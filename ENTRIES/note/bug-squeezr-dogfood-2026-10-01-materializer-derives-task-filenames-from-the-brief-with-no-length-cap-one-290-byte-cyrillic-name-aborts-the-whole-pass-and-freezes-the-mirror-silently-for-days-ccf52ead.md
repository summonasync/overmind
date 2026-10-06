---
id: ccf52ead-acf5-4e1a-83b9-c6b18ea9da8b
type: note
title: Bug (Squeezr dogfood, 2026-10-01): materializer derives task filenames from the brief with no length cap — one 290-byte Cyrillic name aborts the whole pass and freezes the mirror silently for days
tags: bug, cross-project, dogfood, materialization, squeezr
createdAt: 2026-10-01T20:50:13Z
updatedAt: 2026-10-01T20:50:13Z
---

Reported 2026-10-01 by the Qwen Code agent working in the **Squeezr** project (`31f33e30-6056-40a3-8447-c43d889fee9f`). Read-only session w.r.t. Overmind code — nothing below was applied, all fixes are suggestions.

## Symptom

`overmind_onboard` for Squeezr returns:

```
Materialization: strategy=on, lastSuccess=2026-09-28T11:35:49.56, lastAttempt=<now> (failed: File operation failed for
'.../BACKLOG/063-dogfood2-ignore-recall-suggest/tasks/01-ядро-исключений-рукописный-gitignore-матчер-подмножества-ignoreruleset-со-слоями-defaults-config-squeezrignore-gitignore-каскад-last-match-wins-respectgitignore-true-в-indexingconfig-потребителей-не-трогаем.md': The path '...tasks/01-ядро-исключений-...-со-с)
```

The reason string is itself truncated mid-path.

## Root cause

The task filename is built from the artifact `brief` verbatim (slugified, order-prefixed). For the artifact above the single path component is **290 bytes UTF-8** (212 chars, Cyrillic = 2 bytes/char) against a **255-byte per-component limit** on ext4 → ENAMETOOLONG. Two separate problems ride along:

1. **No cap/clamp on the generated name.** 076 fixed the opposite direction (title slug dropped from filenames, `01-task-01.md`); the pendulum now swings to unbounded names. Latin-brief projects stay under 255 by luck, Cyrillic/CJK briefs hit it at roughly 127 chars.
2. **One unwritable file aborts the pass instead of being refused per file.** `planning_materialize_status` for this run: `atomic: false`, `written: []`, `skipped: 288`, `refused: []` — 063-01 was neither written nor refused, it threw. Feature 067 already asks for "skip-not-abort" in the import×materialization path; this is the same failure mode on the ordinary event-driven path, so the contract seems not to hold there.

## Damage observed in Squeezr

- The pass has been failing on **every** planning mutation since 2026-09-30T22:30 (each of my 10 `planning_patch_artifact` calls today re-triggered it and re-failed it), and `lastSuccess` is stuck at 2026-09-28T11:35.
- `BACKLOG/063-dogfood2-ignore-recall-suggest/tasks/` is an **empty directory**: all 12 task artifacts of a live p0 feature have never existed on disk, while `feature.md`/`spec.md`/`plan.md` are there. `git ls-files` confirms the tasks were never tracked — so agents without MCP access (and the README'd "read-only mirror for agents without Overmind") see a feature with no tasks at all.
- `stalePaths`: `BACKLOG.md`, `BACKLOG/055-watch-embeddings-cpu/tasks/03-*.md`, `BACKLOG/057-doc-kind-filter/feature.md` — drift the aborted pass never reconciles.
- Nothing surfaces this to the human: the failure rides only inside the onboard payload, so the mirror looked "up to date" from the repo side. 054 (silent-off) was closed, but the *reporting channel* still hides a persistently failing pass; a second consecutive failure with no successful pass in between arguably deserves a louder signal than a field in the onboard blob.

## Suggested fixes (not applied)

- Clamp the generated filename to the filesystem limit: budget the order prefix + `.md`, truncate the slug, keep determinism (truncate-then-dedupe on collision rather than throwing).
- Fail per file: a path that cannot be written becomes a `refused` entry with a reason, the rest of the plan still lands atomically for the writable set.
- Consider deriving filenames from a short human key (feature slug + order + first N chars) and keeping the full `brief` inside the file only.
- Surface `lastAttempt` failures that repeat across N runs (the board already stores lastSuccess/lastAttempt from 074 — the data for this exists, the alert doesn't).

## Workaround used in this session

None applied on the board: shortening the `brief` of a **done** task artifact is a mutation of closed history, so it was escalated to the human instead. Until it is shortened, every planning write to Squeezr keeps failing the mirror pass.