---
feature: 066
task: 02
slug: feature-number-styles
status: done
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
- [x] Formatter covers hash/key/plain — pinned; prefix required for key.
- [x] All surfaces route through it (grep pass recorded); settings section pinned.
- [x] Gates green (lint/tsc/node test).
</acceptance-criteria>

<refs>
- Spec e3f93cc8; plan 29745c88 (task 2); formatFeatureNumber foundation (065).
</refs>

<obstacles>
- Repo keeps a strict no-cross-feature-import invariant (zero feature→feature imports). Numbering therefore resolves in the pages/widgets layer (planning-page, projects-page, ceremonies-section, awaiting-row, notification-row) via useProjectNumbering from features/projects, and threads down as a single ProjectNumbering prop; the ProjectNumbering type itself lives in shared/lib/feature-number.ts next to the formatter so features stay isolated.
- tests/attention-ceremony-rows.test.mts pinned the exact badge JSX — updated the pin to the numbering shape and added a group-level numbering resolution pin.
- Web tests are node:test source-contract style (no vitest/jest in this repo, contrary to the brief's guess): unit tests import ../src/shared/lib/feature-number.ts directly, interaction coverage is source pins (feature-number-settings.test.mts).
- board-search.ts uses featureNumber for search matching only (needle strips "#") — left untouched by design: matching is style-independent, not a display template.
- useProjectNumbering falls back to DEFAULT_NUMBERING (hash) until the projects list query resolves — same eventual-consistency pattern as the existing useProjectName/ProjectNameBadge.
</obstacles>

<log>
- 2026-10-05 [agent] — taken by orchestrator — dispatching web-coder (sonnet) for parameterized formatter + settings section
- 2026-10-05 [agent] — web-coder: formatter parameterized (slug, style=hash default, prefix; key without usable prefix falls back to hash); settings section FeatureNumberSettings on project detail (style select + key-only prefix input, client prefix guard mirroring ^[A-Z][A-Z0-9_]{1,15}$, live preview on first numbered board slug); numbering resolved at pages/widgets via useProjectNumbering (cached project list) and threaded as ProjectNumbering into FeatureNumber/FeatureNumberBadge/FeatureSlugLabel and mid-tier planning components; grep pass: 3 display components + 8 call-sites on the formatter, board-search.ts left as style-independent matching (not display); PUT sends numberStyle always, numberPrefix only with key (hash/plain omit prefix — backend 400s on prefix+non-key). Gates from web/: lint 0 errors (6 pre-existing warnings), tsc --noEmit clean, node --test 182 pass / 0 fail (new: feature-number.test.mts 8, feature-number-settings.test.mts 10), next build ok. No commits made.
- 2026-10-05 [agent] — web-coder: formatter styles pinned in feature-number.test.mts (hash/plain/key, key-without-prefix falls back to hash, prefix validator mirrors backend).
- 2026-10-05 [agent] — web-coder: grep pass — FeatureNumber, FeatureNumberBadge, FeatureSlugLabel all call formatFeatureNumber(slug, style, prefix); call-sites feature-board, ceremonies-section, task-board x2, delete-artifact-dialog, task-dependencies-panel, notification-row, awaiting-row x2; no local # templates left in display paths; settings section pinned by feature-number-settings.test.mts.
- 2026-10-05 [agent] — web-coder: from web/ — eslint 0 errors (6 pre-existing warnings), tsc --noEmit clean, node --test 182 pass / 0 fail, next build success.
- 2026-10-05 [agent] — light review PASS-WITH-NITS 0 blockers (single formatter verified by independent grep pass, zero cross-feature imports confirmed, PUT semantics match backend composition rules, key-fallback-to-hash pinned). Gates re-run by orchestrator: lint 0e/6 pre-existing, tsc clean, 182/182 node --test. Nits 1 (board polling for preview slug) and 2 (hash fallback beside warning) -> microbatch ledger; nit 3 (raw enum labels in select) cosmetic.
</log>
