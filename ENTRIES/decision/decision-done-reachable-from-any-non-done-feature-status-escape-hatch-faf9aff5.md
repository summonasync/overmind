---
id: faf9aff5-b362-4fee-b739-b06266b467b2
type: decision
title: Decision: done reachable from any non-done feature status (escape hatch)
tags: lifecycle, planning, ux
createdAt: 2026-09-20T14:40:33Z
updatedAt: 2026-09-20T14:40:33Z
---

2026-09-20 — user decision, supersedes the 023 spec matrix rule "done reachable ONLY from review".

**What:** feature status `done` is reachable from draft/ready/inProgress/review via the human door (UI dropdown + PUT). `done→{}` unchanged (terminal). Auto-transitions still never set done.

**Why:** burying an abandoned feature required walking draft→ready→inProgress→review→done (4 steps). User hit "done не ставится" twice (2026-09-20 visual pass + escape-hatch discussion earlier). Single-user tool; the ceremony value of done-only-from-review lost to the one-click bury.

**Alternatives rejected:** separate `abandoned` status (honest on the board but more machinery: new enum member, tones, filters, materializer round-trip — user picked simplicity); done from inProgress only (half-measure, draft/ready still stuck).

**Execution:** task 023/07 on the planning board; the 023 spec matrix paragraph gets tombstoned via artifact amendment (logNote references this entry).