---
feature: 080
slug: artifact-roster-listing
status: validated
created: 2026-09-26
validated: 2026-09-26
---

# Spec 080 — Artifact roster: planning_list_artifacts — metadata-only listing per feature (order/kind/status/review/AC/brief)

<goal>
Close the 2-tier read cliff between `planning_list_features` (aggregates only) and `planning_read_feature` (full bodies, ~40k tokens on a 13-task feature). A field agent must resolve "what does feature X consist of and what state is it in" in one cheap call — without falling back to grepping the materialized BACKLOG/ mirror.
</goal>

## Tool contract

`planning_list_artifacts(projectId, slug, kind?)`

| Param | Type | Notes |
|---|---|---|
| projectId | uuid | required, same resolution rules as other feature-scoped tools |
| slug | string | feature slug, required |
| kind | string? | optional filter: `spec` \| `plan` \| `task` \| `result`; unknown value → 400 listing allowed values |

Response — metadata only, no bodies, per artifact:
`artifactId`, `order`, `kind`, `status`, `review` (tasks only), `acTicked`/`acTotal` (tasks only), `brief` (task-only, capped by the existing task-brief rule), `version`, `updatedAt`.

## Behavior rules
- Metadata-only is the ONLY mode — no include_content switch (unlike list_entries). Bodies stay with `planning_read_artifact`.
- Sort: documents first (`spec`, `plan`, `result` in this order), then tasks by `order` asc. This grouping is an agent default — the feature body left ordering open.
- `kind` filter narrows the same projection, same sort.
- Budget: a 13-task feature listing fits well under 2KB — short field names, brief is the only long field and is already capped.
- Unknown feature → same error shape as `planning_read_feature`. Works for any feature regardless of status.
- No pagination — artifact count per feature is bounded and small.

## Non-goals
- No body content or section reads (`planning_read_artifact` keeps that).
- No web UI change (UI may adopt later through its own HTTP surface).
- No batch mutations (044), no diffing (045).

<log>
- 2026-09-26 [agent] — spec drafted from feature body; documents-first grouping is an agent default, flagged for the approval gate.
- 2026-09-26 [agent] — validated with human proof: notification 0d4e08e2-295d-41cc-9e96-0f0c093e9c25 replied 2026-09-26 20:08:18 UTC
</log>