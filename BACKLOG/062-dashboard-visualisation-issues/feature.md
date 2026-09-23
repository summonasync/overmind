---
feature: 062
slug: dashboard-visualisation-issues
status: inProgress
priority: p0
version: 1
created: 2026-09-23
---

# Feature 062 — Dashboard visualisation issues: p2 badge light green + feature number before task titles

Two dashboard/board visualisation bugs from the user, 2026-09-23 (session), merged into one p0 by request:

1. **p2 badge color indistinguishable from p3.** The p2 priority chip renders gray, visually merging with p3 — at a glance you cannot tell where p2 ends and p3 begins. Wanted: p2 = light green, p3 keeps gray. Check every surface priority chips render on (board cards, feature header, filters/legend if present); p0/p1 keep their existing distinct colors.

2. **Feature number missing from task titles.** Every task must show its feature number before the title — e.g. `054 · <task title>`. Today the number lives only in the gray slug, so mapping tasks to features (agent reports reference 054, 059, …) requires squinting. Number source: the feature slug prefix (`NNN-slug`); render as a chip or bold prefix, not part of the editable title. Applies wherever tasks render: feature task tables, current-task views, notifications carrying task titles.

Supersedes 060-priority-badge-colors and 061-task-feature-number-prefix (both tombstoned).

<log>
- 2026-09-23 [auto] — artifact created → inProgress
</log>
