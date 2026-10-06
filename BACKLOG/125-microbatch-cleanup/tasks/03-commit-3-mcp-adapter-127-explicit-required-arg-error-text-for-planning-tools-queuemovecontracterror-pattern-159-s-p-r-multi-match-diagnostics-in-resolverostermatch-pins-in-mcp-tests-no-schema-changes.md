---
feature: 125
task: 03
slug: microbatch-cleanup
status: done
review: hard
created: 2026-10-06
brief: Commit 3 (MCP adapter): #127 explicit required-arg error text for planning_* tools (QueueMoveContractError pattern) + #159 S/P/R multi-match diagnostics in ResolveRosterMatch; pins in Mcp.Tests. No schema changes.
---

<goal>
Commit 3 of the wave: MCP-adapter batch — #127 explicit required-argument error text for planning_* tools, #159 multi-match diagnostics for S/P/R aliases. Both with pins.
</goal>

<scope>
- **#127:** `src/Overmind.Mcp/OvermindPlanningTools.cs` — explicit required-argument checks on planning_* tools producing a contract-violation error that names the argument and the rule, e.g. «projectId is required (required for planning_* tools — entries tools resolve from cwd, planning tools do not)». Follow the existing `ContractViolation` pattern — grep `QueueMoveContractError` for the in-repo precedent. Minimum coverage: the projectId-bearing reads from the field case (`planning_task_queue`, `planning_list_features`); extend the same one-liner where trivial.
- **Pin #127:** `tests/Overmind.Mcp.Tests/PlanningToolContractTests.cs` — the field-case calls return the explicit error text, not the bland wrapper message.
- **#159:** `ResolveRosterMatch` in `src/Overmind.Mcp/OvermindPlanningTools.cs` — duplicate S/P/R documents (spec@0 + spec@1 is data-legal) must yield explicit diagnostics naming the candidates (feature slug + both orders) instead of a silent first-match pick. Non-duplicated data keeps current behavior.
- **Pin #159:** Mcp.Tests — alias resolution against a feature with two specs produces the diagnostic.
- Commit: one focused commit; tombstones #127, #159 immediately after.
</scope>

<anti-scope>
- No schema changes, no backend kind-uniqueness constraint (the ledger's alternative direction is deliberately not taken this wave).
- No `ArtifactAliasResolver` extraction (#165a) — keep changes local to the two functions.
- No tool-description rewrites beyond the required-arg checks.
</anti-scope>

<constraints>
- Strictly sequential: after T2.
- `dotnet test` scoped to Overmind.Mcp.Tests; full suite at wave close (serialized, infra #158).
- Tombstone mechanics as in T1/T2 (`[CLOSED 2026-10-06, коммит <hash> — …]` in the ledger file right after the commit).
- No `git push`. English-only. Load `dotnet-best-practices` — review is hard and cites BP-IDs.
</constraints>

<acceptance-criteria>
- [x] #127: planning_* calls missing a required argument return the explicit contract-violation text naming the argument; the field case is pinned in PlanningToolContractTests.
- [x] #159: duplicate S/P/R documents produce multi-match diagnostics naming candidates; single-document resolution unchanged.
- [x] Existing adapter contract tests stay green (no regression in alias resolution or tool wiring).
- [x] Ledger #127 and #159 tombstoned with this commit's hash.
- [x] `dotnet test` on Overmind.Mcp.Tests green.
</acceptance-criteria>

<refs>
- Spec: cdb6854b-01ac-410c-82fc-8f13e706cb7d — legacy pool #127, #159.
- Plan: e3cbeb50-ac62-4d36-8fc8-d4cbb379dd3d — Commit 3.
- Ledger: `/home/johan-boolman/.claude/projects/-mnt-Ubuntu-Dev-Projects-AI-Tools-Overmind/memory/overmind-microbatch-queue.md` — #127 (полевой кейс 2026-09-28), #159 (090/03 NIT-1).
- Sources: `audits/review-090-03.md` (#159); #127 exists only via the ledger.
</refs>

<obstacles>
- `OvermindPlanningTools.cs` is a 1200+ line monolith by design — keep the diff surgical; the extraction refactor (#165a) is anti-scope.
- If the ContractViolation pattern lives in a shared helper rather than QueueMoveContractError itself, reuse the helper — do not fork a second pattern.
- 2026-10-06 (T3): the required-arg doors (list_features/task_queue/materialize_status) return `Task<JsonElement>`, so the ContractViolation(CallToolResult) helper does not apply without return-type churn — the check throws `McpException(ProjectIdRequired)` off a shared const instead; wire shape is identical (IsError + text), same const-reuse discipline as the obstacle above.
- 2026-10-06 (T3): multi-match coverage went to the reads-only trio whose ONLY required argument is projectId (the exact field-case shape); multi-arg doors (read_feature, list_artifacts, current_task, dependencies) keep the SDK-required schema — extending there is a wider contract change than the one-liner this wave prescribes.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan (Commit 3); review: hard (contract/pin semantics).
- 2026-10-06 [agent] — T3 coding round started
- 2026-10-06 [agent] — coding round complete — hard review gate
- 2026-10-06 [agent] — hard review APPROVED — 0 blockers. Obstacle (a) McpException-vs-ContractViolation judged honest (dominant file pattern, wire shape identical); obstacle (b) scope line judged a faithful fix, not under-coverage. WARNING (brief said "no schema changes", tools/list required-removal is the only mechanism to AC#1 — documented in obstacles) and SUGGESTION (planning_materialize mutation door still opaque — filed to ledger #245) both non-blocking. Commit 04104b9, Mcp.Tests 498/498.
</log>