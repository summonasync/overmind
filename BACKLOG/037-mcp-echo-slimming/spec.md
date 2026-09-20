---
feature: 037
slug: mcp-echo-slimming
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Spec 037 — MCP echo slimming: мутационные тулы не эхят полное тело артефакта

<decisions>
- Adapter-level projection only — the host API and web keep full bodies in their contracts (they need them); the Overmind MCP adapter (src/Overmind.Mcp) maps mutation responses to compact DTOs before returning to agents. Zero host changes, zero risk to existing consumers.
- Body dropped ALWAYS in mutation tool responses — no includeBody flag. Rationale: the caller just wrote the body; needing it back after a write is rare and planning_read_feature / read tools are one call away. YAGNI on the flag; it can be added later if a real case appears.
- Compact response shape: { id, kind (or entity type), status, version, order (artifacts), priority where applicable, frozenAt, updatedAt, bodyChars } — bodyChars gives cheap scale confirmation that the write landed with the expected size. planning_set_status additionally carries the LAST log line (the appended mutation provenance) instead of the whole log.
- Scope v1 = every MCP mutation tool that echoes a body today: planning_upsert_artifact, planning_set_status (confirmed burners this session); audit planning_append_log, planning_set_feature_status, set_current, update_entry, write_entry during the task — slim any that echo bodies, leave already-compact ones alone. Read tools (planning_read_feature, list_entries, search, list_*) are NOT touched — bodies there are the payload.
- The projection lives as small private mappers next to the tool handlers in Overmind.Mcp (no new shared abstraction until a second consumer pattern emerges).
- Pickup: new adapter binary ships with the next pack; running agent sessions keep the old echo until their MCP adapter restarts — noted in the result, no migration.
</decisions>

<open-questions>
- none (user request 2026-09-20 + orchestrator decisions above; gate confirms).
</open-questions>

<requirements>
- Overmind.Mcp: compact projections for mutation tool responses per decisions (body dropped, metadata + bodyChars + set_status log tail).
- Audit list from decisions executed and recorded in the task log (which tools were slimmed, which were already compact).
- Tests (Overmind.Mcp.Tests): mutation tool outputs do NOT contain the submitted body content; they DO carry id/status/version/bodyChars (and the log tail for set_status); read tools unchanged (regression pin on one representative).
- Gates: dotnet build + full suite green (baseline 989).
</requirements>

<anti-scope>
- No host/API contract changes; no web changes.
- No includeBody flag, no pagination, no truncation modes (drop, not truncate).
- Read tools untouched.
</anti-scope>