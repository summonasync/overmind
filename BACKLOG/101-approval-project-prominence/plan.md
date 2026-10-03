---
feature: 101
slug: approval-project-prominence
status: validated
created: 2026-10-03
validated: 2026-10-03
---

# Plan 101 — Project prominence: LARGE project name on approval surfaces + always-visible header on feature page/dashboard/entries

4 tasks, web-only, no backend, no migrations. Chain: 01 (shared primitive) → 02 (approval surfaces) ∥ 03 (content-page headers) → 04 (closeout: architect light + result; NO live dogfood — batch after release per user strategy).

## Task chain

1. **01 — Shared primitive.** `ProjectNameBadge`/`ProjectHeader` component + resolver hook in the FSD shared layer: projectId → prominent project name, graceful fallback (unknown id → id-shown/generic, never crash). Resolver consumes the projects data the app already loads (no new fetches unless truly absent). Unit tests for resolution + fallback.
2. **02 — Approval surfaces (parallel with 03).** Bell rows (084/096): project name in the title position, feature/slug + agent demoted to secondary context. Attention cards (087): same hierarchy. Global toast (093): project name as headline line. Regression: notification-href tests untouched, unread states unaffected.
3. **03 — Content-page headers (parallel with 02).** Feature page: project name at the top above dashboard + entries. Dashboard and entries views: same persistent header. Same primitive from 01.
4. **04 — Closeout.** Cross-surface consistency check (one primitive, no ad-hoc stylings left), architect light, result artifact. NO live dogfood (batch after release); result carries a small click-check list for the release batch.

## Verification

- Web gates every task (lint/tsc/build/test with real numbers); per-surface presence tests (project name in the primary position); notification-href regressions stay green.

<log>
- 2026-10-04 [agent] — plan drafted after spec validation; 01 → (02∥03) → 04, web-only, no live dogfood per user strategy.
- 2026-10-03 [agent] — validated with human proof: notification 764150d5-30cd-41bb-afd4-c286474a5dc7 replied 2026-10-03 21:18:15 UTC
</log>