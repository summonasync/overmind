---
feature: 071
task: 02
slug: backlog-import-ui
status: done
review: light
created: 2026-10-04
depends_on: 01
brief: Web: settings section — scan, states, preview, per-folder import + tests
---

# Task 02 — Web UI on project settings

<goal>
Bring the import flow to the human: scan button + folder list + per-folder import on the project settings page.
</goal>

<scope>
- Section on project settings: Scan button, folder list with states (new / already imported / error note), expandable preview, per-folder Import button (confirm on re-import naming it), report lines after import.
- React Query per house idiom; web tests on list states and import interaction.
</scope>

<anti-scope>
- Bulk import-all; editing imported content.
</anti-scope>

<acceptance-criteria>
- [x] Folder states rendered and pinned; re-import confirm names the state.
- [x] Gates green (lint/tsc/node test).
</acceptance-criteria>

<refs>
- Spec d46a2a7c; plan 2859ccfb (task 2).
</refs>

<log>
- 2026-10-04 [agent] — [agent] Claimed by Van (claude), taken by orchestrator — dispatching web-coder (sonnet). Runs parallel to 104/01 review (disjoint pools: web vs dotnet gates).
- 2026-10-04 [agent] — Coding round complete (web-coder, sonnet): Backlog import section in the project detail modal (the settings surface of this app) — scan, folder state badges, expandable preview, per-folder import; re-import confirm = inline warning-strip instead of Modal (nested-modal Escape conflict, deviation #1, pinned by test); subPath control not built (out of AC, api accepts it). Gates: lint 0e/6 pre-existing, tsc 0, tests 134 (+21 new), build 12/12. Checkpoint a94963a. Dispatching light review (web-code-reviewer).
- 2026-10-04 [agent] — [agent] Light review PASS-WITH-NITS (report audits/review-071-02.md; gates re-run independently: lint 0e/6w pre-existing, tsc 0, tests 134/134, build 12/12). W1 fixed in-tree (f9f4b2e): session-local imported-folders set keeps the re-import confirm armed across the rescan lag; manual rescan drops a stale confirm strip; tests 138/138 (+4 pins). Deviation 1 (inline strip vs nested Modal Escape) upheld by reviewer — modal.tsx attaches window keydown per open Modal. N1 (parallel folder imports possible, local disable) accepted for v1; N2 (confirm strip not announced to screen readers) + N3 (no post-import focus move) → microbatch. Both AC ticked. Closing task.
</log>
