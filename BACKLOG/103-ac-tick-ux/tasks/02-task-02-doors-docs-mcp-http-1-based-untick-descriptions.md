---
feature: 103
task: 02
slug: ac-tick-ux
status: done
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
- [x] MCP: tick by 1, untick by text — contract-tested; descriptions pin the 1-based line.
- [x] HTTP: echo fields in contracts, door tests.
- [x] No 0-based assertions left in tests.
</acceptance-criteria>

<refs>
- Spec 3f42d84f; plan 4a117b99 (task 2).
</refs>

<log>
- 2026-10-04 [agent] — [agent] Light review passed (PASS-WITH-NITS — 1 WARNING closed by spec log-note about the alreadyTicked→noChange rename; nits #188 to microbatch ledger; 27-vs-28 file count: 27 is correct, coder miscounted). Gates confirmed by reviewer: build 0/0, Api 614, Mcp 446, Infra 615, Core 301, format clean. Code in 09b947f. Closing task; result deferred to closeout (task 03).
</log>
