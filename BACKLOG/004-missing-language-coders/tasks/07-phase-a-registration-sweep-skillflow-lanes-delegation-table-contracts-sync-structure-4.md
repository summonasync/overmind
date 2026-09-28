---
feature: 004
task: 07
slug: missing-language-coders
status: done
review: light
created: 2026-09-27
brief: Phase A registration sweep: SKILLFLOW lanes, delegation table, contracts, SYNC-STRUCTURE ×4
---

<goal>Registration sweep: wire the new lanes into SKILLFLOW, feature-orchestrator, contract files, SYNC-STRUCTURE — on all four sides.</goal>
<scope>- SKILLFLOW.md ×4: web/node/platform lanes routing table (from spec §Resolved domain model, incl. monorepo rule); review rows updated (web-code-reviewer etc.); delegation lines consistent.
- feature-orchestrator SKILL.md ×4 Phase 2: full delegation table — .NET → dotnet-coder, web → web-coder, node → node-coder, Electron/RN → js-platform-coder, MAUI → maui-coder, docs → documentation-writer, learning → tutor-master.
- Contract files ×4 (AGENTS.md/CLAUDE.md/GEMINI.md/QWEN.md): crew skill/agent mentions updated where lanes are listed.
- SYNC-STRUCTURE.md (root): file-mapping rows for 8 new agents + 11 new skills (incl. 3 bp); count-parity text 23/26 → 28/34; frontmatter-affected set additions if any new quoted descriptions.
- manifest.json: verify dir-mirroring covers new dirs automatically; edit only if per-entry surprises appear.</scope>
<anti-scope>No content changes to the new skills themselves; no BACKLOG//audits/.</anti-scope>
<constraints>Quad-edit consistency; English-only; NO git commit.</constraints>
<acceptance-criteria>
- [x] SKILLFLOW ×4 contain the lanes table; feature-orchestrator Phase 2 names all five coder lanes ×4
- [x] SYNC-STRUCTURE parity text 28/34; mapping rows complete; contamination greps empty; counts 28/34 ×4
</acceptance-criteria>
<refs>Spec §Phase A item 9 + domain model; Plan T7; blocked by T1–T6.</refs>
<obstacles></obstacles>
<log>
- 2026-09-28 [agent] — взята в работу исполнителем
- 2026-09-28 [agent] — body normalized in a full upsert: newline added right after the acceptance-criteria opening tag (patch could not see AC items without it) and the log section duplicated by the first status move was collapsed back into one section
- 2026-09-28 [agent] — normalize quirk artifacts: AC section newline + duplicated log collapsed; body content unchanged
- 2026-09-28 [agent] — quirk root-caused: the AC item parser only sees the list when BOTH tags sit on their own lines — the last item abutting the closing tag zeroes it (patch 400 "no acceptance-criteria items"; same zero-parse hit T4/T6/T16). Closing tag moved to its own line.
- 2026-09-28 [agent] — AC section normalization round 2: closing tag moved to its own line so the item parser sees the list
- 2026-09-28 [agent] — registration sweep executed ×4: SKILLFLOW lanes table (web/node/platform + monorepo rule) + Implement/Review rows naming node/js-platform coders and reviewers; feature-orchestrator Phase 2 delegation table grown to 7 lanes + Phase 3 ALWAYS-run review lines + Conditional Review Triggers rows for node/js-platform (spec cross-cutting rule "delegation lists and routing lines"); SYNC-STRUCTURE 13 mapping rows (5 agents + 8 skills, alphabetical), frontmatter-affected set 8→11 agents, count parity 23/26→28/34; contract files verified — no coder-lane listings exist in any of the four, nothing to update; manifest.json verified — whole-dir mirror/merge entries, new dirs covered automatically. Verify: counts 28/34 ×4, mapping rows complete vs disk, next-js- residue 0, all 5 contamination greps empty, frontmatter YAML grep empty, install/uninstall dry-runs clean (13 new-lane deploy lines). Both AC ticked.
- 2026-09-28 [agent] — registration sweep complete on all four sides, both AC verified and ticked — на ревью (light per task flag)
- 2026-09-28 [agent] — Orchestrator review passed: lanes table ×4 in SKILLFLOW, delegation table 7 lanes ×4 in feature-orchestrator, SYNC-STRUCTURE 13 rows + parity 28/34, contracts/manifest verify-only with justification, contamination greps empty, dry runs clean. Root-cause AC-parser quirk documented (both tags must sit on their own lines). Executor's out-of-scope observations verified as REAL 003 residue → routed to new task 18: (a) sprint-planner route exists only in kimi SKILLFLOW (003-T4 wrongly assumed registration was root-wide), (b) contract files ×4 still claim «no BACKLOG.md index» (003-T9 fixed only the ceremony skill). Its BACKLOG.md-cancellation inference was wrong — the file is a legitimate materialized index per 003/T9; only the stale sentence is the bug.
</log>