---
feature: 070
task: 03
slug: response-polish-round3
status: done
review: light
created: 2026-09-26
brief: review scoped to tasks; instructions/descriptions fixed (resources pointer, XML sections, order, UI-only deletion)
---

# 070/03 — Review scoping + docs: instructions, sections, order, UI-only deletion

<goal>
Spec R3, R5–R7, R9: review field only on tasks; docs point at things that exist.
</goal>

<scope>
- Review field only for kind=task in artifact projections (read_feature artifacts, read_artifact echoes) — spec/plan/result payloads stop carrying it; pin both sides.
- MCP server instructions: drop the dead overmind://resources pointer → working pointer (list_entries / onboard payload); pin.
- planning_upsert_artifact description: expected XML sections per kind (task: goal/scope/anti-scope/constraints/acceptance-criteria/refs/obstacles/log + optional brief; document: free-form + <log> machine-parsed) + order semantics (1-based sort key, drives current_task picking, omitted appends after max); pin.
- planning_create_feature + upsert descriptions: "features cannot be deleted or archived via MCP — the UI owns feature deletion"; pin.
- Mark 082-п.3 closed by this work (append a line to 082's feature log via planning_append_log on the feature's spec? — no: record the closure in this task's log; the orchestrator handles 082 body at close).
</scope>

<anti-scope>
- Host endpoints (01), adapter projection (02), delete_entry tool (04).
</anti-scope>

<constraints>
- .NET 10 / C# 14; no comments; English-only; no git commands; dotnet build 0 warnings + dotnet test green; 068/077 grep gates green.
</constraints>

<acceptance-criteria>
- [x] read_feature/read_artifact: review present on tasks, absent on spec/plan/result (pins)
- [x] Instructions carry no overmind://resources; working pointer present (pin)
- [x] Upsert description lists per-kind sections + order semantics (pin)
- [x] Feature-deletion UI-only note in create_feature + upsert descriptions (pin)
- [x] dotnet build 0 warnings + dotnet test green
</acceptance-criteria>

<refs>
- Spec cda4c5c7 R3/R5–R7/R9; 082-п.3 duplicate; 032 pin-test pattern.
</refs>

<obstacles />

<log />

<log>
- 2026-09-26 [agent] — 070/03 picked up: review field scoped to tasks; instructions/descriptions fixed (resources pointer, per-kind sections, order semantics, UI-only deletion)
- 2026-09-26 [agent] — Coded (dotnet-coder): R3 — planning_read_feature and planning_read_artifact now project review scoping at the adapter (host passthrough replaced): the review field survives only on kind=task payloads, spec/plan/result stop carrying it; descriptions state "The review field appears on task payloads only"; pins cover both sides (task keeps review on read_artifact + read_feature; spec drops it; two exact-string description pins updated). R5 — server instructions in Program.cs drop the dead overmind://resources pointer; the working pointer is list_entries (type=gate|guard|note|decision|skill) + read_entry with the onboard payload naming the active project; handshake pin asserts no dead uri and both working pointers. Second half of the dead pointer found and fixed: the host onboard quickstart (OnboardQuickstart.Text) also told agents to read via overmind://resources — replaced with the list_entries/read_entry pointer (this was the twin of the 082-п.3 item). R6 — planning_upsert_artifact description lists per-kind expected XML sections (task: goal/scope/anti-scope/constraints/acceptance-criteria/refs/obstacles/log; spec/plan/result: free-form with <log> machine-parsed) and order semantics (1-based sort key driving planning_current_task picking and roster ordering; omitted appends after the current max). R7 — same order semantics added to planning_upsert_tasks. R9 — create_feature + upsert descriptions state features cannot be deleted or archived via MCP — the UI owns feature deletion. Pins: 8 new description fragments (ToolsList), read-scoping pins x3, instructions handshake pin. 082-п.3 closure recorded here per the task scope — both dead-pointer sites (instructions + onboard quickstart) are gone; the orchestrator handles the 082 body at close. Gates: 068 frozen-sweep and 077 proof-line green. Build 0 warnings; tests 1518/1518 (Core 215 / Infra 496 / Api 447 / Mcp 360, +3).
- 2026-09-26 [agent] — 070/03 done by dotnet-coder: review field scoped to tasks in read_feature/read_artifact echoes; instructions + onboard quickstart drop overmind://resources in favor of list_entries/read_entry; upsert/upsert_tasks descriptions carry per-kind sections + order semantics; create_feature/upsert state feature deletion is UI-only. 082-п.3 closure recorded in the task log. Evidence: build 0 warnings, tests 1518/1518 (Core 215 / Infra 496 / Api 447 / Mcp 360), 068/077 gates green. All 5 AC ticked; ready for light review.
- 2026-09-26 [agent] — Fix round W1 (review-070-03, applied verbatim): the order-semantics sentence in planning_upsert_artifact and planning_upsert_tasks descriptions no longer claims 1-based — the code is 0-based on append (ResolveOrderAsync returns 0 on an empty feature, StageTask appends 0, validator accepts Order >= 0, first append materializes 00-slug.md). New sentence in both descriptions: "Order is a sort key within the feature — lowest order first; it drives planning_current_task picking; omitted order appends after the current max (first append lands on 0; materialized backlog files are numbered 1-based)." The two ToolsList fragment pins updated to "lowest order first" + "first append lands on 0; materialized backlog files are numbered 1-based". S1/S2 untouched (microbatch). Evidence: build 0 warnings, tests 1518/1518 (Core 215 / Infra 496 / Api 447 / Mcp 360) on a clean full-suite rerun — one unrelated ToolContractTests flake (process-spawn timing) passed in isolation and on the rerun. Task status stays done.
</log>
