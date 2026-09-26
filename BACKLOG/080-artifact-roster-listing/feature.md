---
feature: 080
slug: artifact-roster-listing
status: draft
priority: p1
version: 1
created: 2026-09-26
---

# Feature 080 — Artifact roster: planning_list_artifacts — metadata-only listing per feature (order/kind/status/review/AC/brief)

<goal>
Kill the 2-tier read cliff on the agent path. Today `planning_list_features` returns aggregates only (`taskDone/taskTotal`), and `planning_read_feature` is "the most expensive read" — full bodies of every artifact. A 13-task feature is either a number or ~40k tokens; field agents (MindlashStudio dissection, 2026-09-26) fell back to grepping the materialized BACKLOG/ mirror, which bypasses the board-as-source-of-truth model.
</goal>

<requirements>
- New MCP tool `planning_list_artifacts(projectId, slug, kind?)`: metadata only, no bodies — `artifactId, order, kind, status, review, acTicked/acTotal, brief` (truncated per task-brief rules), version, updatedAt.
- Shape mirrors `list_entries` metadata-only default (proved in the field).
- Compact enough that a 13-task feature fits in well under 2KB.
</requirements>

<non-goals>
- No body content in the listing (full/partial reads stay with planning_read_artifact).
- No web UI change required (UI already reads through its own HTTP surface; may adopt later).
- Batch mutations (044), diff (045).
</non-goals>

<refs>
- Source report: MindlashStudio session note 2026-09-26 (gap 1 of 3; note deleted after intake — provenance in this body).
- Shape precedent: 041-entries-metadata-listing.
- Companion gaps from the same report: current_task held-signal → folded into 068 (OM-13); task-level dependencies → 081.
</refs>