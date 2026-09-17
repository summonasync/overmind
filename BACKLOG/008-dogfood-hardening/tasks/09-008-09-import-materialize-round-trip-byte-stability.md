---
feature: 008
task: 09
slug: dogfood-hardening
status: inprogress
review: light
created: 2026-09-17
---

# 008/09 — Import/materialize round-trip byte-stability

<goal>
An import → materialize → re-import cycle over an unchanged repo must be a byte-stable no-op: no compounding H1 duplication in materialized documents, no validated-date churn, no spurious "re-imported" log entries on validated artifacts. Verified live on the dogfood repo (001 feature.md accumulated 3 duplicated H1s and version 2→4 over two cycles).
</goal>

<context>
Feature 008-dogfood-hardening, found LIVE during the 008/07 closeout re-import cells (predicted by the 008/03 light review byte-stability observation). Root cause: the importer keeps the document H1 in the artifact body, and the materializer prepends its own canonical H1 — so every cycle adds one more. Separately, any body mismatch on a validated artifact triggers the edit path (change note + draft-reset + re-validate with FrozenAt=UtcNow), which is what bumped validated: 2026-09-16 → 2026-09-17 and appended the re-imported log line on 001 spec.
</context>

<scope>
- src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs — document rendering: strip existing leading H1 heading(s) from the body before prepending the canonical one (self-healing: a body with 2-3 duplicated H1s normalizes to exactly one on the next sync)
- src/Overmind.Infrastructure/Planning/BacklogImporter.cs — strip the leading H1 from parsed feature/spec/plan/result/task bodies so DB bodies stay canonical (H1-free)
- tests/Overmind.Infrastructure.Tests: (a) materialize over an imported repo is byte-idempotent; (b) import → materialize → re-import produces zero artifact updates and no validated/FrozenAt churn on a validated spec; (c) healing test: a body with duplicated H1s normalizes to one
</scope>

<anti-scope>
- No schema changes; no report-model changes
- The dedup content key (008/08) already strips <log>; do not change that mechanism
- Do not hand-fix the currently corrupted live DB/files — the fix must heal them through the normal ceremony (that is the live verification)
</anti-scope>

<constraints>
- .NET 10 / C# 14; no new packages; suite currently 540 green
</constraints>

<acceptance-criteria>
- [ ] Tests (a), (b), (c) green
- [ ] Live on the dogfood profile: one import → materialize cycle after the fix collapses 001 feature.md back to a single H1 and a second cycle is a full no-op (git diff empty, no validated-date changes)
- [ ] dotnet build Overmind.sln + dotnet test Overmind.sln --no-build green
</acceptance-criteria>

<refs>
- @../spec.md — IMP decisions (reviewer)
- audits/architect-review-2026-09-17-008-07.md — hard review context
- BACKLOG.md Deferred — the byte-stability nit this promotes to a live bug
</refs>

<obstacles>
- 2026-09-17 — scope interpretation: task bodies keep ONE leading H1 instead of going fully H1-free. The heading carries the task title used to generate `tasks/NN-<slug>.md` filenames (`BuildPlans` → `FirstHeading`), and with schema changes anti-scope there is no field to move it to; stripping it would rename every existing task file and orphan the originals. Duplicated leading H1s in a task body are still collapsed to one on both import (`CollapseLeadingHeadings`) and materialize, so the self-heal and byte-stability goals hold for tasks too.
</obstacles>

<log>
- 2026-09-17 — 008/09 started: live-found compounding H1 duplication + validated churn on re-import cycles (blocks the closeout live cells)
- 2026-09-17 — implemented: `BacklogMarkdown.StripLeadingHeadings`/`CollapseLeadingHeadings`; importer strips leading H1s from feature/spec/plan/result bodies (DB canonical H1-free) and collapses task headings to one; materializer strips before prepending the canonical H1 (`# Feature NNN — title`, `# Spec|Plan|Result NNN — title` derived from the feature title) and collapses task bodies; round-trip tests (a)(b)(c) + DB-seeded self-heal test green; suite 540 → 544
</log>