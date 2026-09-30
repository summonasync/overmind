---
feature: 090
slug: field-nits-batch
status: validated
created: 2026-09-29
validated: 2026-09-29
---

# Spec 090 — Field-report nits batch: brief/title patch, feature body-only read, 404 alias hints, no-plan quickstart line (бывш. 082)

## Goal

Close the four small agent-experience items left after the BobHouse/Mindlash intake waves (feature body R1, R2, R4, R5) and record item R3 as superseded. Each item is too small for its own feature; together one focused batch touching the MCP adapter layer, the planning API surface, and tool descriptions.

## Decisions (user-approved in chat, 2026-09-29)

- **R4 shape: alias addressing + 404 hint.** A typoed GUID cannot list neighbors (the feature is not derivable from a GUID), so artifacts get slug aliases resolved in the MCP adapter layer — the backend never sees aliases. Additionally, every "artifact not found" text teaches the alias format and points at `planning_list_artifacts`.
- **R2 shape: `includeArtifacts` flag.** `sections=` would fake a pseudo-section on a document that has none; an honest boolean flag fits.

## R1 — Task brief patch

Title patching is already covered by `planning_update_feature` (title/body/priority) — out of this item. What is missing: fixing a typo in a task brief costs a full body re-upsert + version bump today.

- `planning_patch_artifact` gains `brief` (optional string, max 500 chars — same cap as upsert).
- Exactly one action per patch: section XOR ac-tick XOR brief. `InvalidPatchRequest` message updated to name all three.
- Brief is task-only: spec/plan/result targets reject with `BriefNotAllowedForKind` (400).
- Uses the existing `PlanningArtifact.SetBrief` semantics: no version bump, no log line, body untouched (same as brief-on-upsert today).
- `logNote` not required (task artifacts are never frozen).
- HTTP: `PatchPlanningArtifactRequest` gains `brief`; `PlanningArtifactService.PatchAsync` gains the third branch. MCP: `planning_patch_artifact` gains the parameter + description update.

## R2 — Body-only feature read

- `planning_read_feature(projectId, slug, includeArtifacts=false)` returns the feature body plus an artifact roster-level summary (kind, order, status, brief for tasks, acTicked/acTotal) — no artifact bodies, no log lines, no versions.
- Default `includeArtifacts=true` keeps today's contract byte-compatible.
- HTTP `GET /api/projects/{projectId}/planning/features/{slug}` gains the same query flag (web UI keeps default).

## R3 — `overmind://resources` — superseded

<removed 2026-09-29 — superseded by 070 (R5), both dead-pointer sites removed and absence-pinned; confirmed live 2026-09-26>

No code. This tombstone in the spec is the record the feature body asked for.

## R4 — Artifact alias addressing + 404 hints

- Alias formats: `{featureSlug}/T{order}` for tasks (e.g. `090/T3`); `{featureSlug}/S`, `{featureSlug}/P`, `{featureSlug}/R` for the single spec/plan/result documents.
- Optional `alias` parameter on four MCP tools: `planning_read_artifact`, `planning_patch_artifact`, `planning_set_status`, `planning_append_log`. `alias` XOR `artifactId` — passing both is a client-side `McpException`.
- Alias resolution happens in the MCP adapter: fetch the feature's artifact roster (existing read path), match kind/order, use its GUID against the existing HTTP endpoints. Backend unchanged.
- Alias needs the feature's project: these four tools lack `projectId` today, so an optional `projectId` parameter joins them; `alias` without `projectId` is a client-side `McpException` naming the requirement.
- Unknown alias or kind/order mismatch → error listing what the feature actually has (kind/order pairs), same spirit as `PatchSectionNotFound` diagnostics.
- 404 hint: `PlanningException.MissingArtifact` message and the three bare `HttpErrors.NotFound("Artifact … was not found.")` sites in `PlanningArtifactsApi` (GetArtifact, GetVersions, UpdateArtifact) gain the alias-format hint + the `planning_list_artifacts` recovery pointer.

## R5 — No-plan-no-auto-review in tool descriptions

The quickstart line already exists (`OnboardQuickstart.Text` line: "Without a plan there is no auto-promotion to review…"). This item adds the same fact to the tool descriptions that gate the affected moments:

- `planning_upsert_artifact` — plans are created here; state that a feature without a validated plan never auto-promotes to review.
- `planning_current_task` — agents wait for review here; state where the manual flip lives (`planning_set_feature_status`, human/agent door per its own rules).

One sentence each, no behavior change — decision C from the 038 quirk note is already implemented, this is documentation parity.

## Acceptance criteria

- [ ] brief-patch: patching `brief` on a task updates the brief without a version bump and without touching the body; on spec/plan/result it 400s with `BriefNotAllowedForKind`.
- [ ] Combined patch ops (section + brief, tick + brief) are rejected as `InvalidPatchRequest`.
- [ ] `includeArtifacts=false` returns body + roster summary only (no bodies/logs); default response is unchanged vs today (contract test).
- [ ] All four tools resolve `090/T3`-style aliases (and S/P/R for documents); unknown alias errors list the feature's actual kind/order pairs.
- [ ] `alias` + `artifactId` together, and `alias` without `projectId`, are rejected client-side in the MCP layer.
- [ ] Unknown-GUID 404s (read/patch/status/log + GetArtifact/GetVersions/UpdateArtifact) carry the alias hint.
- [ ] Tool descriptions of `planning_upsert_artifact` and `planning_current_task` carry the no-plan rule.
- [ ] `dotnet build` + `dotnet test` green.

## Non-goals

- HTTP-level alias routing (aliases are MCP-adapter-only).
- Feature-number aliasing (`089` guards slug-number collisions; unrelated).
- New listing surfaces (080), task dependencies (081), mutation-echo work (068/070).
- UI changes.

## Refs

- Origin: BobHouse 020 field note B6; 038 quirk note (decision C).
- `047-api-read-surface-polish` — sibling doc-gap batch.
- `089-feature-number-collision-guard` — the slug/number hole that forced this feature's renumbering 082→090.
- `070` (R5) — supersedes item R3.

<log>
- 2026-09-29 [agent] — spec drafted from feature body + code recon (PlanningArtifactService.PatchAsync, OnboardQuickstart.Text, OvermindPlanningTools); two design forks resolved with user in chat (alias+hint; includeArtifacts flag).
- 2026-09-29 [agent] — validated with human proof: notification 520538a7-7586-4b9a-991f-ce7da0ff597b replied 2026-09-29 16:31:58 UTC
</log>