---
feature: 066
slug: feature-number-styles
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 066 — Feature-number styles per project (KEY-039 / #039 / plain) + вкладка project settings — один форматер для всех поверхностей

> Grooming 2026-10-04. 3 tasks, 01 → 02 → 03.

## Task chain

1. **01 — Backend settings.** `Project.numberStyle` (hash default | key | plain) + `numberPrefix` (required non-empty when key, `[A-Z][A-Z0-9_]{1,15}` validated); migration AddProjectNumberStyle; settings update path (project settings API) + contract pins; unit tests on validation and defaults.
2. **02 — Web formatter + settings + surfaces.** `formatFeatureNumber(slug, style, prefix)` parameterized; settings tab section (style select + prefix input when key + live preview on a real board number); every call-site (card badge, task titles, notification rows) through the shared formatter — no local templates; web tests on formatter styles and settings interactions.
3. **03 — Closeout.** Consistency pass across surfaces (grep for leftover hardcoded `#` templates), architect light, result with post-release click-check (switch style, see every surface follow).

## Verification

- Unit/contract tests (01), web tests (02), gates each task, architect light (03).

<log>
- 2026-10-04 [agent] — plan groomed: 3 tasks linear; server never renders numbers into wire strings.
- 2026-10-04 [agent] — validated with human proof: notification 6aafb601-5bad-4186-982f-5b51f6894a95 replied 2026-10-04 11:48:57 UTC
</log>