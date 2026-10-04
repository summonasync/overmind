---
feature: 071
task: 02
slug: backlog-import-ui
status: todo
review: light
created: 2026-10-04
depends_on: 01
brief: Web: settings section — scan, states, preview, per-folder import + tests
---

# Task 02 — Web UI on project settings

<goal>
Bring the import flow to the human: scan button + folder list + per-folder import on the project settings page.
</goal>

<scope>
- Section on project settings: Scan button, folder list with states (new / already imported / error note), expandable preview, per-folder Import button (confirm on re-import naming it), report lines after import.
- React Query per house idiom; web tests on list states and import interaction.
</scope>

<anti-scope>
- Bulk import-all; editing imported content.
</anti-scope>

<acceptance-criteria>
- [ ] Folder states rendered and pinned; re-import confirm names the state.
- [ ] Gates green (lint/tsc/node test).
</acceptance-criteria>

<refs>
- Spec d46a2a7c; plan 2859ccfb (task 2).
</refs>