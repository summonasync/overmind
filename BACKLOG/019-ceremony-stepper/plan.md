---
feature: 019
slug: ceremony-stepper
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Plan 019 — Ceremony stepper on feature page (feature → spec → plan → tasks x/y → result)

<approach>
One web-only task: a presentational stepper component fed by the already-loaded feature object, wired to the existing setTab. Zero fetches, zero backend, rides 017's polling for freshness.
</approach>

<contract>
- ceremony-stepper.tsx (features/planning/components): stages feature (anchor, non-clickable) / spec / plan / tasks (x/y) / result; state from feature.artifacts (status chips per doc artifact, task done/total); tones from status-tone.ts; clicks → setTab; aria-current on the active stage; wraps on narrow screens.
- Integration: above the tab strip in feature-workspace.tsx.
- Nothing else changes.
</contract>

<tasks>
- [ ] 01-ceremony-stepper — component + integration + verification → tasks/01-ceremony-stepper.md
</tasks>

<risks>
- Visual crowding above the tab strip on narrow viewports — wrap, keep compact heights.
</risks>

<verification>
- lint + tsc + build green; manual sanity per the spec's acceptance ideas.
</verification>
