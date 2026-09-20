---
feature: 017
task: 01
slug: live-ui-sync
status: done
review: light
created: 2026-09-20
---

# 017/01 — Live polling (15s refetchInterval on the live queries)

<goal>
The open UI picks up MCP-side changes within ~15 seconds without any manual refresh: a shared 15s refetchInterval lands on the planning board, feature detail (workspace incl. artifact bodies), entries list, and drift queries.
</goal>

<context>
Feature 017, the only task. Pure web — React Query v5 refetchInterval. The dogfood pain (2026-09-19): updates arriving via MCP were visible only after F5/Ctrl+Shift+R on the spec/plan/tasks tabs.
</context>

<scope>
- Shared constant (FSD-clean home — shared/lib unless the slice conventions dictate otherwise; name like LIVE_SYNC_INTERVAL_MS) = 15_000, consumed by all sites below; no other exports.
- web/src/features/planning/queries.ts — board queryOptions + feature detail queryOptions gain refetchInterval: LIVE_SYNC_INTERVAL_MS.
- web/src/features/entries/queries.ts (or wherever the list queryOptions live) — list queryOptions gain it.
- The drift panel's query (planning slice) gains it.
- Do NOT set refetchIntervalInBackground (default false is the decision). Do NOT touch mutations, invalidations, editors, tags, or the current-task query.
- Verify: npm run lint && npx tsc --noEmit && npm run build.
</scope>

<anti-scope>
- No SSE/WebSocket, no server changes, no background-tab polling, no optimistic concurrency.
</anti-scope>

<constraints>
- TypeScript strict; the constant is the single knob (no literal 15000 sprinkled).
- PARALLEL WORK: a backend coder is running dotnet in src/** — do not touch C# files; a planning-slice web task may follow (023/03) — keep queries.ts edits minimal and additive.
</constraints>

<acceptance-criteria>
- [ ] Four query options carry the shared interval; grep shows exactly one literal 15_000 in web/src.
- [ ] lint + tsc + build green.
- [ ] No behavior change beyond background refetch (mutations/editors untouched).
</acceptance-criteria>

<refs>
- Spec: feature 017, spec artifact (validated 2026-09-20) — interval, scope, exclusions.
- Pattern refs: features/planning/queries.ts (board/feature options), features/entries/queries.ts, the drift query in the planning slice.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — next-js-coder subagent (parallel with 023/01 backend coder; disjoint trees).
- 2026-09-20 — Coder done: LIVE_SYNC_INTERVAL_MS in shared/lib/live-sync.ts (single 15_000 literal, grep-verified), refetchInterval on board/feature/drift (planning) + entries list; versions/workflow/tags/current-task untouched; no refetchIntervalInBackground. lint 0 / tsc 0 / build green. To review.
- 2026-09-20 — Review PASS (audits/review-017-01.md): 0 blockers. Exactly four query options gained the interval; single 15_000 literal; no refetchIntervalInBackground; FSD clean; drift panel verified as the polling query's consumer with enabled-guard intact; polling vs optimistic mutations reasoned sound (theoretical board mid-mutation flicker accepted by spec decision). Observations: kits drift query + health 30_000 literal → microbatch candidates. Done.
</log>
