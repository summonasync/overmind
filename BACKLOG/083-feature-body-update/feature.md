---
feature: 083
slug: feature-body-update
status: draft
priority: p1
version: 1
created: 2026-09-26
---

# Feature 083 — planning_update_feature: agent-door правка title/body/priority с logNote + log-гвард фича-боди (глотает microbatch #74)

<goal>
A feature body is "durable what/why", yet it is frozen for agents the moment it is created: `planning_create_feature` on an existing slug → 409 with no upsert path, `planning_patch_artifact` covers artifacts only. A typo in a fresh feature body (Squeezr agent hit: stray CJK chars in <requirements>) is unfixable through MCP — the agent falls back to hand-editing in the UI or mirror-hacks the content into a spec artifact. Field evidence 2026-09-26.
</goal>

<requirements>
- `planning_update_feature(projectId, slug, {title?, body?, priority?}, logNote)` — partial update, mandatory logNote like artifacts.
- Log guard for the feature body <log>: append-only in the tail, history rewrite rejected — same contract 078 shipped for artifacts (closes microbatch #74: the human-door PlanningFeature.Update path currently lacks the guard; wire both doors through one guard).
- Feature status transitions stay out of scope: planning_set_feature_status remains the only status door (done/inProgress agent targets, mandatory provenance logNote).
- Validation/frozen semantics mirror artifacts: body edit on a validated spec is unaffected (that guard lives on artifacts); feature-body edits are always allowed but logged.
</requirements>

<non-goals>
- Changing planning_set_feature_status semantics (068 territory for its echo/marker work).
- Deleting features via MCP.
</non-goals>

<refs>
- Source: Squeezr Qwen Code gap note 2026-09-26 (deleted after intake; evidence quoted in the goal).
- 078-upsert-log-guard — the artifact log-guard contract to reuse for the feature door.
- Microbatch #74 (PlanningFeature.Update log-guard absence — absorbed here, remove from the queue when this lands).
</refs>