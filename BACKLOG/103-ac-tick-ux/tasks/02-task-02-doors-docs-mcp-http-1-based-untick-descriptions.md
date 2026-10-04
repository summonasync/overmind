---
feature: 103
task: 02
slug: ac-tick-ux
status: todo
review: light
created: 2026-10-04
depends_on: 01
brief: Doors: MCP/HTTP 1-based acIndex + untick + loud descriptions, pins rewritten
---

# Task 02 — Doors + docs: MCP/HTTP 1-based + untick + descriptions

<goal>
Expose 1-based indexing and un-tick through both doors and make the contract loud in descriptions.
</goal>

<scope>
- MCP planning_patch_artifact: acIndex param 1-based, untick operation (untick + acText/acIndex), description states «acIndex is 1-based; numbering matches the materialized mirror; acText is the recommended selector» verbatim.
- HTTP door: same semantics, echo fields in response contracts.
- 0-based pins rewritten (no compat shims); contract tests both doors; description pins.
</scope>

<acceptance-criteria>
- [ ] MCP: tick by 1, untick by text — contract-tested; descriptions pin the 1-based line.
- [ ] HTTP: echo fields in contracts, door tests.
- [ ] No 0-based assertions left in tests.
</acceptance-criteria>

<refs>
- Spec 3f42d84f; plan 4a117b99 (task 2).
</refs>