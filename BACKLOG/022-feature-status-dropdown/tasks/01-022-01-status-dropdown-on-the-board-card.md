---
feature: 022
task: 01
slug: feature-status-dropdown
status: done
review: light
created: 2026-09-20
---

# 022/01 — Status dropdown on the board card

<goal>
The board card's static status Badge becomes an inline tone-styled Select (draft/ready/inProgress/done) that PUTs the status through the human door with an optimistic update, rollback + toast on failure.
</goal>

<context>
Feature 022, the only task. The board card renders at web/src/features/planning/components/feature-board.tsx (status Badge at :76, tone via featureStatusTone). The workspace header already has a status Select (feature-workspace.tsx:134–149) with its own update flow — reuse its API call and toast patterns, do not duplicate logic that can be shared cheaply.
</context>

<scope>
- feature-board.tsx: replace the status Badge with a compact Select — same options and casing the workspace Select uses; tone-colored styling via featureStatusTone (border/text); aria-label "Feature status"; disabled while that card's mutation is pending.
- Mutation wiring: updatePlanningFeature(projectId, slug, {status}) via useMutation — onMutate: optimistic update of ONLY that card in the board list cache (match by slug); onError: rollback (context snapshot) + error toast; onSettled: invalidate the board key. Reuse the slice's existing toast mechanism.
- Shared select options constant if the workspace Select has one inline — extract to lib if it keeps both DRY, else a small local constant is fine (judge by the existing code shape).
- Verify: npm run lint && npx tsc --noEmit && npm run build in web/.
</scope>

<anti-scope>
- No backend changes, no new endpoints or DTO fields.
- No transition validation (023 will own the state machine).
- No polling/live-sync (017).
- No changes to the workspace header Select beyond cheap shared-constant extraction if warranted.
</anti-scope>

<constraints>
- React Query v5 idioms (useMutation, queryClient.setQueryData snapshot/rollback); no useEffect fetching; TypeScript strict, no any; English-only.
- NOTE: parallel work may be happening in web/src/features/entries and shared/ui (feature 012) — do not touch those files; if lint surfaces warnings in files you did not change, ignore them.
</constraints>

<acceptance-criteria>
- [ ] Card status Select renders with the current status selected and tone styling; options draft/ready/inProgress/done.
- [ ] Changing it updates the card immediately (optimistic) and the board after settle — no edit modal.
- [ ] Failure (host down / 4xx) rolls the card back and shows an error toast.
- [ ] Workspace header and card dropdown agree after a change from either side (shared invalidation).
- [ ] lint + tsc + build green.
</acceptance-criteria>

<refs>
- Spec: feature 022, spec artifact (validated 2026-09-20).
- Pattern refs: feature-board.tsx:76, feature-workspace.tsx:134–149 (existing status select + update flow), features/planning/queries.ts (board key), api.ts updatePlanningFeature, status-tone.ts.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — next-js-coder subagent (web slot freed after 012/02; feature 012 backend task still running in parallel, disjoint sets).
- 2026-09-20 — Coder done: FeatureStatusSelect on the board card (tone-styled, aria-label, disabled-while-pending), optimistic scoped-by-slug cache update with snapshot rollback, board+feature-detail invalidation (needed for the header/card agreement AC). Deviations: no toast system exists in web/ — used the slice's ErrorText pattern under the select; raw <select> instead of shared Select (cn is a plain join — conflicting classes are non-deterministic, full deterministic class list chosen); feature-detail key also invalidated. lint 0 / tsc 0 / build green. To review.
- 2026-09-20 — Review PASS-WITH-NITS (audits/review-022-01.md): 0 blockers, 3 NITs + 1 suggestion. NIT-1 (identical aria-labels across cards) FIXED in-tree: aria-label now "Feature status for ${item.title}" matching the file's existing pattern; tsc + lint clean. NIT-2 (snapshot rollback can clobber a concurrent foreign card update — self-heals via invalidation), NIT-3 (third component in feature-board.tsx — extraction candidate), suggestion (shared Select tone variant when a second tone consumer appears) → microbatch queue. All three coder deviations ACCEPTED by review (ErrorText for absent toast infra; deterministic raw select vs non-deterministic cn class order; feature-detail invalidation justified by the agreement AC). Done.
</log>
