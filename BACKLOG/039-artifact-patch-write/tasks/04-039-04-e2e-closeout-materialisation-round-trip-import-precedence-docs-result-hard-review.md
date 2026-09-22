---
feature: 039
task: 04
slug: artifact-patch-write
status: done
review: hard
created: 2026-09-22
brief: E2E: patch → materialised file reflects it, clobber-guard intact, import round-trip keeps patched state; docs + result; hard review gate
---

# 039/04 — E2E closeout: materialisation round-trip, import precedence, docs, result (hard review)

<goal>
Prove the whole path on a real project state: a patch flows through the service into the materialised `BACKLOG/` file, clobber-guard semantics survive, the importer's disk-wins precedence does not roll a patched body or its version back. Write the docs and the result artifact. This task is the feature's hard-review gate.
</goal>

<context>
Tasks 01–03 delivered parser, domain/service and surface. Spec D8 keeps materialisation semantics untouched — but "untouched" must be proven, not assumed, because patches change bodies outside the upsert path for the first time. The importer is disk-wins on frontmatter (034 precedent), so an old materialised file re-imported after a patch could silently revert a body — the plan calls this out as the version-jump risk.
</context>

<scope>
- E2E (test or scripted live run against an isolated host — scratch `OVERMIND_HOME` + `Overmind__BindUrl=http://127.0.0.1:<port>` + `--no-tray`, kill by port PID; NEVER from the repo working tree): create feature + task artifact → `replace_section` via the agent endpoint → materialised `tasks/NN-*.md` on disk contains the patched section and the untouched rest; `tick_ac` → checkbox flipped in the file; `planning_append_log` after patch → version in frontmatter bumped (D4).
- Clobber-guard regression: local edit to a managed file + patch → same 409/force behaviour as before (033 semantics unchanged).
- Import round-trip: re-import the materialised folder after a patch → body and version on the board match the patched state (no rollback).
- Full `dotnet build` + `dotnet test` solution-wide.
- Docs: write-cost model (three tiers) into `CONTEXT.md` and `STRUCTURE.md`; patch tool into the MCP tool inventory section if one exists there.
- Result artifact on the feature (what shipped, evidence paths, deviations).
- Hard review (architect-review) — the feature does not close without it.
</scope>

<anti-scope>
- No production deploy — that stays a user decision (pack + deploy flow is outside 039).
- No new features or refactors beyond fixes the review demands.
- No web UI work.
</anti-scope>

<constraints>
- Isolated host for live checks (scratch home, dedicated port, PID kill) — the running production host on 4761 is never touched.
- Evidence-first result artifact: commands, outputs, file paths.
</constraints>

<acceptance-criteria>
- E2E green: patch → disk reflects it; tick → checkbox in file; append → version bump visible.
- Clobber-guard and import round-trip checks pass.
- Solution build + tests green.
- Docs updated; result artifact written; hard review PASS.
</acceptance-criteria>

<refs>
- Spec 039 D8, R6.
- 033-materializer-clobber-guard (guard semantics), 034 (disk-wins precedent), 015 (isolated-host pattern).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-22 — Execution starts: coder takes the e2e closeout — materialisation round-trip on an isolated host, import precedence, docs, result artifact. Hard review follows the coding pass.
- 2026-09-22 — Hard review PASS-WITH-NITS (audits/architect-review-2026-09-22-039.md). IMPORTANT-1 closed in-tree by the fix round: PatchSection now rejects replace_section on <log> before any mutation (PlanningFailureCode.PatchLogSectionNotReplaceable, redirect message to planning_append_log), 7 new pins, 1109/1109. IMPORTANT-2 → microbatch #38. Feature assembled.
- 2026-09-22 — Hard review PASS-WITH-NITS (0 CRITICAL): structure clean, D1-D9/R1-R7 CONFIRMED, all four plan risks closed by e2e evidence. IMPORTANT-1 (append-only log guard) fixed in-tree post-review; IMPORTANT-2 (patch-shape union factories) parked as microbatch #38.
- 2026-09-22 — E2e closeout accepted: live-host evidence (patch→disk byte-precise, tick→file, version snapshots, clobber-guard intact, import round-trip no-rollback, disk-wins precedence), docs landed, result artifact on the board. All four tasks of 039 done.
</log>
