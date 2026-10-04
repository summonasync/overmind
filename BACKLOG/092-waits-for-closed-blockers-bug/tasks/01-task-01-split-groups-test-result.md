---
feature: 092
task: 01
slug: waits-for-closed-blockers-bug
status: done
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
- [x] Open/closed split rendered per spec; headings and counts pinned in a web test.
- [x] Gates green (lint/tsc/node test); light review pass.
- [ ] Result artifact drafted.
</acceptance-criteria>

<refs>
- Spec a434b759; plan 3c3bd3f8.
</refs>

<log>
- 2026-10-04 [agent] — taken into work by the autonomous pipeline
- 2026-10-04 [agent] — execution complete, awaiting orchestrator review dispatch
- 2026-10-04 [agent] — review 092/01: PASS-WITH-NITS — gates green, report at audits/review-092-01.md
- 2026-10-04 [agent] — light review PASS-WITH-NITS: 0 blockers, 2 nits (cleared-group X aria-label wording; render wiring unpinned by tests). Gates green: lint 0 errors, tsc clean, node test 90/0. Report: audits/review-092-01.md
- 2026-10-04 [agent] — review PASS-WITH-NITS closed (0 blockers, verdict logged by the reviewer); result 0dbbc7db stays draft per the no-tap pipeline mandate; aria-label nit → microbatch #173.
</log>
