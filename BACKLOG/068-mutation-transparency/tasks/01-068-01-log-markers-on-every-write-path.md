---
feature: 068
task: 01
slug: mutation-transparency
status: done
review: light
created: 2026-09-26
brief: Markers on every <log> write path: AppendLog/patch/upsert logNote lines carry the door source (MCP→[agent], HTTP→[ui])
---

# 068/01 — Log markers on every write path

<goal>
No write path may emit an unmarked <log> line anymore (spec R1 / OM-11). PlanningLog.Append/Parse already carry PlanningLogSource (043) with legacy tolerance — route the missing call sites through the source param; no format change.
</goal>

<scope>
- Core: PlanningArtifact.AppendLog gains a PlanningLogSource parameter (src/Overmind.Core/Entities/PlanningArtifact.cs).
- Infrastructure: AppendLogAsync routes the door source; patch logNote and upsert logNote lines stamped by door (src/Overmind.Infrastructure/Planning/PlanningArtifactService.cs).
- MCP adapter: planning_append_log / planning_patch_artifact / planning_upsert_artifact pass Agent (src/Overmind.Mcp — locate the planning tools file).
- HTTP artifacts API doors pass Ui.
- [auto]/[import] lines and legacy unmarked-line parsing unchanged (043 contract holds).
</scope>

<anti-scope>
- No response-shape changes (task 02), no demotion logic (task 03), no resolver work (task 04).
</anti-scope>

<constraints>
- .NET 10 / C# 14; no comments; English-only; no git commit/push; dotnet build + dotnet test green before close.
</constraints>

<acceptance-criteria>
- [x] MCP append_log writes "- YYYY-MM-DD [agent] — note" (Mcp test pin)
- [x] HTTP append writes [ui] (Api test pin)
- [x] patch with logNote → door marker in the appended line; same for upsert logNote
- [x] legacy unmarked lines still parse with Source null (existing Core tests stay green)
- [x] dotnet build 0 warnings + dotnet test green
</acceptance-criteria>

<refs>
- Spec 9f1fa31e R1; 043-status-provenance marker channel; microbatch #45 absorbed here.
</refs>

<obstacles>
- Wire-declared source is restricted to agent|ui (PlanningDoorSource.Resolve); Auto/Import stay system-reserved. The agent-door fallback guarantees [agent] even if the MCP payload field is dropped, so the payload declaration is belt-and-suspenders, not load-bearing.
- POST .../artifacts/tasks (CreateTasks) and POST .../artifacts/{id}/patch are mapped only from /api/agent — their door fallback is hardcoded Agent; the plain /api surface has no patch/tasks routes.
- PlanningFeatureService import path (UpdateBody "re-imported from ...") left unmarked per scope ("[auto]/[import] lines unchanged", 043 legacy tolerance) — flagging in case a later task wants Import there.
- Repo-wide dotnet format --verify-no-changes is red on pre-existing EF-migration files (block-scoped namespaces, auto-generated); verified format-clean only on the touched files.
- The running server predates this change: live planning_append_log still writes unmarked lines until the fix is deployed (the implementation log line below is unmarked for exactly that reason).
</obstacles>

<log />

<log>
- 2026-09-26 [agent] — [agent] execution started: routing AppendLog/patch/upsert logNote lines through PlanningLogSource per door (MCP→agent, HTTP→ui)
- 2026-09-26 — 068/01 implemented. Core: PlanningArtifact.AppendLog/UpdateBody/PatchSection/TickAc (+ApplyBodyMutation) gained PlanningLogSource? and route it into PlanningLog.Append. Infrastructure: AppendLogAsync/PatchAsync/UpsertAsync/UpsertTasksAsync take the door source (optional, ct-last) and stamp logNote lines; StageTask threads it. Host: new PlanningDoorSource.Resolve(stated, fallback) — wire-declared source limited to agent|ui, fallback by route door; CreateArtifact/AppendLog split into per-door entries over shared ForDoor cores (plain→Ui, /api/agent→Agent); CreateTasks falls back Agent (agent-door-only route); UpdateArtifact (PUT) falls back Ui; request DTOs gained optional Source. MCP: planning_append_log / planning_patch_artifact / planning_upsert_artifact payloads carry source="agent" (const DoorSource). Tests: Core AppendLog_WithSource_AppendsSourcedLine; Infra AppendLogAsync/PatchAsync/UpsertAsync door-stamp pins; Api PlanningApiTests [ui] pins (AppendLog_HttpDoor_WritesTheUiMarker, CreateArtifact_WithLogNote_WritesTheUiMarker) + AgentPlanningApiTests [agent] pins (Upsert/AppendLog/Patch) + AgentLogUrl helper; Mcp payload pins source=agent incl. upsert key-set ["kind","body","order","review","logNote","brief","source"]. Evidence: dotnet build 0 warnings; dotnet test 1450 passed / 0 failed (Core 213, Infrastructure 473, Api 419, Mcp 345); dotnet format --verify-no-changes clean on all touched files.
- 2026-09-26 [agent] — 068/01 done: all 5 AC ticked. Every <log> write path now carries its door marker — Core AppendLog/UpdateBody/PatchSection/TickAc take PlanningLogSource; service AppendLog/Patch/Upsert/UpsertTasks stamp logNote lines; MCP tools declare source=agent; HTTP doors resolve via PlanningDoorSource (plain→Ui, /api/agent→Agent). Evidence: dotnet build 0 warnings; dotnet test 1450 passed / 0 failed (Core 213, Infrastructure 473, Api 419, Mcp 345); new contract pins in Core/Infra/Api/Mcp suites; format clean on touched files. Review label: light.
- 2026-09-26 — Fix-round for review PASS-WITH-NITS (audits/review-068-01.md): W1 — PlanningFeatureService re-import path now threads PlanningLogSource.Import into UpdateBody, the "re-imported from {path}" line reads [import] (format unchanged; MatchesImportedBody unaffected — it matches by Text without the marker); pin added to Import_ReRunAfterEdit_UpdatesWithNoteAndStaysStable (ShouldContain "[import] — re-imported from "). W2 — three PlanningDoorSource pins in PlanningApiTests via the plain-door log endpoint: declared "agent" honored, declared "ui" honored, reserved "auto"/garbage "banana" fall back to the door default ([ui], no [auto]/[import] leakage). Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1453 passed / 0 failed (Core 213, Infrastructure 473, Api 422, Mcp 345); dotnet format --verify-no-changes clean on the three touched files.
</log>
