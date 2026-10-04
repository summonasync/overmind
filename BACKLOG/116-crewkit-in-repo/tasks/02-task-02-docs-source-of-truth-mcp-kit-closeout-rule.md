---
feature: 116
task: 02
slug: crewkit-in-repo
status: todo
review: light
created: 2026-10-04
brief: CONTEXT.md: kit source of truth + MCP→kit closeout rule
---

# Task 02 — Docs: source of truth + MCP→kit closeout rule

<goal>
CONTEXT.md states the kit is the in-repo source of truth and codifies the user's discipline: MCP-surface changes land their crew-kit docs sync in the same wave.
</goal>

<scope>
- CONTEXT.md: kit location (src/OvermindCrewKit, own git/origin), one-way flow (repo → bundle → home, never home → bundle), pack.sh default change.
- The closeout rule as a named discipline: any feature touching MCP surface (tools, gates, statuses, quickstart/instructions texts) must carry a crew-kit docs sync step — WITHOUT auto-copying texts between C# constants and markdown (099-W1/094-W1 drift precedents cited).
</scope>

<anti-scope>
- No other docs files; no kit-internal SYNC-STRUCTURE changes (that file is the kit repo's own contract).
</anti-scope>

<acceptance-criteria>
- [ ] CONTEXT.md carries both statements; wording consistent with the 114 experience (first in-repo kit exercise).
</acceptance-criteria>

<refs>
- Spec 8a2e649f; CONTEXT.md; 114 result (precedent).
</refs>

<log>
- 2026-10-04 [agent] — task cut post-validation; orchestrator-executed (markdown only).
</log>