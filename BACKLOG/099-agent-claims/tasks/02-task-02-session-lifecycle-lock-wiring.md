---
feature: 099
task: 02
slug: agent-claims
status: inprogress
review: light
created: 2026-09-30
depends_on: 01
brief: X-Overmind-Session header, register/end endpoints, server-side touch, lock across 12 mutations, adapter start/EOF lifecycle
---

# Task 02 — Session lifecycle + lock wiring

<goal>
Wire sessions and the mutation lock end-to-end at the HTTP boundary: host derives liveness from the X-Overmind-Session header, the lock bites in the planning service layer, the adapter registers/touches/ends its session.
</goal>

<scope>
- Host: session register + end endpoints (AgentPlanningApi.cs or a sibling API file, per repo conventions).
- Host: header handling — X-Overmind-Session presence updates the session's LastSeenAt (server-side touch, no extra requests); absence = human/legacy → never locked.
- Lock enforcement where feature mutations funnel (PlanningFeatureService / PlanningPolicy): the 12 feature-scoped mutations (set_feature_status, update_feature, set_feature_priority, move_feature, add/remove feature dependency, upsert_artifact, upsert_tasks, patch_artifact, append_log, set_status, validate) → 409 naming the holder for non-holder sessions on a claimed feature. Reads untouched. Unclaimed features unchanged.
- Holder closes feature done → claim auto-releases; claim on done feature → 409.
- Adapter (src/Overmind.Mcp/): register session on startup (Program.cs), stamp the header on every API call (OvermindApiClient.cs HttpClient setup), set EndedAt on stdin-EOF/shutdown via IHostApplicationLifetime with a ~3s bounded timeout.
- Tests: lock matrix at the service/API level, touch behavior, register/end lifecycle.
</scope>

<anti-scope>
No MCP tool surface changes (task 03), no web, no docs. Do not touch planning_current_task.
</anti-scope>

<constraints>
English-only, no comments by default, no auto-commits. Lock check lives server-side, one funnel — not sprinkled per-endpoint where a shared guard exists.
</constraints>

<acceptance-criteria>
- [ ] `dotnet build` + full `dotnet test` green
- [ ] Header presence updates LastSeenAt; absence never locks
- [ ] Each of the 12 mutations 409s for a non-holder session with the holder named; reads pass for everyone
- [ ] set_feature_status(done) by holder releases the claim
- [ ] Adapter registers on start, stamps every call, marks ended on shutdown
</acceptance-criteria>

<refs>
- Spec: 0e774399 (Session lifecycle + MCP agent door sections).
- Task 01 (order 1) — entity/service foundation this task wires.
</refs>

<obstacles></obstacles>

<log></log>

<log>
- 2026-09-30 [agent] — orchestrator recon: OvermindApiClient.CreateHttpClient (OvermindApiClient.cs:56) is called from TWO sites — Program.cs:35 and Registration/OnboardProjectResolver.cs:14. The X-Overmind-Session stamp must live INSIDE CreateHttpClient (handler on the returned client), otherwise the resolver's client ships headerless and its calls would be treated as human/legacy and never locked.
- 2026-10-01 [agent] — orchestrator scope note: task 01 (compile-driven) already removed part of the pin surface that touched this task's area — AgentPlanningApi SetCurrent route/handler and related pin test coverage are gone from the working tree. Task 02 = header plumbing (X-Overmind-Session inside CreateHttpClient), register/end endpoints, server-side touch, lock across the 12 feature-scoped mutations, adapter start/EOF lifecycle. Re-grep before editing: lines shifted.
- 2026-10-01 [agent] — dispatched to dotnet-coder (same session as task 01 — owns FeatureClaimService context), task 01 done unblocked the chain
</log>
