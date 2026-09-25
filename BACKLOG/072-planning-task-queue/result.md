---
feature: 072
slug: planning-task-queue
status: validated
created: 2026-09-25
validated: 2026-09-25
---

# Result 072 — Planning task queue: MCP-тул «что брать следующим» — проекция борды (приоритет asc, blocked-aware, readiness), юзер рулит приоритетами с борды

<summary>
Shipped in full (7 tasks, 34+ files): the dashboard board IS the queue — QueuePosition (int, DB-only, never materialized) orders features by priority asc → position asc; the human drags cards within one priority band (native HTML5 DnD, optimistic + snapshot rollback, zero new packages); the agent reads planning_task_queue (ReadOnly+Idempotent, readiness 0-3 ordinal, direct blocker slugs, current/blocked flags), moves features with planning_move_feature (up/down/first/last/toPosition, band-scoped, edge no-ops), and changes bands with planning_set_feature_priority (approval-gated per description). Tail auto-assign covers create (API+MCP), import, and priority-band changes; migration backfilled 1..N per band deterministically (createdAt → slug tiebreak, cross-project pinned by a mutation-proven test). Ceremony texts state the mechanic in plain language: tool descriptions, bootstrap QueueRouting line, onboarding quickstart step 2, CONTEXT.md (tool count corrected to the truthful 21 planning / 30 total), web dashboard description, dotNetCrew note (bbf605de). Spec c2f0c760 v9 + plan 86f6a90e validated; 079-feature-request-entry-type taken as intake during the session.
</summary>

<evidence>
- Suite: 1344 → 1415 passing (Core 212, Infrastructure 458, Api 406, Mcp 339); dotnet build 0 warnings throughout; web lint 0 errors / tsc clean / build green; package.json untouched.
- Reviews: 5 light per-task (01: W1+W3 fixed; 02: W1+W2 band-load-inside-tx fixed; 03: WARNING-1 PascalCase error keys + 5 nits fixed; 04: W1 fixture-verbatim fix; 05 web: 0 actionable) + 1 HARD cross-wave gate (audits/architect-review-2026-09-25-072.md: found the band-load tie-break seam invisible to per-file reviews — ThenBy(Slug) + duplicate-position test added; found two stale "agent door only" claims in CONTEXT.md — rephrased). All under audits/.
- Live walk (isolated host, scratch HOME, port 4799, killed by port PID): seed 5 features → queue read correct (omega p0,1; alpha/beta/gamma p1,1-3 by createdAt; delta p2,1) → gamma up swaps → beta/omega edge no-ops → alpha down swaps the real neighbor → beta first → alpha last no-op → gamma toPosition 3 exact → 4 contract-violation shapes return PascalCase-keyed 400s with the verbatim range text ("the valid range is 1..3") → PUT priority p0→p1 re-tails omega at the p1 tail → human-door full-band reorder [omega,gamma,beta,alpha] → positions 1..4 → partial list 400 names "the list is missing: beta, gamma, omega" → limit=2 truncates, limit=0 → 400 → materialize shows NO queuePosition in frontmatter (DB-only, live disk) → disk priority flip p1→p2 + re-import keeps omega's position 1 → the delta/omega position tie resolves slug-first (hard-review fix visible live) → pin isCurrent flags without re-sorting.
- Deviations from plan: task order 06/07 swapped (docs before closeout so the hard gate + result see the final state); dotnet-ef CLI broken in WSL — migration trio hand-written, proven by a real-SQLite migration test (parity check from Windows suggested at next deploy).
</evidence>

<pending>
Smoked by the user live (2026-09-25, in-session): "в остальном — идеально" — drag persistence, band scoping, rollback behavior all confirmed by hand. One finding: mouse-wheel scrolling is dead during an active HTML5 drag (browser-native DnD swallows wheel events) — with a long board, targets below the fold are unreachable mid-drag. Filed as task 08 (c64299b7, edge auto-scroll loop, no new packages), in flight. Feature stays in progress until 08 lands and the user re-confirms.
</pending>

<refs>
Spec c2f0c760 v9 (validated); plan 86f6a90e (validated); tasks a76162fe/b5754442/98a56b78/656d8e71/99c9c02e/9811e6eb/e56bc5fe; audits review-072-01..04, next-js-code-reviewer-072-05, architect-review-2026-09-25-072; dotNetCrew note bbf605de; intake 079-feature-request-entry-type.
</refs>