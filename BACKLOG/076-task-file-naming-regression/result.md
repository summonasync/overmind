---
feature: 076
slug: task-file-naming-regression
status: draft
created: 2026-09-24
---

# Result 076 — Bug: task files materialize as 01-task-01.md — title slug dropped from filenames

<goal>
Delivery record for 076-task-file-naming-regression: fresh ceremony output names task files NN-<brief-slug>.md again; the degraded NN-task-NN.md form survives only as the last-resort fallback.
</goal>

<context>
BobHouse 020 field notes B3 flagged 01-task-01.md filenames on fresh ceremony output. Live investigation: 37 degraded files since feature 032 (2026-09-20). Root cause was NOT BacklogNaming or the 067 series — task filename slugs derive from the first H1 of the task body, and the ceremony drifted to XML-section bodies without H1. BacklogNaming and Slug were intact; the input degraded.
</context>

<scope>
PlanningMaterializer task loop: slug source is now FirstHeading(task.Body) ?? task.Brief before the task-NN fallback (PlanningMaterializer.cs:312-315). One new regression test On_HeadinglessTaskWithBrief_FilenameCarriesBriefSlug pins the Latin brief slug (01-data-ef-blockedby-on-planningfeature.md), the Cyrillic brief slug exactly matching the goal example (02-cpm-обновление-пакетов-aspire-13-5.md — Slug.From passes Cyrillic through IsLetterOrDigit, readable not octal), and the absence of the degraded form. Existing pins cover heading-based naming (01-first-task.md) and the briefless fallback (02-task-02.md).
</scope>

<acceptance-criteria>
All four task ACs ticked with evidence; final suite state: Core 203/203, Infrastructure 428/428 (+1 new), Api 382/382, Mcp 318/318 — 0 failures, 0 warnings on build.
</acceptance-criteria>

<refs>
task 9091777f (done, all AC ticked); BobHouse 020 B3 (entry c7f34f40); open follow-up (microbatch queue): rename pass for the 37 already-pinned degraded files — PinnedTaskPath rows keep existing names by design, a rename needs a tracking-row migration or fresh rematerialize
</refs>

<obstacles>
Considered and rejected: (1) injecting an H1 into task files at materialization — would leak the injected heading back into bodies on import round-trip (bodies stay verbatim); (2) Title field on PlanningArtifact — schema change disproportionate to a naming bug; brief covers the need. Name-pinning (016) intentionally protects existing files from renames — old degraded names persist until a deliberate follow-up.
</obstacles>

<log>
- 2026-09-25 [agent] — result recorded after task 9091777f closed done; fix + tests green on full suite
</log>