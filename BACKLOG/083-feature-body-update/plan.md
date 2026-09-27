---
feature: 083
slug: feature-body-update
status: validated
created: 2026-09-27
validated: 2026-09-27
---

# Plan 083 — planning_update_feature: agent-door правка title/body/priority с logNote + log-гвард фича-боди (глотает microbatch #74)

<approach>
Four tasks, layered bottom-up so each gate runs on a green build: the entity guard + edit method (Core, #74 absorption point), the service overload + agent HTTP door (Infrastructure + Host Api), the MCP tool with pins (Mcp), and the docs anchors. Reuse is maximal: the 078 guard shape ports almost verbatim to the feature entity; the service already owns transaction + materializer sync + queue-tail on priority change; the agent door follows its own POST + verb pattern.

- The guard lives inside `PlanningFeature.Update` (src/Overmind.Core/Entities/PlanningFeature.cs) — port of `PlanningArtifact.RequireLogHistoryAppendable` (PlanningArtifact.cs:309-332): stored `<log>` must remain an exact ordered prefix (trim-normalized) of the incoming body's log; violation → `PlanningException.LogHistoryNotAppendable`. This closes #74 for BOTH doors at the seam (human PUT goes through the same Update).
- The agent path needs a logNote + `[agent]` provenance on the appended log line — the service gets an explicit-logNote overload (`UpdateAsync` with logNote + PlanningLogSource); the existing human-door signature stays (its UI provenance lives in the status branch it already has; a human feature-body edit appends the logNote it passes — guard applies equally).
- Priority semantics stay 072-honest: an agent priority change reassigns the queue tail exactly like the human door — pinned in tests, documented in the tool description ("priority change moves the feature to the tail of its priority band").
- Status stays out of this tool's reach: the overload takes no status parameter; `planning_set_feature_status` remains the only status door.
- Response echo follows the 070 family: `{ slug, status, version, updatedAt, bodyChars }`; feature version semantics are pinned as they exist today in the entity (the coder records in obstacles what Update does to Version and pins it — no silent semantic change).
</approach>

<tasks>
- [ ] 01-feature-log-guard — Core: `PlanningFeature` — port `RequireLogHistoryAppendable` (private, 078 shape, `PlanningLog.Parse` prefix check) and call it from `Update(title, body, ...)` before mutation; extend `Update` to accept an optional log note + `PlanningLogSource` that appends the dated sourced line via `PlanningLog.Append` (same grammar as artifacts); characterization first (PlanningFeatureTests.cs): existing Update/SetPriority/SetStatus pins stay green; new: history rewrite rejected, tail deletion rejected, append accepted, note line lands with `[agent]`/`[ui]` source marker. Pin what Update does to Version (obstacles note if it bumps). Gates: build 0W/0E + full suite → tasks/01-feature-log-guard.md
- [ ] 02-service-agent-update-door — Infrastructure + Host: `PlanningFeatureService.UpdateAsync` overload (projectId, slug, title?, body?, priority?, logNote, source) — mandatory non-empty logNote, no status parameter, reuses project/feature resolution + transaction + `AssignQueueTailAsync` on priority change + materializer sync; agent route `POST /api/agent/projects/{projectId:guid}/planning/features/{slug}/update` in AgentPlanningApi with a FluentValidation request (at least one of title/body/priority; logNote required; priority whitelist p0-p3; title/body non-empty when present); echo `{ slug, status, version, updatedAt, bodyChars }`; unknown slug → 404 via existing mapping; guard violation → LogHistoryNotAppendable surfaced (PlanningErrors.Map already carries it since 078). Tests (Api): happy title-only / body-only / priority-only (queue tail asserted), all-empty 400, missing logNote 400, unknown slug 404, log rewrite 409, `[agent]` marker in the returned/logged line. Gates: build + full suite → tasks/02-service-agent-update-door.md
- [ ] 03-mcp-update-feature-tool — Mcp: `planning_update_feature(projectId, slug, title?, body?, priority?, logNote)` — NOT ReadOnly, Idempotent=false, logNote required parameter; description verbatim pin: "Partial edit of a feature's title, body, or priority — durable what/why stays correctable by agents. logNote is mandatory and lands as a dated line in the feature log. A priority change moves the feature to the tail of its priority band. Status is not editable here — use planning_set_feature_status."; params forwarded, absent strings omitted; echo passthrough (070 slim family, no body echo). Tests: name/verbatim pins (live ListToolsAsync; planning-tools count 23→24, total 33→34), canned happy path (echo shape, no body), logNote-missing 400 surfacing, 404 → tool error. Gates: build + full suite → tasks/03-mcp-update-feature-tool.md
- [ ] 04-docs-anchors — Docs: CONTEXT.md MCP surface "twenty-three" → "twenty-four" + planning_update_feature rung next to planning_list_artifacts (feature-level write: correctable what/why, logNote-guarded); STRUCTURE.md — one clause in the writes bullet that the feature body edit is log-guard append-only on both doors (083/#74); grep-verified. Gates: grep pins + build → tasks/04-docs-anchors.md
</tasks>

<risks>
- **Version semantics ambiguity.** The entity tests pin `SetPriority` keeping Version=1; what `Update` does to Version is not asserted today. The coder pins current behavior and records it — changing it silently is a review blocker.
- **Human PUT logNote absence.** Today the human PUT has no logNote field; the guard (01) must not break it: a human edit without a note still passes the guard as long as the log tail is untouched — the logNote requirement is agent-door-specific (validator-enforced), not entity-enforced. Reviewer should reject any entity-level "logNote always required" drift.
- **Queue-tail surprise.** An agent priority tweak silently moves the feature to its band tail (072 rule). Mitigated by tool-description wording + an Api test asserting the reassignment; a "no-move" option is explicitly out of scope.
- **#74 residual scope.** The human-door PUT gains the guard but not the logNote requirement — #74 is closed by the guard; requiring notes from humans is a separate product decision.
</risks>

<verification>
- dotnet build 0W/0E + full test suite green (baseline 1533).
- Live (post-deploy): planning_update_feature fixes a typo in a fresh feature body with a logNote; the feature log gains an `[agent]` line; a hand-crafted history-rewrite attempt 409s; planning_set_feature_status still the only status path.
- Grep pins: CONTEXT "twenty-four", both docs rungs present.
</verification>

<log>
- 2026-09-27 [agent] — plan drafted after code recon (squeezr-assisted): 078 guard port target = PlanningFeature.Update; service overload with source; agent route POST /update; priority keeps 072 queue-tail semantics; 4 tasks bottom-up.
- 2026-09-27 [agent] — validated with human proof: notification f3d2b680-4809-4e3d-8960-42df7018b42a replied 2026-09-27 00:09:08 UTC
</log>