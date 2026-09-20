---
feature: 034
slug: feature-priorities
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 034 — Feature priorities: прио как данные (поле + сортировка/фильтр борды + MCP + round-trip)

<summary>
Feature 034 done: priorities are first-class data on the board. FeaturePriority enum P0-P3 (P2 default) persisted, backfilled by migration, settable at creation through both doors (API + MCP planning_create_feature) and after creation through the human door (PUT / web editor); lowercase wire format pinned by raw-wire tests; frontmatter `priority: pN` round-trips through feature.md with visible unknown-value downgrade; web renders priority chips (p0 danger / p1 warning / p2 neutral / p3 muted), sorts the board (priority asc, updatedAt desc, slug tiebreak), filters by priority, and edits priority in the feature form. Live-proven on prod after deploying 0.1.9 bundle 4: the real queue heads now carry P1 and the disk tree shows it.
</summary>

<tasks>
01 — Backend core (d1fd7cc): enum + migration w/ P2 backfill + both create doors + lowercase wire pins + HasSentinel(P2). Review PASS (audits/review-034-01.md), 985/985.
02 — Frontmatter round-trip (1fb1463): emit after status, disk-wins import (verified as the existing status contract), unknown→P2 with report line, byte-stability. Review PASS (audits/review-034-02.md), 989/989.
03 — Web surface (24cd326): priority-tone + BadgeTone "muted", composite sort (pin demoted by design — identification, not position; spec-sanctioned), 023-pattern filter, editor select. Review PASS (audits/review-034-03.md); lint/tsc/build green.
04 — Live dogfood: deployed 0.1.9 bundle 4 (pack verified fresh, host restarted by port PID, migration ran on startup), set P1 on 026/029/028/030/034 via human-door PUT, verified all three response surfaces + disk frontmatter round-trip (p1 on queue heads, p2 default on untouched 037), materialize 221 files zero warnings.
</tasks>

<evidence>
- Tests 969 → 989 (+20 across Core/Infra/Api/Mcp). Build 0 warnings; web gates green.
- Live: PUT/GET/list carry priority; five P1 members exactly as mapped; disk frontmatter matches board; no clobber-guard noise on the materialize cycle.
- Dogfood trap caught: first PUT wave was silently ignored by the pre-034 prod build (unknown JSON field + old response contract) — recognized before "fixing" a non-bug; re-run after deploy.
</evidence>

<deviations>
- None against contracts. Design call surfaced and upheld at review: pinned feature no longer sorts first (composite sort replaces isCurrent-first; current remains visible via primary frame + badge + currentOnly filter).
</deviations>

<follow-ups>
- Microbatch items 32-34 (TryParseEnum duplicate; artifact-level priority swallow via global StructuralKeys; ISO sub-second compare edge).
- Web test harness does not exist in the repo at all — surfaced as a general frontend gap, candidate for its own feature if wanted.
- 035-execution-queue remains the fine-grained ordering layer above these coarse priorities.
</follow-ups>

<log>
</log>