---
feature: 019
slug: ceremony-stepper
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Spec 019 — Ceremony stepper on feature page (feature → spec → plan → tasks x/y → result)

<decisions>
- The stepper renders ABOVE the existing tab strip in the feature workspace — the tabs stay as fine-grained navigation; the stepper is the at-a-glance ceremony journey with click navigation. One visual language with the 023 status badges (tones from status-tone.ts) (decision delegated by the user 2026-09-20).
- Stages: feature → spec → plan → tasks (x/y) → result. State derives purely from the already-loaded feature data (no new endpoints): feature stage = the anchor (renders the feature title/status dot, not clickable — it is where you already are); spec/plan = absent / artifact status chip; tasks = none yet → todo, else done/total counter, all-done → done; result = absent / artifact status.
- Click on spec/plan/tasks/result → the existing setTab helper (URL-syncing ?tab=) — the stepper is navigation, not just an indicator.
- Live sync rides 017 for free: the stepper consumes the feature query, which already polls every 15s.
- The stepper shows the CEREMONY, not the lifecycle — the feature status badge in the header (022/023) keeps that job; no duplication.
</decisions>

<open-questions>
- none (user delegated autonomous decisions 2026-09-20)
</open-questions>

<requirements>
Web only (features/planning):
- New ceremony-stepper component in the planning slice: horizontal stage list (wrap on narrow screens), tone-colored dots/labels per stage state, tasks stage shows done/total, clickable stages get tab navigation (spec/plan/tasks/result), current tab's stage highlighted, feature stage is the non-clickable anchor.
- Integrate above the tab strip in feature-workspace.tsx; derive all state from the loaded feature object (artifacts by kind, task counts) — zero fetches.
- a11y: the stepper is a nav with aria-current on the active stage.
- Verify: npm run lint && npx tsc --noEmit && npm run build.
</requirements>

<acceptance-ideas>
- A feature with validated spec, no plan, 2/5 tasks shows: spec ✓ (success tone), plan ○ absent, tasks ● 2/5 (info), result ○ absent.
- Clicking tasks jumps to the tasks tab (URL ?tab=tasks updates).
- An MCP-side artifact change reflects in the stepper within ~15s (017 polling).
</acceptance-ideas>
