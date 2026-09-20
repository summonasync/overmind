---
feature: 023
task: 02
slug: feature-lifecycle-flow
status: todo
review: light
created: 2026-09-20
---

# 023/02 — Auto-promotions (artifact create + last task done)

<goal>
Event-driven lifecycle promotions in PlanningArtifactService: creating any artifact on a draft/ready feature promotes it to inProgress; a task reaching Done with all sibling tasks Done (and spec+plan present) promotes the feature to review. Never auto-done, never a Version bump, importer untouched.
</goal>

<context>
Feature 023, backend half 2. Depends on task 01 (Review member + transitions). Hook points verified: UpsertAsync create branch (~71–76) and unique-retry branch (~89–92) already load the feature (~34–38); SetStatusAsync (110–135) loads only the artifact today; the all-done evaluation pattern lives in AgentPlanningApi.Current (119–124).
</context>

<scope>
- PlanningArtifactService.UpsertAsync: after a successful artifact CREATE (both branches), if feature.Status is Draft or Ready → feature.SetStatus(InProgress) in the same SaveChanges. No promotion on updates of existing artifacts.
- PlanningArtifactService.SetStatusAsync: after a task reaches Done, load sibling tasks + the feature; promote to Review when: changed task is Task kind AND Done AND at least one task exists AND all tasks Done AND a spec artifact exists AND a plan artifact exists. Feature must currently be InProgress or Ready for the promotion to fire (no resurrecting done; no yanking draft).
- Verify the importer does NOT route through SetStatusAsync (exploration says it writes statuses directly — confirm in code; if it does route through, guard with an internal promotion flag instead).
- No Version bumps (SetStatus semantics preserved); materializer sync unchanged.
- Tests: artifact create promotes draft→inProgress and ready→inProgress (both doors implicitly via service); update of existing artifact does NOT promote; task-done promotes to review only when all-done + spec/plan present (negatives: one task not done; missing plan); promotion skipped for done features; no version bump asserted.
</scope>

<anti-scope>
- No manual-transition changes (task 01 owns validation), no web, no MCP tools.
- No promotion on artifact DELETE.
</anti-scope>

<constraints>
- Same-save-changes promotion (no extra round-trip where avoidable); .NET 10 / C# 14; no comments; English-only.
- dotnet build + test green (record counts).
</constraints>

<acceptance-criteria>
- [ ] Create spec on a draft feature via the agent door → feature flips to inProgress (board reflects).
- [ ] Last task done (all done, spec+plan exist) → feature review.
- [ ] One task pending → stays inProgress; missing plan → stays inProgress.
- [ ] Editing artifacts of a done feature changes nothing.
- [ ] Feature Version unchanged by promotions.
</acceptance-criteria>

<refs>
- Spec: feature 023 — auto-transition decisions (event-driven, never done, importer bypass).
- Pattern refs: PlanningArtifactService.cs (UpsertAsync 24–94, SetStatusAsync 110–135, GetProjectIdAsync 191–197), AgentPlanningApi.cs:119–124 (all-done evaluation).
</refs>

<obstacles>
</obstacles>

<log>
</log>
