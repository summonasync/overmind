---
feature: 113
task: 01
slug: attention-row-polish
status: done
review: light
created: 2026-10-04
brief: Web: dashboard-style number + linked title on attention rows (Simple path)
---

# Task 01 — Dashboard-style number + linked title on attention ceremony rows

<goal>
Ceremony rows on /attention read like the dashboard: the feature number uses the official badge and the title opens the feature.
</goal>

<scope>
- web/src/pages/attention-page/ui/ceremonies-section.tsx: the raw number span becomes FeatureNumberBadge (shared 065 machinery, dashboard parity); the title becomes a link to the feature url with card-style hover emphasis; «Open →» stays as is.
- Awaiting rows untouched; notification center untouched.
- Source-pin tests per the web/tests pattern where render harness is missing.
</scope>

<anti-scope>
- The awaiting section, bell, approval bubble, dashboard cards.
</anti-scope>

<acceptance-criteria>
- [x] Feature number rendered via FeatureNumberBadge (dashboard parity), not a raw span.
- [x] Title is a link to the feature url with hover emphasis; «Open →» unchanged.
- [x] Gates green (lint/tsc/node test); result drafted.
</acceptance-criteria>

<refs>
- Spec ecc675de-5f68; plan 10ddc0c3-7dfa; 065 feature-number primitives; 087 attention page.
</refs>

<log>
- 2026-10-04 [agent] — task cut after spec+plan validated (human proof 14289b51).
- 2026-10-04 [agent] — [agent] Claimed by Van (claude), taken by orchestrator — dispatching web-coder (sonnet).
- 2026-10-04 [agent] — [agent] Light review passed (PASS-WITH-NITS, 0 blockers — N1 barrel fix landed pre-commit 6a3bec3, N2 → microbatch #184), all 3 AC ticked, result eca3d5c1 drafted. Closing task; result stays draft per pipeline mandate (user taps on return).
</log>