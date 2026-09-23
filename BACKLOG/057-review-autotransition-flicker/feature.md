---
feature: 057
slug: review-autotransition-flicker
status: inProgress
priority: p1
version: 1
created: 2026-09-23
---

# Feature 057 — Premature feature auto-transition to review during parallel task creation (OM-5)

Field report OM-5 (FEATURELIST/overmind-features-2026-09-23.md): while five agents created tasks in parallel, `planning_list_features` showed `mmt-36-2` in `review` at `taskTotal: 4, taskDone: 4` — the fifth task was still being created. Its creation then fired "task create on review → back to inProgress" and the feature re-entered `review`. Final state correct, but the feature log accumulated phantom transitions and any observer in the window saw a false "ready for review".

Fix options from the report (pick the minimal robust combination): debounce the review auto-transition (quiet window with no mutations), batch task creation (044-batch-planning-ops), or collapse logging of `review → inProgress → review` pairs inside a short window.

<log>
- 2026-09-23 [auto] — artifact created → inProgress
</log>
