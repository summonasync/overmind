---
feature: 070
task: 02
slug: response-polish-round3
status: done
review: light
created: 2026-09-26
brief: All seven mutation tools project the unified echo {id, version, status, lastLogLine, warnings?}; honest no-op text
---

# 070/02 — MCP: uniform mutation echo projection

<goal>
Spec R1-adapter, R2-echo: all seven mutation tools project ONE echo shape {id, version, status, lastLogLine, warnings?}; no-op renders honestly.
</goal>

<scope>
- Fold append_log, set_status, set_current, set_feature_status, validate onto the unified projection reading the live host responses from task 01 (no request-data fabrication — 037 lesson). patch/upsert align (already rich).
- Re-tick renders "already ticked — no change" (distinct from a real patch echo).
- Canned fixtures updated for the new host shapes; Mcp pins enumerate the seven tools and assert the key-set + no-op text.
- Task 01 lands first — project its live responses, do not guess.
</scope>

<anti-scope>
- Host endpoints (task 01), docs texts (03), delete_entry (04).
</anti-scope>

<constraints>
- .NET 10 / C# 14; no comments; English-only; no git commands; dotnet build 0 warnings + dotnet test green; 068/077 grep gates stay green.
</constraints>

<acceptance-criteria>
- [x] Seven mutation tools return the unified key-set (contract pin enumerating tools)
- [x] Re-tick text "already ticked — no change" pinned
- [x] No body content in any echo (compactness pin from 037 stays green)
- [x] dotnet build 0 warnings + dotnet test green
</acceptance-criteria>

<refs>
- Spec cda4c5c7 R1-adapter/R2; task 01; 037 Compact* helpers + regression pins.
</refs>

<obstacles />

<log />

<log>
- 2026-09-26 [agent] — 070/02 picked up: fold five mutation tools onto the unified echo from task 01 live responses; honest re-tick text; Canned fixtures + key-set pins
- 2026-09-26 [agent] — Coded (dotnet-coder): five laggards folded onto the unified echo {id, version, status, lastLogLine, warnings?} read from the live task-01 host responses — append_log, set_status (warnings + featureStatusChanged copied when present), set_current, set_feature_status (id keyed by slug), validate; no request-data fabrication anywhere (the old set_status confirmed from request fields — replaced; stub lastLogLines deliberately differ from posted logNotes to pin that). patch/upsert aligned: CompactArtifact gains lastLogLine from the artifact log; patch projects {id, version, status, lastLogLine, section, bodyChars} + statusChanged/featureStatusChanged when demotion fired. Honest no-op: a re-tick renders the text "already ticked — no change" instead of a fake patch echo (description updated to say so; StatusModelContractTests pin updated). OBSTACLE NOTE (host spillover, task-01 boundary): the live patch response carried no status/lastLogLine, so the patch projection would have had to fabricate — fixed additively in PlanningArtifactPatchResponse (+status, +lastLogLine); task-01 key-set pins updated in AgentPlanningApiTests. STOP doors preserved: set_current/validate keep the STOP-catch CallToolResult refusal path byte-identical (spec/plan human gates, blocker refusals); success echoes ride the content text so ToolResultText parses uniformly. Canned fixtures updated to the new host shapes; new pins: seven-tool uniform key-set enumeration, no-op text, per-tool key order incl. warnings/featureStatusChanged variants, anti-fabrication (echo lastLogLine != posted logNote). Gates: 068 frozen-sweep and 077 proof-line re-verified green. Build 0 warnings; tests 1515/1515 (Core 215 / Infra 496 / Api 447 / Mcp 357, +2 new pins).
- 2026-09-26 [agent] — 070/02 done by dotnet-coder: all seven mutation tools project the unified echo from live host responses (patch/upsert aligned; re-tick renders "already ticked — no change"). Evidence: build 0 warnings, tests 1515/1515 (Core 215 / Infra 496 / Api 447 / Mcp 357), 068/077 grep gates green. Host spillover recorded in the task log (patch response gained status/lastLogLine additively). All 4 AC ticked; ready for light review.
</log>
