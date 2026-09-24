---
feature: 030
slug: feature-dependencies
status: draft
created: 2026-09-24
---

# Result 030 — Feature dependencies: structured blockedBy/blocks (не текстом в body)

<goal>
Delivery record for 030-feature-dependencies: structured blockedBy/blocks with MCP-door enforcement, dedicated editors (UI panel + MCP tools), and materialization round-trip — all six tasks done and reviewed.
</goal>

<context>
Mindlash feedback 2026-09-20 (deps as text in bodies) hardened twice by the user on 2026-09-24: enforcement + dedicated editing surfaces. Field notes BobHouse 020 (B7) confirmed this as the single highest-value structural gap versus md-files.
</context>

<scope>
Shipped end-to-end across six tasks:
1. Storage: PlanningFeatureDependency link table (composite PK, EntryTag idiom), blocks side derived in memory, additive migration (apply-on-live-DB pinned).
2. Service: FeatureDependencyService — add/remove/list, DFS cycle rejection (direct + transitive, full chain in error, e.g. "alpha -> delta -> gamma -> beta -> alpha"), closure rule done|review computed at read time.
3. API: PlanningDependenciesApi handlers shared by both doors; isBlocked/blockedBy/blocks on feature DTOs (additive, wire-compatible); cycle → 409 DependencyCycle, unknown slug → 404, empty → 400.
4. MCP + enforcement: planning_add/remove/list_feature_dependency tools; current_task returns a stop state naming unclosed blockers (reads stay open, artifact writes stay open by design — spec anti-scope); planning_set_current refuses pins on blocked features (409 FeatureBlocked, STOP-prefixed; manual CallToolResult render preserves the STOP line); human door never blocked; unblocked behavior unchanged (pinned).
5. Web UI: Dependencies panel in the feature workspace (picker, danger/muted chips, read-only Blocks section), blocked badge on dashboard cards (indication only), "Blocked" board filter with URL param blocked=1.
6. Materialization: depends_on frontmatter key via the registry, sorted-ordinal canonical projection, additive import with linked:/unlinked: report, blockers-first folder ordering, byte-stable round-trip into a fresh database.
</scope>

<acceptance-criteria>
All spec ACs verified per task artifacts (96eb93a2, 19dfbb11, 9f588e30, a62ada15, 2a71fbf5, b8cbcc51 — done, review evidence in each log). Final suite state: Core 203/203, Infrastructure 427/427, Api 382/382, Mcp 318/318; build 0 warnings; web lint/tsc/build clean.
</acceptance-criteria>

<refs>
audits/review-030-01..06.md (tasks 1-6; task 4 hard review APPROVED WITH WARNINGS); spec 7badf229; plan 8075355f; follow-ups: microbatch #68-#72 (graph-scale tradeoff, service/API test gaps, import ordering perf, BacklogImporter split, web invalidation width, unwrapped OpenBlockersAsync)
</refs>

<obstacles>
Accepted semantics worth remembering: (1) enforcement is POINT-IN-TIME — a blocker closing between the check and the stop-state render yields a stale stop that self-heals on the next issuance check; a pin granted before a feature becomes blocked is not recalled (hard-review W1, by design). (2) The current-task resolver physically lives in AgentPlanningApi (Host), not Infrastructure as the plan sketched — enforcement added minimally in place. (3) Feature pin via set_current on a blocked feature is refused only on the agent door; pins and status moves through the human door are always open.
</obstacles>

<log>
- 2026-09-24 [agent] — result recorded after all six tasks closed with review verdicts (last: hard review 030-04 APPROVED WITH WARNINGS, 0 blockers)
</log>