---
feature: 029
task: 03
slug: entry-tombstones
status: done
review: light
created: 2026-10-04
depends_on: 02
brief: Closeout: quickstart line, architect light, result + click-check
---

# Task 03 — Closeout: quickstart + architect + result

<goal>
Close the wave: discovery line, architect, result.
</goal>

<scope>
- Onboarding quickstart line: check tombstones before proposing anything that smells like a resurrect.
- Architect light; result with post-release click-check (delete an entry via MCP with reason → tombstone appears, listable, searchable).
</scope>

<acceptance-criteria>
- [x] Quickstart line pinned; architect verdict recorded; result drafted with click-check.
</acceptance-criteria>

<refs>
- Spec 9a3cef1f; plan 241a8ad0 (task 3).
</refs>

<log>
- 2026-10-05 [agent] — [agent] Taken by orchestrator (tasks 01+02 done). Closeout items landed by orchestrator (8445289): quickstart tombstone line (pinned, Api 631) + honest update_entry description (review-029-02-mcp N2). Dispatching architect light over the whole wave.
- 2026-10-05 [agent] — [agent] Closeout complete: quickstart line pinned (8445289), update_entry description honest, architect light APPROVED-WITH-WARNINGS with W1 (ServerInstructions knew the old type set) + N1 (reason param honesty on tombstone targets) fixed in 95ce4c1; bury-not-erase verified across every reachable delete path. Result 8e3df13b drafted with click-check. AC ticked. Closing task.
</log>
