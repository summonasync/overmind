---
feature: 010
slug: planning-board-filters
status: validated
created: 2026-09-18
validated: 2026-09-18
---

# Result 010 — Planning board: filters (tags/status/state) + current feature highlight

<summary>
Shipped: CURRENT pin flag (IsCurrent) with a single-current-per-project invariant in PlanningFeatureService; POST .../features/{slug}/current on both doors + MCP planning_set_current (16 tools surface); web pin button with card accent and current-first sort; board filter bar (status multi-select, spec/plan chip filters, current-only toggle) with URL search-param state, AND composition, zero-match reset.
</summary>

<evidence>
- Tests: 558/558 green (Core 103 / Infrastructure 224 / Api 170 / Mcp 61); baseline was 544.
- Web: lint 0 errors, tsc clean, static build green.
- Reviews: 01 PASS / 02 APPROVED / 03 PASS / 04 PASS (audits/next-js-code-reviewer-010-03, -010-04); NIT sweep applied in-session (Canned fixture title, unpin API test, cn() cleanup).
- Live dogfood on 0.1.2 (bd6f5b8): migration applied on the live profile; planning_set_current pinned 009 via the MCP adapter; pinning 010 unpinned 009 — invariant holds through the MCP door; board response carries isCurrent; materialize clean (79 files, no warnings, git tree unchanged).
- WARNING-1 from review 04 (useRouter.replace on static export) — runtime click-check handed to the user; code-side fallback noted (history.replaceState) if it silently no-ops.
</evidence>

<deferred>
- Review 01 WARNINGs: tests blind to skipped SaveChanges (shared-context identity map) — file-wide test pattern; pin-pin race between concurrent calls (same accepted class as single-active-kit; filtered unique index when API leaves loopback); PlanningFeatureService at 360 lines (BP-91 watch).
- Review 02 NITs remaining: PlanningFeatureResponse carries no IsCurrent echo (clients re-read the board).
- Review 04 NITs: double useBoardFilters instantiation; raw status literals in chips; hardcoded /planning path in the filter hook.
- Known flake hit during the session: Mcp.Tests port race (StubOvermindApi.TakeFreePort) — already in BACKLOG Deferred.
</deferred>