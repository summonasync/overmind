---
feature: 070
slug: response-polish-round3
status: validated
created: 2026-09-26
validated: 2026-09-26
---

# Plan 070 — Response/doc polish round 3: единый echo-объект, no-op тексты, доки (resources/XML-секции/order), решение archived/delete (OM-15/16/17)

<goal>
Ship R1–R10 as four sequential tasks: host echo objects + feature touch → MCP uniform echo + honest no-op → review scoping + docs → delete_entry. Grep gates from 068/077 stay green throughout.
</goal>

<approach>
- R1 is host-first by necessity: adapters cannot project what the host does not return. Artifact /log, /status, /validate and feature set-current/set-feature-status endpoints return the compact shape {id/slug, version, status, lastLogLine} (warnings? where produced); the empty-200 era ends. The MCP adapter then folds all seven mutation tools onto one projection — 037's CompactArtifact stays for upsert/patch (already rich), the four laggards adopt the same key-set.
- R2 rides the host: the patch endpoint knows a tick was a no-op (068 chain exits early) — response carries the existing shape plus alreadyTicked flag (or the adapter derives it from unchanged version + op kind; implementer picks, pins decide).
- R4 rides the service: every artifact mutation already loads the feature for materialization sync — touch PlanningFeature.UpdatedAt in the same SaveChanges, no log line.
- Docs tasks (R5–R7, R9) are text work over live behavior — executed after T1/T2 so descriptions match shipped responses.
- delete_entry (R8) is additive MCP surface over the existing entries DELETE route, 020 tool pattern (resolution chain, Guarded), with kit/skill guards.
</approach>

<task-breakdown>
1. (R1-host, R2, R4) Backend: compact echo objects on artifact/feature mutation endpoints (no empty 200s); honest no-op on re-tick (version untouched, alreadyTicked surfaced); feature.UpdatedAt touch on artifact mutations in the same SaveChanges; Api pins (echo key-set per endpoint, no-op version pin, touch pin). Files: src/Overmind.Host/Api/PlanningArtifactsApi.cs, PlanningFeaturesApi.cs, src/Overmind.Infrastructure/Planning/PlanningArtifactService.cs (+ feature touch), tests/Overmind.Api.Tests + Infrastructure.Tests. Review: light.
2. (R1-adapter, R2-echo) MCP uniform projection: one echo key-set {id, version, status, lastLogLine, warnings?} across append_log, set_status, set_current, set_feature_status, validate (+ patch/upsert aligned); no-op renders "already ticked — no change"; Canned fixtures updated; Mcp pins enumerate the seven tools and assert the key-set. Files: src/Overmind.Mcp/OvermindPlanningTools.cs, tests/Overmind.Mcp.Tests. Review: light.
3. (R3, R5–R7, R9) Scoping + docs: review field only on task-kind projections (read_feature, read_artifact echoes); server instructions drop overmind://resources → list_entries pointer; upsert description lists per-kind XML sections + order semantics; create_feature/upsert descriptions state feature deletion is UI-only; pins for all texts. Files: src/Overmind.Host/Api/Mapping (or projection sites), src/Overmind.Mcp (Program instructions, OvermindPlanningTools descriptions), SessionBootstrapPrompt if needed, tests. Review: light.
4. (R8) delete_entry tool: entryId + projectId resolution chain (020 pattern); DELETE via the existing entries route; guards: kit-linked → 409 KitOwnedEntry, skill-kind → 409; description states permanence + no entry tombstones (029 owns that); contract pins (tool count, note deleted, kit-linked 409, skill 409, description text). Files: src/Overmind.Mcp (entries tool class), Program.cs registration, tests/Overmind.Mcp.Tests (HandshakeContractTests count grows). Review: light.
</task-breakdown>

<verification>
- dotnet build 0 warnings + dotnet test green after each task; light review per task.
- Live dogfood on the isolated host: uniform echo on all seven mutation tools (one representative call each, key-set asserted), re-tick no-op text, feature updatedAt movement visible via board/list, delete_entry round-trip (create note → delete → gone; kit-linked → 409).
- 068/077 grep gates green; 086 (web time fix) lands independently.
</verification>

<refs>
- Spec cda4c5c7 (validated via the 077 proof flow — the first spec validated with a bell-reply proof, notification f6b71049).
- 037 echo lineage + obstacle (empty-200); 020 tool pattern; 082-п.3 duplicate closure; 064/010 board surfaces untouched (backend echo changes do not alter web shapes).
</refs>

<log>
- 2026-09-26 — plan drafted; spec validated through the 077 proof flow (first live use). Plan gate delegated by the user's "поехали кодить" mandate; a fresh bell-reply proof will be requested for plan validation per the 077 freshness rule.
- 2026-09-26 [agent] — validated with human proof: notification 1ea5a0e2-0a01-4d64-bdba-a5d754294c07 replied 2026-09-26 16:29:17 UTC
</log>