---
feature: 101
task: 03
slug: approval-project-prominence
status: done
review: light
created: 2026-10-03
depends_on: 01
brief: ProjectHeader on feature page + dashboard + entries via the shared primitive
---

# Task 03 — Content-page project headers (parallel with 02)

<goal>
Feature page, dashboard, and entries always show which project you are in — persistent header above the content.
</goal>

<scope>
- Feature page: ProjectHeader at the top, above the dashboard section + entries — visible without scrolling.
- Dashboard view: same persistent header.
- Entries view: same persistent header.
- One primitive (task 01), one pattern — no ad-hoc stylings per page.
- Per-page presence tests: header rendered above content with the correct project.
</scope>

<anti-scope>
No Issues Board header (its filter already shows the project), no sidebar/workspace changes, no backend.
</anti-scope>

<constraints>
FSD, no new packages, English UI, no auto-commits; gates real numbers.
</constraints>

<acceptance-criteria>
- [x] lint + tsc + build + test green
- [x] Feature page: header above dashboard + entries
- [x] Dashboard + entries: same header
- [x] Presence tests per page
</acceptance-criteria>

<refs>
- Spec: 0f88a771 (B section).
- Task 01 (order 1) — the primitive.
</refs>

<obstacles></obstacles>

<log></log>

<log>
- 2026-10-03 [agent] — dispatched to web-coder (fresh session, parallel with 02 — disjoint surfaces: bell/attention/toast vs feature-page/dashboard/entries)
- 2026-10-03 [agent] — 2026-10-04 [agent] — reviewed by web-code-reviewer: PASS-WITH-NITS — placement per spec section B (header first in feature branch above workspace, above PageHeader on board/entries), dead-derivation removal lossless (same cache), suffix removal justified (double render), 2 informational nits (h1/h2 polish future, guid fallback contract); coder also closed 2 nits from review-101-01 in this delta; gates re-verified (lint 0/6, tsc 0, 62/62, build); report audits/review-101-03-web.md.
- 2026-10-03 [agent] — delta review PASS-WITH-NITS (audits/review-101-03-web.md): placement per spec, lossless dead-code removal, AC 4/4 ticked. Closing as done.
</log>
