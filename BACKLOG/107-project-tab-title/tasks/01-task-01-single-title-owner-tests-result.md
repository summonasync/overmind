---
feature: 107
task: 01
slug: project-tab-title
status: inprogress
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
</log>
