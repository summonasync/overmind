---
feature: 058
slug: agent-side-import
status: inProgress
priority: p2
version: 1
created: 2026-09-23
---

# Feature 058 — Import from materialized files unavailable to agents — migration reroutes every body through the model (OM-7)

Field report OM-7 (FEATURELIST/overmind-features-2026-09-23.md): the quickstart mentions import ("Import restores statuses verbatim from the materialized files") but no MCP tool exposes it. Migrating a `BACKLOG/` folder currently means piping every body through the model: ~17K chars of spec+plan through the main agent, ~70K tokens per sonnet sub-agent for one task each.

`BacklogImporter` already exists host-side (src/Overmind.Infrastructure/Planning/BacklogImporter.cs) — the gap is the MCP surface, not the engine.

Requested: `planning_import_feature(projectId, path)` reading a feature folder in the materialization format (frontmatter + XML sections) and creating/updating the feature with statuses restored verbatim; alternatively `bodyFromPath` on upsert for files under the project root.

<log>
- 2026-09-23 [auto] — artifact created → inProgress
</log>
