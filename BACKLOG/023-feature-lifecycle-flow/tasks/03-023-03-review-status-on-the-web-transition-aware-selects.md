---
feature: 023
task: 03
slug: feature-lifecycle-flow
status: todo
review: light
created: 2026-09-20
---

# 023/03 — Review status on the web + transition-aware selects

<goal>
The web knows review: types/tone/lists gain it, and both status selects (022 board card, workspace header) offer only allowed targets (+current) from a shared lib transition map, rolling back on 400.
</goal>

<context>
Feature 023, web half 1. Depends on tasks 01 (API emits review, 400s on illegal moves) and lands AFTER 022 is committed (feature-board.tsx shared file). The 022 select already has rollback + ErrorText on failure — illegal-transition 400s ride it.
</context>

<scope>
- web/src/features/planning/types.ts — PlanningFeatureStatus union + PLANNING_FEATURE_STATUSES gain "review" (display order: draft, ready, inProgress, review, done).
- web/src/features/planning/lib/status-tone.ts — FEATURE_TONES review: "info" (artifact precedent).
- New web/src/features/planning/lib/feature-transitions.ts — mirror of the backend matrix (draft→[ready,inProgress], ready→[inProgress,draft], inProgress→[review,ready,draft], review→[done,inProgress], done→[]) + allowedTransitions(status) helper.
- feature-board.tsx FeatureStatusSelect + feature-workspace.tsx header Select: options = allowedTransitions(current) + current; done features render a locked select (current only, disabled). 400 rollback already exists — verify it surfaces the typed error text.
- Verify: npm run lint && npx tsc --noEmit && npm run build.
</scope>

<anti-scope>
- No filter changes (task 04), no backend, no new endpoints.
- No workflow automation beyond the dropdowns.
</anti-scope>

<constraints>
- TypeScript strict; English-only; the transition map is the ONLY place listing targets (no duplication in components).
</constraints>

<acceptance-criteria>
- [ ] A review-status card renders with the info tone; board list shows the status chip.
- [ ] Selects show only legal targets; done → locked; draft has no done option.
- [ ] Illegal selection (should not be possible via UI) still rolls back cleanly if the API 400s.
- [ ] lint + tsc + build green.
</acceptance-criteria>

<refs>
- Spec: feature 023 — web dropdown decisions.
- Pattern refs: types.ts:1+26–31, status-tone.ts:4–9, feature-board.tsx (022 select), feature-workspace.tsx:134–149.
</refs>

<obstacles>
</obstacles>

<log>
</log>
