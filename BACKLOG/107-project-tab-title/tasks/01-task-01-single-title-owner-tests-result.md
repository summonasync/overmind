---
feature: 107
task: 01
slug: project-tab-title
status: done
review: light
created: 2026-10-04
brief: Web: single title owner f(project, awaiting) + tests + result (Simple path)
---

# Task 01 — Single title owner + tests + result

<goal>
document.title shows the open project; awaiting composes on top.
</goal>

<scope>
- One hook owns document.title = f(projectName, awaitingCount): name via the 101 resolver over ?project=; plain «Overmind» without/unresolvable; awaiting counter composes over the project base (087/093 semantics preserved).
- Replace the mount-once base capture (microbatch #134 resolved by design).
- Web tests: project route title, fallback, awaiting composition.
- Result artifact.
</scope>

<anti-scope>
- Per-page titles beyond the project segment; favicon.
</anti-scope>

<acceptance-criteria>
- [x] Title computation pinned (project/unresolvable/none × awaiting 0/N).
- [x] Single writer (old capture removed) — verified by grep/test.
- [x] Gates green; result drafted.
</acceptance-criteria>

<refs>
- Spec da92c2fe; plan 71028822; use-awaiting-title (087); #134 microbatch note.
</refs>

<log>
- 2026-10-04 [agent] — taken into work by the autonomous pipeline
- 2026-10-04 [agent] — execution complete, awaiting orchestrator review dispatch
- 2026-10-04 [agent] — [orchestrator] Light review PASS-WITH-NITS (0 blockers, 2 cosmetic nits): single writer confirmed (document.title only in use-tab-title.ts), #134 closed by design, all gates re-run green by reviewer (lint 0e/6 pre-existing w, tsc clean, 99/99, build 12/12). Report: audits/review-107-01.md. Nits → microbatch #174 (tab-title.test final test re-asserts pairs, no new matrix cell), #175 (BASE_TITLE duplicates SSR metadata title — deliberate FSD trade-off, note for product rename).
- 2026-10-04 [agent] — [orchestrator] Light review passed (PASS-WITH-NITS, 0 blockers — audits/review-107-01.md), all 3 AC confirmed by reviewer. Closing task; result stays draft per pipeline mandate (human tap batched at validation).
</log>
