---
feature: 019
task: 01
slug: ceremony-stepper
status: done
review: light
created: 2026-09-20
---

# 019/01 — Ceremony stepper component

<goal>
A horizontal ceremony stepper above the feature workspace tab strip: feature (anchor) → spec → plan → tasks (x/y) → result, with per-stage state derived from the loaded feature data and click navigation via the existing setTab.
</goal>

<context>
Feature 019, the only task. The workspace already has URL-syncing tabs (setTab helper, ?tab= param) and the feature query polls every 15s (017) — the stepper only presents and navigates.
</context>

<scope>
- New web/src/features/planning/components/ceremony-stepper.tsx: props {feature, activeTab, onSelect}; stages feature/spec/plan/tasks/result; feature = non-clickable anchor (title + status dot); spec/plan/result = absent (muted) or artifact status chip (tone via status-tone.ts); tasks = none (todo) / done/total counter / all-done; clickable stages call onSelect(stage tab); aria-current on the active stage; nav landmark; wraps on narrow screens.
- feature-workspace.tsx: render <CeremonyStepper> above the tab strip (line ~176), wired to the loaded feature + tab state + setTab.
- Verify: npm run lint && npx tsc --noEmit && npm run build.
</scope>

<anti-scope>
- No backend, no new queries, no lifecycle badges (that is the header's job), no changes to the tabs themselves.
</anti-scope>

<constraints>
- TypeScript strict, no any; English-only; FSD; presentational component (no hooks beyond what rendering needs).
- PARALLEL WORK: a coder may be editing task-editor.tsx (feature 014) — do not touch it.
</constraints>

<acceptance-criteria>
- [ ] Stepper renders above the tabs with all five stages; states match artifacts (validated spec → success tone; absent plan → muted; tasks counter x/y).
- [ ] Clicking spec/plan/tasks/result switches the tab and updates ?tab=.
- [ ] Active stage carries aria-current; feature stage is not clickable.
- [ ] lint + tsc + build green.
</acceptance-criteria>

<refs>
- Spec: feature 019, spec artifact (validated 2026-09-20).
- Pattern refs: feature-workspace.tsx (tab strip ~176, setTab ~76–78, TAB_LABELS), status-tone.ts, Badge.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — next-js-coder subagent (clean tree after the 014 commit).
- 2026-09-20 — Coder done: ceremony-stepper.tsx (~140 lines, presentational, no hooks) — feature anchor (tone dot + truncated title), spec/plan/result status chips (Badge + artifactStatusTone, absent → muted), tasks x/y counter (todo/partial info/all-done success), chevrons, nav landmark, aria-current="step", wrap; wired above the tab strip via setTab; barrel export. lint 0 / tsc 0 / build green. To review.
- 2026-09-20 — Review PASS (audits/review-019-01.md): 0 blockers, 4 INFO (done x/y wording richer than spec literal; second tasks filter render-side isolation; dead "skill" TONE_DOTS entry = price of Record exhaustiveness; deliberate no "use client" — do not "fix"). Multiple-docs-of-kind impossibility verified against the unique index; derivation mirrors documentArtifact; tones via artifactStatusTone. Done.
</log>
