---
feature: 008
task: 08
slug: dogfood-hardening
status: done
review: light
created: 2026-09-17
---

# 008/08 — Import: dedup letter-suffixed re-import by content

<goal>
Re-importing a repo whose letter-suffixed task file (01b) was already imported must NOT create another artifact. The duplicate cycle found by the 008 hard review is closed: import -> materialize (plain NN names) -> re-import is a no-op for the suffixed file.
</goal>

<context>
Feature 008-dogfood-hardening, follow-up to task 04 (IMP-5). Found by audits/architect-review-2026-09-17-008-07.md WARNING-1: IMP-5 appends a colliding suffixed file at max+1, but the materializer writes tasks back with plain NN- names, so the original 01b file stays a foreign file on disk and every subsequent import appends another duplicate (order 11, 12, ...). The live dogfood repo has exactly such a file (BACKLOG/006-planning/tasks/01b-domain-schema-hardening.md), so this blocks the 008/07 live re-import cells.
</context>

<scope>
- src/Overmind.Infrastructure/Planning/BacklogImporter.cs and/or PlanningFeatureService.ImportAsync merge path: when a letter-suffixed task file collides, before appending at max+1 check whether an existing task artifact of the feature (or one already assigned in this batch) carries identical normalized content; if yes — match that artifact instead of creating a new one (idempotent no-op when unchanged)
- Regression test: two-cycle scenario — import a folder with 01 + 01b, simulate the materializer outcome (add the plain-named file the materializer would write, keep 01b), re-import, assert zero new artifacts and no order-12 duplicate
</scope>

<anti-scope>
- Materializer filename format (stays plain NN)
- Reordering existing DB artifacts
- Changes to the report model beyond what the dedup needs
</anti-scope>

<constraints>
- .NET 10 / C# 14; no new packages; dotnet build + dotnet test must stay green (currently 538)
</constraints>

<acceptance-criteria>
- [ ] Two-cycle regression test green: import -> materialize-simulation -> re-import creates no duplicate task
- [ ] First import of a genuinely new 01b still works per IMP-5 (append + loud reassigned entry)
- [ ] dotnet build Overmind.sln + dotnet test Overmind.sln --no-build green
</acceptance-criteria>

<refs>
- @../spec.md — IMP-5 decision (reviewer)
- @../plan.md — risks (reviewer)
- audits/architect-review-2026-09-17-008-07.md — WARNING-1 (the finding this task closes)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-17 — 008/08 started: coder subagent on letter-suffix re-import dedup (hard review WARNING-1)
- 2026-09-17 — 2026-09-17 — coder fix loop — ContentKey/StripLogSections make the fold log-tolerant on both sides; fold branch is pure presence-dedup (no merge tail); ceremony-cycle regression test pins the reviewer's repro incl. DB body byte-identity; CONTEXT.md documents the matched: fold; 540/540 green
- 2026-09-17 — 2026-09-17 — fix-loop verification (dotnet-code-reviewer) — final verdict PASS; residual nit recorded to BACKLOG hardening (create-branch indexes raw Normalize instead of ContentKey)
- 2026-09-17 — AC met + review warnings closed at root: re-import after ceremony moves is a no-op for letter-suffixed tasks; 540/540; final verdict PASS
</log>