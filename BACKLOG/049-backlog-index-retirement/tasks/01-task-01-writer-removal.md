---
feature: 049
task: 01
slug: backlog-index-retirement
status: todo
review: light
created: 2026-10-06
brief: Remove the BACKLOG.md writer: BuildBacklogPlan + plans.Add + HashTrackedDiskCopyAsync branch in PlanningMaterializer.cs; trim ManagedBlock Render/Merge/StatusMarker; legacy cleanup branches stay byte-exact.
---

# Task 01 — Writer removal

<goal>
The materializer stops planning BACKLOG.md: the generated-index writer is removed, the legacy cleanup machinery stays byte-exact.
</goal>

<scope>
- `src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs`: delete `plans.Add(BuildBacklogPlan(root, features));` (~line 74), the whole `BuildBacklogPlan` method (~527–538), and the BACKLOG.md branch in `HashTrackedDiskCopyAsync` (~665–675 — the method collapses to plain byte hashing of planned files).
- `src/Overmind.Infrastructure/Planning/ManagedBlock.cs`: delete `Render`, `Merge`, `StatusMarker` (~lines 10–42 and 105–111). KEEP `StartMarker`, `EndMarker`, `TryExtract`, `Remove`.
- KEEP untouched: the `BacklogIndexRelativePath` const (line 12) and the block-hash branches in `DetectDriftAsync` (~187), `InspectTrackedCopyAsync` (~313), `RemoveTrackedCopyAsync` (~355), the `ClearAsync` strip path, and `AgentFileConflictScanner.cs` (~61).
- Awareness note: after removal `FilePlan.Sha256` is always the content hash (the only `with { Sha256 = ... }` override dies with `BuildBacklogPlan`) — no further code change needed.
</scope>

<anti-scope>
Any behavior change to guards, drift detection, clear, or archive paths; the importer; the onboard block writer; `AgentFileConflictScanner`; any test file (task 02 owns tests).
</anti-scope>

<constraints>
No comments for obvious code; match the file's existing style; `dotnet build` green before handoff.
</constraints>

<acceptance-criteria>
- [ ] `dotnet build` green.
- [ ] `grep -rn "ManagedBlock.Render\|ManagedBlock.Merge" src/` returns nothing.
- [ ] git diff shows deletions only in the listed regions; all legacy cleanup branches (Detect/Inspect/Remove/Clear special cases) present and unchanged.
</acceptance-criteria>

<refs>
spec cc4c4392-566e-4ff3-a84a-ff6aa61914fd; plan cf343934-d939-482d-9dd2-36dc8e41287b step 1; 033-materializer-clobber-guard (the semantics that must survive untouched).
</refs>

<obstacles>
None at creation.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan step 1
</log>