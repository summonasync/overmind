---
feature: 076
task: 01
slug: task-file-naming-regression
status: done
review: light
created: 2026-09-24
brief: Task filenames: slug from FirstHeading ?? Brief before the task-NN fallback; regression tests incl. Cyrillic slug readability
---

# Task 076/01 — Task filename slug: FirstHeading ?? Brief fallback chain

<goal>
Fresh ceremony output materializes headingless tasks as NN-<brief-slug>.md instead of the degraded NN-task-NN.md; the fallback stays as the last resort only.
</goal>

<scope>
Root cause (investigated, NOT 067/BacklogNaming): task filename slugs derive from the first H1 of the task body; the ceremony style drifted to XML-section bodies without H1 (37 files degraded since feature 032, 2026-09-20). Fix the slug source in PlanningMaterializer: FirstHeading(task.Body) ?? task.Brief before the task-NN fallback. Pin with tests: English brief slug, Cyrillic brief slug (letters kept readable, not octal), last-resort fallback unchanged. Existing pinned files keep their names by design (PinnedTaskPath rows); renaming them is out of scope — follow-up.
</scope>

<anti-scope>
No renaming of the 37 existing degraded files (name-pin rows protect them; a rename pass is a separate follow-up). No Heading injection into task files (would mutate bodies on import round-trip). No Title field on PlanningArtifact (schema change, separate feature if ever needed).
</anti-scope>

<acceptance-criteria>
- [x] Headingless task with brief materializes as NN-<brief-slug>.md (fresh output)
- [x] Cyrillic brief slugifies readable: letters preserved, separators as dashes (e.g. 01-cpm-обновление-пакетов-aspire-13-5.md)
- [x] Headingless + briefless task still materializes as NN-task-NN.md (existing pin stays green)
- [x] Task with H1 body keeps the old behavior (01-first-task.md pin stays green)
</acceptance-criteria>

<refs>
Feature 076 (goal example filename); BobHouse 020 field notes B3 (entry c7f34f40); PlanningMaterializer.cs:312-316 (slug source); BacklogNaming.TaskFileName (unchanged); Slug.From (Cyrillic already passes IsLetterOrDigit).
</refs>

<log>
- 2026-09-25 [agent] — task created after live investigation: 37 degraded files inventoried, root cause pinned to heading-less ceremony bodies, not the 067 series
- 2026-09-24 [agent] — starting implementation — slug source fix in PlanningMaterializer + regression tests
- 2026-09-24 — pinned by On_HeadinglessTaskWithBrief_FilenameCarriesBriefSlug — 01-data-ef-blockedby-on-planningfeature.md materialized
- 2026-09-24 — asserted 02-cpm-обновление-пакетов-aspire-13-5.md — exactly the goal-example filename; Slug.From passes Cyrillic through IsLetterOrDigit
- 2026-09-24 — existing fixture (taskTwo, no heading no brief) kept expecting 02-task-02.md — green in full suite
- 2026-09-24 — On_MaterializesProject_DemoTree (01-first-task.md from H1 body) green unchanged
- 2026-09-24 [agent] — done — fix verified: materializer tests 36/36, full suite 1331/1331 (Infra 428), Cyrillic slug pinned to the goal example
</log>