---
feature: 051
task: 01
slug: entries-materialization
status: done
review: light
created: 2026-10-04
brief: Materializer: ENTRIES/ write + remove + tombstone files + strategy + tests
---

# Task 01 — Materializer: ENTRIES write + remove paths

<goal>
Entries hit the disk: one entry, one file, strategy-respecting.
</goal>

<scope>
- ENTRIES/{type}/{kebab(title)}-{id8}.md (frontmatter id/type/title/tags/createdAt/updatedAt + body); idempotent passes (byte-identical no-ops).
- Remove on the next pass when the entry is gone (board = truth); tombstone entries materialize like any entry (file appears on 029 conversion).
- Rides the per-project strategy (off/on/archive).
- Tests: write shape, idempotency, remove path, tombstone file, strategy off skips.
</scope>

<acceptance-criteria>
- [x] File shape + frontmatter pinned; idempotent re-pass — pinned.
- [x] Remove path works (file gone when entry deleted) — pinned.
- [x] Tombstone entry produces its file — pinned.
</acceptance-criteria>

<refs>
- Spec a87863c1; plan 46b7e368 (task 1); 029 entity.
</refs>

<log>
- 2026-10-05 [agent] — [agent] Claimed by Van (claude), taken by orchestrator — dispatching dotnet-coder (sonnet). Builds on 029's Tombstone entity (files appear on conversion, per grooming).
- 2026-10-05 [agent] — Coding round complete (dotnet-coder, sonnet; gates re-verified by orchestrator — build 0w, Infra 644 (+5), Api 631, format clean; checkpoint 919fab4): entry plans append into the SyncAsync plans list BEFORE plannedPaths capture — clobber-guards, remove-path, atomic write, 074 report and the 104 async queue all work unchanged (no separate phase hooks needed; reports are path-based). Tombstone conversion lands its file via plain type=tombstone frontmatter; stale entry rows prune files + empty type dirs. Declared for reviewer: entry mutations do NOT trigger a pass (file appears on next sync — per task scope); skill entries get a file in ENTRIES/skill/ alongside the untouched SkillFile materialization; TargetDir still reports BACKLOG for on (report contract not redesigned). Dispatching light review.
- 2026-10-05 [agent] — [agent] Light review PASS-WITH-NITS (audits/review-051-01.md; gates re-run: build 0w, Infra 644, Api 631, format clean). All interactions verified clean: entry files tracked in PlanningMaterializedFile (FeatureId null), hand-edited file refuses the pass per 033, CRLF normalizes through ContentHash (no refuse loop), id8 collision fails closed via DuplicateTarget, stale-row removes only hash-verified files, on↔archive migrates via the other-root dance, strategy-off clear sweeps only owned files. W1 (frontmatter sanitization — newline in title breaks the file structurally, board=truth so no data loss) + N1 (archive pin) + N2 (rename pin) → merged into task 02 scope. All 3 AC ticked. Closing task.
</log>
