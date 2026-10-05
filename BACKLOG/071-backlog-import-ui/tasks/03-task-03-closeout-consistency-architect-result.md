---
feature: 071
task: 03
slug: backlog-import-ui
status: done
review: light
created: 2026-10-04
depends_on: 02
brief: Closeout: door consistency, architect light, result + click-check
---

# Task 03 — Closeout: consistency + architect + result

<goal>
Close the wave: both doors share the importer, architect light, result.
</goal>

<scope>
- Consistency pass: agent door vs human door over the same BacklogImporter.
- Architect light; result with post-release click-check (scan a real project, import one folder, verify the board + mirror).
</scope>

<acceptance-criteria>
- [x] Consistency recorded; architect verdict recorded; result drafted with click-check.
</acceptance-criteria>

<refs>
- Spec d46a2a7c; plan 2859ccfb (task 3).
</refs>

<log>
- 2026-10-04 [agent] — [agent] Taken by orchestrator (tasks 01+02 done). Consistency pass recorded: both doors share the single DI-registered BacklogImporter — human door PlanningImportApi (BacklogFeatureScanner.ScanAsync for scan + ImportFeatureAsync for import), agent door AgentPlanningApi:186-190 (ParseFeatureAsync + ImportFeatureAsync, same instance); scan stays human-only per spec. Dispatching architect light (web gates + static only — dotnet build is held by the parallel 104/02 coder on the same solution).
- 2026-10-05 [agent] — [agent] Closeout complete: consistency pass recorded (one DI importer behind both doors, scan human-only), architect light APPROVED-WITH-WARNINGS with W1 fixed in-tree (MaterializationErrors.Map extract, f67669f) and N2 fixed (8d89544); result 2444a25f drafted with the post-release click-check. AC ticked. Closing task.
</log>
