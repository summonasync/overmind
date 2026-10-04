---
feature: 092
task: 01
slug: waits-for-closed-blockers-bug
status: todo
review: light
created: 2026-10-04
brief: Web fix: Waits for / Cleared by split + test + result (Simple path)
---

# Task 01 — Split groups + test + result

<goal>
Cleared blockers stop looking like waiting: split the dependencies panel by closure state.
</goal>

<scope>
- FeatureDependenciesPanel: open blockers → «Waits for» group (danger chips, «N open» badge, X); closed → separate «Cleared by» group (muted, line-through, link, X kept).
- Empty waits + non-empty cleared renders «nothing open».
- Web test pins the split (headings, chip styling, badge counts, empty text).
- Result artifact.
</scope>

<anti-scope>
- Backend/API; Blocks group semantics; auto-removing closed blockers.
</anti-scope>

<acceptance-criteria>
- [ ] Open/closed split rendered per spec; headings and counts pinned in a web test.
- [ ] Gates green (lint/tsc/node test); light review pass.
- [ ] Result artifact drafted.
</acceptance-criteria>

<refs>
- Spec a434b759; plan 3c3bd3f8.
</refs>