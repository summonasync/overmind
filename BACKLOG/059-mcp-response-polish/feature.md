---
feature: 059
slug: mcp-response-polish
status: inProgress
priority: p2
version: 1
created: 2026-09-23
---

# Feature 059 — MCP response polish: naming, echo and doc gaps — 5 nits (OM-9)

Field report OM-9 (FEATURELIST/overmind-features-2026-09-23.md), five small nits:

1. The 409 text says "A change note is required…" while the parameter is named `logNote` — align the names.
2. `planning_upsert_artifact` response for a task does not echo `brief` — confirmation visible only via `read_artifact`.
3. `planning_set_status` appends `YYYY-MM-DD [agent] — <logNote>` to `<log>` — useful, but the description only says "records why in the artifact log" without documenting the line shape.
4. Upsert requires `logNote` only when editing a validated artifact; the description is correct but the conditionality is non-obvious — add "not needed on create".
5. Onboard works fine without being called first (agents with explicit `projectId` skipped it, no errors) — either enforce "Call this first" or soften the wording.

<log>
- 2026-09-23 [auto] — artifact created → inProgress
</log>
