---
feature: 019
slug: ceremony-stepper
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 019 — Ceremony stepper on feature page (feature → spec → plan → tasks x/y → result)

<outcome>
019-ceremony-stepper shipped: a horizontal ceremony stepper above the feature workspace tab strip — feature (non-clickable anchor with status tone dot + truncated title) → spec → plan → tasks → result, separated by chevrons. Stage state derives purely from the loaded feature object (doc artifacts → status chips with tones from status-tone.ts, absent → muted; tasks → todo / x/y info / all-done success), clicks ride the existing URL-syncing setTab (?tab=), the active stage carries aria-current="step". Zero new fetches — the 017 polling keeps it fresh; one visual language with the 023 badges.
</outcome>

<evidence>
- Review: audits/review-019-01.md — PASS, 0 blockers, 4 INFO notes. The multiple-docs-of-a-kind concern resolved structurally: the backend pins non-task kinds to Order 0 with a unique (FeatureId, Kind, Order) index — a second spec/plan/result is impossible, .find() derivation mirrors documentArtifact.
- Presentational purity verified (no hooks, no fetches); aria wiring correct (nav landmark, unique label, aria-current only when active); tsc 0 / lint 0 errors / build green.
- Files: ceremony-stepper.tsx (new ~140 lines), feature-workspace.tsx (one render line above the tabs), index.ts (barrel).
</evidence>

<deferred>
- Microbatch: "done x/y" wording vs a literal "done" (richer, kept); the dead "skill" entry in the stepper's tone map is the price of Record exhaustiveness — fine.
- Visual pass on 0.1.8 pending (same posture as the session's features).
</deferred>
