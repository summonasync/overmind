---
feature: 066
task: 01
slug: feature-number-styles
status: todo
review: light
created: 2026-10-04
brief: Backend: numberStyle+numberPrefix fields, migration, settings API, validation + tests
---

# Task 01 — Backend: project number settings

<goal>
Per-project number style + prefix, stored and validated.
</goal>

<scope>
- Project.numberStyle (hash default | key | plain) + numberPrefix (required non-empty when key; [A-Z][A-Z0-9_]{1,15}); migration AddProjectNumberStyle (zero-impact defaults).
- Settings update path (project settings API) + validation errors; contract pins; unit tests on validation/defaults.
</scope>

<acceptance-criteria>
- [ ] Migration up+down green; defaults zero-impact — pinned.
- [ ] Validation: key without prefix rejected, bad prefix charset/length rejected — pinned.
- [ ] Settings API round-trip — pinned.
</acceptance-criteria>

<refs>
- Spec e3f93cc8; plan 29745c88 (task 1).
</refs>