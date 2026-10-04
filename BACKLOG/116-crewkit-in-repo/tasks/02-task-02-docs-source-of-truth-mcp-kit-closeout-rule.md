---
feature: 116
task: 02
slug: crewkit-in-repo
status: done
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
- [x] CONTEXT.md carries both statements; wording consistent with the 114 experience (first in-repo kit exercise).
</acceptance-criteria>

<refs>
- Spec 8a2e649f; CONTEXT.md; 114 result (precedent).
</refs>

<log>
- 2026-10-04 [agent] — task cut post-validation; orchestrator-executed (markdown only).
- 2026-10-04 [agent] — [agent] Orchestrator-executed: CONTEXT.md Distribution gained the «Crew kit source of truth (116)» bullet — in-repo clone location, one-way repo → bundle → home flow, clone-layout CREW_ITEMS, dirty-kit warning, and the MCP→kit closeout discipline (no auto-copy; 099-W1/094-W1 cited). AC ticked. Awaiting its light review with task 01's.
</log>