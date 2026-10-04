---
feature: 066
task: 02
slug: feature-number-styles
status: todo
review: light
created: 2026-10-04
depends_on: 01
brief: Web: parameterized formatter + settings section + all call-sites + tests
---

# Task 02 — Web: shared formatter + settings + all surfaces

<goal>
One formatter feeds every surface; humans pick the style in settings.
</goal>

<scope>
- formatFeatureNumber(slug, style, prefix) parameterized; settings tab section (style select + prefix input when key + live preview on a real board number).
- Every call-site (card badge, task titles, notification rows) through the shared formatter — no local templates; grep-verified.
- Web tests: formatter styles, settings interactions.
</scope>

<anti-scope>
- Server-side number rendering; per-feature overrides.
</anti-scope>

<acceptance-criteria>
- [ ] Formatter covers hash/key/plain — pinned; prefix required for key.
- [ ] All surfaces route through it (grep pass recorded); settings section pinned.
- [ ] Gates green (lint/tsc/node test).
</acceptance-criteria>

<refs>
- Spec e3f93cc8; plan 29745c88 (task 2); formatFeatureNumber foundation (065).
</refs>