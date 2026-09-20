---
feature: 021
slug: task-brief
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 021 — Task brief description visible in the tasks table

<outcome>
021-task-brief shipped: task artifacts carry a typed optional Brief (single line, max 500) through every door — Host API (create/update/response + FluentValidation), MCP planning_upsert_artifact (brief param, posted only when provided), the disk round-trip (structural frontmatter key on materialized task files, typed restore + diff on import), and the web UI (muted multi-line brief on kanban cards, Brief input in TaskEditor on create + edit).

Semantics as specced: task-kind only (400 BriefNotAllowedForKind elsewhere), update null=untouched / ""=clear, no body versioning for brief-only changes. Suite 770 → 794 (+24 tests). All three tasks reviewed: 01 PASS-WITH-NITS (4 NITs, deviation accepted), 02 PASS-WITH-NITS (1 IMPORTANT fixed in-tree: multi-line brief rejected at the validators — frontmatter injection vector closed; 4 NITs → microbatch), 03 PASS (0 blockers, 3 NITs → microbatch).

Deviations kept deliberately: UpsertAsync skips UpdateBody when body unchanged and logNote empty (kills junk version rows on identical re-posts; the old behavior was worse and unpinned); importer reads brief via Raw not Token (free text survives); Restore takes an optional brief parameter instead of a post-restore SetBrief (preserves UpdatedAt == CreatedAt on import).
</outcome>

<evidence>
- dotnet build 0 warnings / 0 errors; dotnet test 794/794 (Core 106, Infra 274, Api 211, Mcp 203) — includes 24 new tests across the three tasks.
- Round-trip pinned by tests: briefed task survives materialize → wipe → re-import as a typed field with an unknown frontmatter key (`owner`) routing to preserved-JSON while `brief` does not; old task files without the key import null with zero flagged changes; hand-edited brief on disk applies via SetBrief.
- Kind guard pinned on both doors (API + agent routes, same handler); update semantics pinned by 4 dedicated tests; brief-only edit leaves Version at 1 with an empty versions endpoint.
- Multi-line brief (\n, \r\n, \r) → 400 on create and update (4 tests) — the review-021-02 IMPORTANT closed in-tree.
- Web: lint 0 errors, tsc 0 errors, build green; untouched-input-never-clears verified by the reviewer through to the server normalization paths.
- Reviews: audits/review-021-01.md, audits/review-021-02.md, audits/review-021-03.md.
- Live UI visual pass (kanban chip rendering, TaskEditor flow) deferred to the user — the running 0.1.7 host predates the field; next pack (0.1.8) carries it.
</evidence>

<deferred>
- Microbatch queue additions: identical re-post now returns 201 instead of 200 (UpsertAsync created-vs-updated flag); whitespace-only brief stored as-is (not trimmed); import-door no-version-row assert missing; full byte-stability cycle (wipe → re-import → force re-sync) unpinned; hand-written brief: on doc artifacts silently swallowed (consistent with existing `validated` convention); leading/trailing whitespace in a stored brief causes one benign drift cycle; client 500-char guard checks raw value vs server post-trim (overblocks cosmetically); add maxLength attribute to the Brief input.
- UI visual dogfood on a packed release pending 0.1.8.
</deferred>
