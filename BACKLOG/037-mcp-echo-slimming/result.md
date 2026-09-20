---
feature: 037
slug: mcp-echo-slimming
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 037 — MCP echo slimming: мутационные тулы не эхят полное тело артефакта

<summary>
Feature 037 done: mutation MCP tools no longer echo full bodies. Five tools slimmed at the adapter level (planning_create_feature — the worst echoer, returning the feature body plus every artifact body; planning_upsert_artifact; planning_set_status; write_entry; update_entry) to compact confirmations {id/kind/status/version/frozenAt/updatedAt/bodyChars}; planning_set_status carries lastLogLine (the submitted logNote, verbatim-stored — honesty verified end-to-end). Three mutators were already string-compact and untouched; read tools untouched with a regression pin. Host and web contracts unchanged. Thousands of tokens per ceremony session returned to the agent budget.
</summary>

<tasks>
01 — Adapter-level compact projections: audit of 9 mutation tools, 5 slimmed, private mappers next to handlers, 6KB marker test (echo < 500 chars, bodyChars == stored length), canned tests updated, read-pin on both bodies. Review PASS (audits/review-037-01.md), 990/990.
</tasks>

<evidence>
- Reviewer re-ran gates: build 0 warnings, 990 passed (108/336/238/308).
- lastLogLine honesty: PlanningArtifact.SetStatus → PlanningLog.Append stores the note verbatim; the host's 200 fires only after SaveChanges — the echo is byte-true. Empty-200 provenance documented in CONTEXT.md.
- bodyChars computed from the HOST response body (server-side transformations reflected), pinned by StoredLength against canned JSON.
- Obstacle recorded: spec assumed set_status echoed body+log; the host returns an empty 200 (agent door has no artifact read-back; human door forbidden to the adapter by the two-doors ADR) — resolved by the verbatim-note confirmation.
</evidence>

<deviations>
- create_feature was outside the task's named audit list but inside the spec phrase ("every mutation tool that echoes a body today") — reviewer judged in-scope; it echoed feature body + all artifact bodies.
- overmind_notify still echoes its text (WARNING-1): not in the task list, borderline spec phrase — routed to microbatch #35 as the single remaining exception.
</deviations>

<follow-ups>
- Microbatch #35 (overmind_notify projection).
- Pickup: new adapter binary ships with the next pack; running agent sessions keep the old echo until their MCP adapter restarts (this session included).
</follow-ups>

<log>
</log>