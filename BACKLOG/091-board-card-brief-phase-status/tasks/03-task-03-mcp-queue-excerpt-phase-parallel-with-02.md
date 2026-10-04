---
feature: 091
task: 03
slug: board-card-brief-phase-status
status: done
review: light
created: 2026-10-03
depends_on: 01
brief: planning_task_queue excerpt + phaseLabel fields + description + contract tests
---

# Task 03 — MCP queue excerpt + phase (parallel with 02)

<goal>
Agents get the same one-glance context: planning_task_queue entries carry excerpt + phase label.
</goal>

<scope>
- planning_task_queue entries: excerpt + phaseLabel fields (null-safe for bodyless features) — the queue projection already carries the backend fields from task 01; update the tool DESCRIPTION to document the new fields.
- Contract tests: entries carry excerpt/phaseLabel when present, null for bodyless; description asserts mention both fields.
</scope>

<anti-scope>
No other tool changes, no backend, no web.
</anti-scope>

<constraints>
English-only, no comments by default, no auto-commits; Mcp test project + full suite real numbers.
</constraints>

<acceptance-criteria>
- [x] dotnet build 0/0 + full dotnet test green
- [x] Queue entries carry excerpt + phaseLabel; null-safe for bodyless
- [x] Description documents both fields
- [x] Contract tests pin presence/absence
</acceptance-criteria>

<refs>
- Spec: d066ec12 (Surfaces).
- Task 01 (order 1) — derivation.
</refs>

<obstacles>
- FIELD NAME: the task brief called the field phaseLabel; the live HTTP payload from task 01 exposes it as phase (camelCase of the contract property). The tool is a passthrough — kept the server name phase so HTTP, MCP and web (task 02/04) all speak one name. Recorded so nobody renames one surface only.
- Null JSON properties surface as C# null JsonNode in tests — asserting absence uses .ShouldBeNull() on the property (no GetValue), presence uses GetValue<string>() after null-forgiving access. Test-side single() results also get the null-forgiving operator (CS8602 otherwise).
</obstacles>

<log>
- 2026-10-03 [agent] — dispatched to dotnet-coder (same session — built the derivation these tools expose), parallel with 02 (disjoint: Mcp vs web)
- 2026-10-03 [agent] — Executed by dotnet-coder. planning_task_queue description documents the two new entry fields — phase (drafting | writing spec | planning | plan validated | in progress | review | done — derived server-side, lifecycle column stays the source of truth) and briefExcerpt (first <goal> paragraph, ≤150 chars, null when no goal) — appended to the compact-fields enumeration after the claim-state sentence. Contract tests: PlanningTaskQueue_ReadsTheAgentQueueRoute extended — claimed row (demo-feature) asserts phase="in progress" + briefExcerpt verbatim, bodyless row (draft-one) asserts phase="drafting" + briefExcerpt null; description asserts briefExcerpt + "plan validated" present. Canned queue payloads refreshed to the live contract (isCurrent → claim fields + phase/briefExcerpt; draft-one is the natural bodyless fixture). Thin passthrough — zero adapter logic. Gates: dotnet build --no-incremental 0 errors / 0 warnings; sequential full run 1859 passed / 0 failed (Core 274, Infra 588, Api 582, Mcp 435); format clean outside Migrations.
- 2026-10-03 [agent] — task 03 executed to review by dotnet-coder: planning_task_queue description documents phase + briefExcerpt (server-derived, one-glance ceremony context), contract tests pin claimed-row presence (phase/briefExcerpt verbatim) and bodyless null-safety (draft-one), canned queue payloads refreshed to the live payload contract. Thin passthrough — zero adapter logic; OvermindPlanningTools description-only edit. Gates: dotnet build --no-incremental 0 errors / 0 warnings; sequential full run 1859 passed / 0 failed (Core 274, Infra 588, Api 582, Mcp 435); format clean outside Migrations.
- 2026-10-03 [agent] — 2026-10-04 [agent] — reviewed by dotnet-code-reviewer: PASS-WITH-NITS — 091/03 queue ceremony-context description accurate, passthrough presence/absence pinned, queue fixtures brought current (dead isCurrent out, 099 claims + claimedByHuman in — stale since that wave); 1 NIT (board fixture PlanningBoardJson still stale with isCurrent + asserts it — pre-099 drift, closeout item); build 0/0, Mcp 435/435; report audits/review-091-03.md. Fix loop dispatched with the closeout.
- 2026-10-03 [agent] — delta review PASS-WITH-NITS (audits/review-091-03.md): description accurate, passthrough pinned, fixtures current; AC 4/4. Closing as done; board-fixture nit routed to closeout.
</log>