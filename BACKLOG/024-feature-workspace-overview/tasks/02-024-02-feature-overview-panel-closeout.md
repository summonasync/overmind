---
feature: 024
task: 02
slug: feature-workspace-overview
status: done
review: light
created: 2026-09-20
---

# 024/02 — Feature Overview panel + closeout

<goal>
When a feature is open, the right panel shows Feature Overview: tone status, version, current-pin, tasks x/y + progress, spec/plan/result chips, backlog age (d/h), updated relative, and Open questions parsed from the spec's <open-questions> section. Dashboard mode untouched.
</goal>

<context>
Feature 024, task 2 of 2. Depends on nothing from 01 (disjoint areas) but rides the same feature. Find where the right panel renders today (planning-info-panel / dashboard widget) and branch its content on feature mode.
</context>

<scope>
- Locate the right panel composition (planning-info-panel.tsx or the dashboard-side widget) and add the feature-mode branch: Feature Overview block fed by the loaded feature object — status Badge (featureStatusTone), vN, current-pin badge when pinned, tasks done/total + the progress bar idiom (feature-board.tsx:171-180 precedent), spec/plan/result artifact chips (artifactChip precedent), "in backlog Xd/Xh" (createdAt → now; days >24h else hours), "updated X ago" (existing relative-time helper), Open questions.
- New tiny pure helper in features/planning/lib (e.g. open-questions.ts): extract the markdown between <open-questions> and </open-questions> from a spec body; absent spec → null; present but "- none" → render as-is (it reads fine).
- Render the questions via the shared Markdown component.
- Verify: npm run lint && npx tsc --noEmit && npm run build.
- Closeout (orchestrator): result artifact, validate, feature done via human door, unpin, materialize, single feat-commit (no push), plus the live visual check with the user (he is walking the UI).
</scope>

<anti-scope>
- No changes to the dashboard-mode panel content; no new queries/endpoints; no backend.
</anti-scope>

<constraints>
- TypeScript strict; English-only; FSD (feature data flows through the planning slice's public API).
</constraints>

<acceptance-criteria>
- [ ] Feature open → right panel shows the vitals incl. age and Open questions (or None / No spec yet).
- [ ] Dashboard (no feature) → the existing overview unchanged.
- [ ] lint + tsc + build green; closeout artifacts done.
</acceptance-criteria>

<refs>
- Spec: feature 024, spec v2.
- Pattern refs: planning-info-panel.tsx (or the current panel), feature-board.tsx artifactChip + progress bar, formatRelativeTime, ceremony-stepper.tsx (spec artifact derivation).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — next-js-coder subagent (disjoint from 01's file; review of 01 running in parallel).
- 2026-09-20 — Coder done: PlanningInfoPanel branches on feature mode (featureSlug prop from the page); new feature-overview-panel.tsx (tone status, vN, current badge, x/y + progressbar idiom, spec/plan/result chips with absent-neutral, "In backlog Xd/Xh/m" via new formatBacklogAge, Updated via formatRelativeTime, Open questions: No spec yet / None / markdown render via the shared component); extractOpenQuestions pure helper; slice public API extended; zero new queries (React Query dedup). lint 0 / tsc 0 / build green. To review.
- 2026-09-20 — Review PASS (audits/review-024-02.md): 0 blockers, 4 INFO (sub-30s age rounds to 1m + half-second divergence vs formatRelativeTime — microbatch; "- none" renders as the bullet per task contract, recorded; empty panel while pending — consistent; usePlanningBoard mounted in two panels — same key, do not "optimize"). Query dedup via shared keys verified; FSD via the slice public API; age edges (24h→1d, <1m→1m, invalid ISO) pinned; progress-bar a11y byte-identical to the board idiom; dashboard branch untouched. Done.
</log>
