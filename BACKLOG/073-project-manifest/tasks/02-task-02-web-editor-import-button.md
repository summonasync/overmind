---
feature: 073
task: 02
slug: project-manifest
status: todo
review: light
created: 2026-10-04
depends_on: 01
brief: Web: markdown editor + import button on project config + tests
---

# Task 02 — Web: editor + import button

<goal>
The human edits the manifest on the config page.
</goal>

<scope>
- Project config: markdown textarea editor + save through the existing update mutation; «Import from agent file» button (shows which file was found / none); manifest absent state.
- Tests: editor save flow, import button states.
</scope>

<acceptance-criteria>
- [ ] Editor + save pinned; import button states (found/not-found) pinned.
- [ ] Gates green (lint/tsc/node test).
</acceptance-criteria>

<refs>
- Spec 722d3acd; plan c6958bdf (task 2).
</refs>