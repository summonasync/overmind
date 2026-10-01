---
feature: 099
task: 02
slug: agent-claims
status: review
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
- [x] `dotnet build` + full `dotnet test` green
- [x] Header presence updates LastSeenAt; absence never locks
- [x] Each of the 12 mutations 409s for a non-holder session with the holder named; reads pass for everyone
- [x] set_feature_status(done) by holder releases the claim
- [x] Adapter registers on start, stamps every call, marks ended on shutdown
</acceptance-criteria>

<refs>
- Spec: 0e774399 (Session lifecycle + MCP agent door sections).
- Task 01 (order 1) — entity/service foundation this task wires.
- Review round 2: audits/review-099-02.md (blocker: priority route, nits: MarkEnded guard, parameter order).
</refs>

<obstacles>
- Lock funnel shape: the constraint said "service layer (PlanningFeatureService / PlanningPolicy)" but the human door shares those services and must never lock; per the spec's human-door carve-out the lock lives at the AGENT-DOOR boundary as a single ClaimLockFilter (IEndpointFilter) attached to the agent mutation routes (now 12: status, update, queue-move, priority, dependencies add/remove, artifacts, artifacts/tasks, artifact status/log/patch/validate). Filter reads projectId+slug from route values, or artifactId via FeatureClaimService.EnsureHolderForArtifactAsync (resolves the owning feature through a join); holder 409 wording identical to the service matrix. Headerless = human/legacy → filter passes through. CORRECTION after review 099-02: the original round claimed "queue-move covers priority and move" — wrong; priority rode the unlocked human PUT. Fixed in the fix round (see the priority bullet).
- Server-side touch is a dedicated SessionTouchMiddleware registered right after builder.Build(): header presence → session.Touch() + SaveChanges on every request (reads included); unknown session id → silently ignored (registration is the adapter's startup job, task 02 tests seed rows).
- Adapter stamping per orchestrator recon: X-Overmind-Session is stamped inside CreateHttpClient via a new AdapterSessionHandler (DelegatingHandler) reading a process-wide AdapterSession.Id set once after registration — covers BOTH call sites (Program.cs and Registration/OnboardProjectResolver). Session identity: OVERMIND_ALIAS / OVERMIND_VENDOR env vars, defaults "agent"/"unknown" (claim tools in task 03 carry explicit identity per call, so registration identity is a fallback only).
- Shutdown: IHostApplicationLifetime.ApplicationStopping → AgentSessionLifecycle.MarkEnded — sync-over-async on purpose (ApplicationStopping callbacks are synchronous) with a 3s bounded CTS; dead host API is swallowed with a WARN on stderr; Guid.Empty (failed registration) now skips the end call entirely (review nit). Register failure at startup is non-fatal (WARN; adapter keeps working headerless = legacy semantics).
- FIX ROUND (review 099-02 blocker): planning_set_feature_priority rode the unlocked human-door PUT. New dedicated route POST /api/agent/projects/{projectId}/planning/features/{slug}/priority with .LockToClaim() (SetPlanningFeaturePriorityRequest {priority} → PlanningFeatureService.UpdateAsync priority path → full feature payload the adapter's CompactFeature already parses); the MCP tool re-pointed from PUT /api/projects/... to the new agent route; PlanningFeaturesApi.LoadFeatureWithArtifacts/BlockedStatesAsync promoted private→internal for reuse; the lock-matrix row and the Mcp driver test now pin the REAL route (agent priority POST), move_feature stays on queue-move.
- FIX ROUND (user-approved spec addition, logged on spec 0e774399): agent-door import is now claim-aware. POST /api/agent/projects/{projectId}/planning/import parses the feature folder first (BacklogImporter split into ParseFeatureAsync + ImportFeatureAsync over a shared private ParseImportAsync — parse and write no longer coupled), then FeatureClaimService.EnsureImportAllowedAsync(projectId, sessionId, slug) 409s FeatureClaimHeld (holder named, liveness wording) when the imported slug is claimed by a FOREIGN session — atomic, nothing written in that case; unclaimed features and own-session claims pass unchanged. Headerless import (human door parity) stays unlocked.
- FIX ROUND nits applied: EnsureHolderForArtifactAsync parameter order now (artifactId, sessionId) matching the sibling EnsureHolderAsync(projectId, slug, sessionId); MarkEnded Guid.Empty guard.
- Debugging detour recorded for posterity: the API-level tests initially "failed" because raw-SQL seeds wrote Guids as lowercase TEXT while EF Core SQLite stores Guid TEXT UPPERCASE — SQLite string comparison is case-sensitive, so app and tests looked at the same file but never matched rows. All raw Guid interpolations in tests now uppercase. A couple of pre-existing raw-SQL migrations also lack trailing semicolons, which breaks whole-script (dotnet ef migrations script) consumers — noted in task 01 obstacles, left alone here.
- Driver-based MCP tests launch the REAL adapter process, so startup registration + shutdown end now hit the stub on every McpSessionTestBase test: the stub 404s them (no session routes), the adapter's non-fatal registration path absorbs it, and the one strict request-count assertion (gated validate test) now excludes /api/agent/sessions calls.
- Two full-suite failures during the round were real bugs caught by the suite, not flakes: AgentSession.Create used two DateTime.UtcNow calls (StartedAt/LastSeenAt drifted microseconds apart) — single now; and the driver request-count above.
- Full-suite runs flaked twice under parallel load during the round (timing-sensitive); isolated reruns green both times, control full runs green (1719 → 1720 → 1773 → 1776 passed as rounds landed).
</obstacles>

<log>
- 2026-09-30 [agent] — orchestrator recon: OvermindApiClient.CreateHttpClient (OvermindApiClient.cs:56) is called from TWO sites — Program.cs:35 and Registration/OnboardProjectResolver.cs:14. The X-Overmind-Session stamp must live INSIDE CreateHttpClient (handler on the returned client), otherwise the resolver's client ships headerless and its calls would be treated as human/legacy and never locked.
- 2026-10-01 [agent] — orchestrator scope note: task 01 (compile-driven) already removed part of the pin surface that touched this task's area — AgentPlanningApi SetCurrent route/handler and related pin test coverage are gone from the working tree. Task 02 = header plumbing (X-Overmind-Session inside CreateHttpClient), register/end endpoints, server-side touch, lock across the 12 feature-scoped mutations, adapter start/EOF lifecycle. Re-grep before editing: lines shifted.
- 2026-10-01 [agent] — dispatched to dotnet-coder (same session as task 01 — owns FeatureClaimService context), task 01 done unblocked the chain
- 2026-10-01 [agent] — Executed by dotnet-coder. Host: AgentSessionsApi (POST /api/agent/sessions register, POST /api/agent/sessions/{id}/end), SessionTouchMiddleware (header → LastSeenAt), ClaimLockFilter on the 11 agent mutation routes (one funnel, human door untouched), FeatureClaimService.EnsureHolderForArtifactAsync. Adapter: AdapterSessionHandler stamps X-Overmind-Session inside CreateHttpClient (covers resolver call site), AgentSessionLifecycle (register on startup, bounded 3s end on ApplicationStopping), AgentIdentity env (OVERMIND_ALIAS/OVERMIND_VENDOR). Tests: AgentSessionLockApiTests (45: register/end lifecycle, touch presence/absence, 12-mutation matrix foreign-409/holder/headerless, reads-open, done-release), SessionLifecycleTests (8: register posts, bounded end, header stamp, identity env). Gates: dotnet build --no-incremental 0/0; dotnet test 1773 passed / 0 failed (Core 251, Infra 552, Api 556, Mcp 414); format clean outside Migrations.
- 2026-10-01 [agent] — task 02 executed to review: build 0/0, full suite 1773 passed / 0 failed, all 5 ACs ticked; lock at agent door via ClaimLockFilter (human door exempt per spec), touch middleware, sessions register/end, adapter lifecycle inside CreateHttpClient per orchestrator recon; ready for dotnet-code-reviewer (light review per plan)
- 2026-10-01 [agent] — 2026-10-02 [agent] — reviewed by dotnet-code-reviewer: FAIL — 1 BLOCKER (planning_set_feature_priority rides the unlocked human-door PUT; test matrix pins the wrong route), 1 WARNING (agent import mutates claimed features, outside the 12-list — needs spec decision), 3 nits; rest clean, build 0/0, 1773/0 re-verified; report audits/review-099-02.md. Fix loop dispatched to the coder.
- 2026-10-01 [agent] — Fix round for review 099-02: (1) BLOCKER — new locked agent-door route POST /api/agent/projects/{projectId}/planning/features/{slug}/priority (.LockToClaim), MCP planning_set_feature_priority re-pointed off the human PUT, matrix row + Mcp driver test now pin the real route; (2) user-approved spec addition — agent import is claim-aware: BacklogImporter split into ParseFeatureAsync/ImportFeatureAsync over shared ParseImportAsync, FeatureClaimService.EnsureImportAllowedAsync 409s foreign-claimed imports before any write, own-claimed/unclaimed pass, headerless never locked; (3) nits — MarkEnded Guid.Empty guard, EnsureHolderForArtifactAsync parameter order (artifactId, sessionId). For task 03 coordination: the priority tool now calls POST .../priority (route owned by task 02), import route stays claim-checked server-side. Gates: build 0/0, full suite 1776 passed / 0 failed (Core 251, Infra 552, Api 559, Mcp 414).
</log>