---
id: 500f8197-c842-45aa-8acc-f09c0e96217c
type: note
title: Agent-side friction log (Qwen Code, Bob.AI sessions, 2026-10-03/04)
tags: agent-ux, dogfood, friction, qwen-code
createdAt: 2026-10-03T21:23:49Z
updatedAt: 2026-10-03T21:23:49Z
---

# Agent-side friction log from Bob.AI-daily-assistant sessions (Qwen Code runtime), 2026-10-03..04

Reporter: Qwen Code main agent, heavy Overmind consumer (board ceremonies run through this API every session). This is the product-UX counterpart of the squeezr adoption note (Squeezr board `f31ec28f`): Overmind is adopted precisely BECAUSE it is load-bearing (single source of truth, no grep-equivalent, loud STOP semantics) — so the friction below does not kill adoption, it just taxes every session. Each item is grounded in a real payload observed this period.

## 1. Deferred-MCP bridge taxes every call (runtime, but API design can soften it)

Every MCP call costs `tool_search` (schema) + `tool_call` = two round trips per family per session; schemas re-learned cold each session. Mitigation discovered this session: Qwen Code `tools.visible` pins named tools eager — recommend pinning the hot Overmind set (`overmind_onboard`, `planning_current_task`, `planning_read_artifact`, `planning_patch_artifact`, `planning_append_log`, `planning_upsert_artifact`, `planning_set_status`, `planning_list_artifacts`, `write_entry`, `read_entry`). Product-side angle: keep tool NAMES and schemas stable across releases — the visible-list is name-based, churn silently un-pins tools.

## 2. `acIndex` is 0-based while the human-facing list renders 1-based — silent wrong tick

Observed 2026-10-03: ticking AC by the number shown in the materialized mirror hit the neighbour item; the API ACCEPTED it (a wrong tick is a successful write). Only the out-of-range 400 revealed the truth — its diagnostics dump the visible list with real 0-based indices. Also: no un-tick operation — repair required replacing the whole `acceptance-criteria` section. Asks: (a) make `acIndex` 1-based to match rendering, or better accept `acText` exact-match as the primary selector; (b) echo back the ticked item text in the response so the caller sees WHAT was ticked; (c) an un-tick op.

## 3. Write timeout that still commits

`planning_patch_artifact` returned "overmind API is unreachable — HttpClient.Timeout of 10 seconds" while the patch HAD committed (verified by read-back showing the new version). Re-sending on timeout risks double-version churn. The inline materialization after each mutation is the slow part. Asks: move materialization off the request path (async pass), and/or on timeout-path errors state explicitly whether the mutation committed and its version.

## 4. upsert registers only some task sections

`<anti-scope>` and `<constraints>` ride in the body but `planning_patch_artifact` refuses them ("Sections present: goal, scope, acceptance-criteria, refs, obstacles, log"). Result: fixing one word in anti-scope costed a full ~8 KB body re-send through the model (and the re-send re-learned the section gap first-hand). Ask: register all task sections as patchable.

## 5. Parameter-name inconsistency

`planning_append_log` takes `note`; `planning_set_status`/`planning_patch_artifact`/`planning_upsert_artifact` take `logNote`. One wasted 400 per session learned it the hard way. (Also `planning_read_artifact` accepts only the artifact UUID — a human locator like `021/T15` (slug+order) would make briefs for sub-agents shorter and guess-a-UUID 404s rarer.)

## 6. Log hygiene vs append-only

Append-only `<log>` is the right doctrine (history rewritten only via full upsert, and Cyrillic prose occasionally leaks stray CJK tokens — every leak needs a dated CORRECTION line, four of them on 2026-10-03, permanent clutter). Compromise ask: allow editing the caller's OWN most-recent log line (same actor, same session, before anything references it) with a version trace — cuts the echo-line clutter without breaking auditability.

## What works — protect it

STOP semantics of `planning_current_task` (obey-or-ask, never silently work around); validation gates with frozen-artifact discipline (`logNote` on every validated write); `<refs>` as structured dependencies vs prose cross-references; materialized BACKLOG mirror with byte-identical no-op reporting; the 400-with-diagnostics error style (it is how the 0-based trap got caught). The smoothness of Overmind in agent flows is not despite friction — it is because there is no alternate path; documented as the "single source beats low friction" lesson in the squeezr adoption note.