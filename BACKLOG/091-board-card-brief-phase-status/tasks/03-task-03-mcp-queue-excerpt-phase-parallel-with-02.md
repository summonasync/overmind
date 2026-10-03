---
feature: 091
task: 03
slug: board-card-brief-phase-status
status: todo
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
- [ ] dotnet build 0/0 + full dotnet test green
- [ ] Queue entries carry excerpt + phaseLabel; null-safe for bodyless
- [ ] Description documents both fields
- [ ] Contract tests pin presence/absence
</acceptance-criteria>

<refs>
- Spec: d066ec12 (Surfaces).
- Task 01 (order 1) — derivation.
</refs>

<obstacles></obstacles>

<log></log>