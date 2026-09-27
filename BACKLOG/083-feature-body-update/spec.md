---
feature: 083
slug: feature-body-update
status: validated
created: 2026-09-26
validated: 2026-09-27
---

# Spec 083 — planning_update_feature: agent-door правка title/body/priority с logNote + log-гвард фича-боди (глотает microbatch #74)

<goal>
Unfreeze the feature body for agents: a fresh feature with a typo in the body (field evidence: Squeezr agent, stray CJK chars in requirements) is currently unfixable through MCP — create on an existing slug 409s, patch_artifact covers artifacts only. One tool edits title/body/priority partially, always with a provenance logNote; the feature `<log>` becomes append-only under the same guard 078 shipped for artifacts — closing microbatch #74 (the human-door feature-update path currently lacks the guard).
</goal>

## Tool contract

`planning_update_feature(projectId, slug, title?, body?, priority?, logNote)`

- Partial update: at least one of title/body/priority must be present, otherwise 400 listing the editable fields.
- `logNote` mandatory (same three-layer enforcement as artifact body edits) — appended as a dated `[agent]` line to the feature `<log>`.
- `priority`: p0|p1|p2|p3 (case-insensitive); unknown → 400 listing allowed values.
- `title`: non-empty, trimmed; `body`: non-empty.
- Status is NOT editable through this tool — `planning_set_feature_status` remains the only status door (non-goal to change its semantics).

## Agent-door HTTP

`POST /api/agent/projects/{projectId:guid}/planning/features/{slug}/update` — follows the agent-door mutation pattern (POST + verb: status/current/queue-move precedents; no PUT exists on the agent door today). Same validator + error mapping shape as the human-door feature update.

## Log guard (both doors, one guard)

- Feature `<log>` is append-only in the tail: the stored log must remain an exact ordered prefix of the incoming body's log (trim-normalized) — the 078 contract (`RequireLogHistoryAppendable` on artifacts) mirrored onto `PlanningFeature.Update`.
- History rewrite / log-line mutation / tail deletion → `LogHistoryNotAppendable` 409-family failure with the same diagnostic style as 078.
- The guard lives on the entity/service seam shared by BOTH doors (human PUT included) — that is the #74 absorption; no door-local copies.
- Feature-body edits are always allowed (features have no frozen/validated lifecycle — that lives on artifacts); the logNote + appended log line is the provenance.

## Response (uniform echo, 070 family)

`{ slug, status, version, updatedAt, bodyChars }` — version bumps on every applied edit (feature-version semantics unchanged otherwise).

## Behavior rules

- Editing an existing feature by slug; unknown slug → same 404 shape as `planning_read_feature`.
- Every applied change snapshots nothing new (features keep no version history beyond the version counter) but appends the logNote line — materialized `feature.md` updates through the existing sync.
- Priority change echoes in the response via version/updatedAt (the board sorts by it live, no extra field).

## Non-goals

- Deleting features via MCP (unchanged).
- Changing `planning_set_feature_status` semantics (068 territory).
- Web UI work (the human PUT surface already exists; guard lands server-side for both doors).

<log>
- 2026-09-27 [agent] — spec drafted from feature body; agent-door route chosen as POST /update per door mutation pattern (no PUT on agent door); guard placement on the shared seam named as the #74 absorption point.
- 2026-09-27 [agent] — validated with human proof: notification 73621927-6ce4-4a6e-abca-b00c9a7929cb replied 2026-09-27 00:05:53 UTC
</log>