---
feature: 073
task: 02
slug: project-manifest
status: done
review: light
created: 2026-10-04
depends_on: 01
brief: Web: markdown editor + import button on project config + tests
---

# Task 02 — Web: editor + import button

<goal>
The human edits the manifest on the config page.
</goal>

<scope>
- Project config: markdown textarea editor + save through the existing update mutation; «Import from agent file» button (shows which file was found / none); manifest absent state.
- Tests: editor save flow, import button states.
</scope>

<acceptance-criteria>
- [x] Editor + save pinned; import button states (found/not-found) pinned.
- [x] Gates green (lint/tsc/node test).
</acceptance-criteria>

<refs>
- Spec 722d3acd; plan c6958bdf (task 2).
</refs>

<log>
- 2026-10-05 [agent] — [agent] Taken by orchestrator (task 01 done). Dispatching web-coder (sonnet) — the update contract (setManifest semantics) and the import endpoint response are already in place from task 01.
- 2026-10-05 [agent] — Web coding round complete (web-coder, sonnet; gates re-verified by orchestrator — lint 0e/6w pre-existing, tsc 0, tests 162 (+12), build green; checkpoint 62bbcdf): manifest section in the project detail modal (mono textarea + Save via setManifest=true, empty draft honestly clears), Import button with found/not-found/limit states (role=status/alert), honest absent state, useProjectManifestImport mutation (callback style per 071 precedent), updateProject sends manifest/setManifest only when setManifest=true (legacy-safe for other update callers). Declared nuance for reviewer: draft syncs by VALUE of project.manifest (refetch-on-focus never wipes unsaved text). Dispatching light review.
- 2026-10-05 [agent] — [agent] Light review PASS-WITH-NITS (audits/review-073-02.md; gates re-run: lint 0e/6w pre-existing, tsc 0, tests 162, build green). W1 fixed in-tree (869aaf5): manifest section keyed by project id — draft cross-project leak hardened (was unreachable today, one-word hardening; source-pin updated in the same commit). Legacy-safety verified by reviewer across ALL update call sites (projects-page form omits setManifest → server preserves; app-sidebar create only; manifest section by design). S1 (Saved. span without role=status) noted as precedent-consistent. Both AC ticked. Closing task.
</log>
