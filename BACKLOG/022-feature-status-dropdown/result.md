---
feature: 022
slug: feature-status-dropdown
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 022 — Feature status dropdown on the dashboard card

<outcome>
022-feature-status-dropdown shipped: the board card's static status Badge became an inline tone-styled Select (draft/ready/inProgress/done) with an optimistic PUT through the existing human door (updatePlanningFeature), per-card scoped cache update matched by slug, snapshot rollback on failure, and board + feature-detail invalidation on settle (the detail key keeps the workspace header select in agreement). Zero backend changes — the feature rode the existing PUT contract end to end.
</outcome>

<evidence>
- Reviews: audits/review-022-01.md — PASS-WITH-NITS, 0 blockers; all three coder deviations accepted with verified justifications (ErrorText instead of a nonexistent toast system; deterministic raw select because cn() is a plain join where conflicting classes resolve non-deterministically; feature-detail invalidation required by the agreement AC).
- Reviewer re-ran tsc (0 errors) + lint (0 errors, 6 pre-existing warnings outside the change); coder proved build green (10/10 routes).
- NIT-1 (identical aria-labels across cards) fixed in-tree: "Feature status for ${item.title}" matching the file's own pattern (line 177 precedent); tsc + lint clean after the fix.
- React Query v5 idiom verified canonical: await cancelQueries before the optimistic write, getQueryData snapshot, setQueryData scoped to the matching slug (other cards untouched), guarded rollback in onError.
</evidence>

<deferred>
- Microbatch queue: snapshot rollback can clobber a concurrent foreign optimistic update on another card (self-heals via invalidation); third component in feature-board.tsx is an extraction candidate; grow a shared Select tone variant when a second tone consumer appears; a real toast system for web/ is a shared-layer story of its own.
- Live UI visual pass on the packed release (0.1.8) pending — backend untouched, e2e behavior code-reviewed.
- Transition awareness lands with 023-feature-lifecycle-flow (this dropdown shows all statuses by design until then).
</deferred>
