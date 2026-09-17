---
feature: 008
task: 09
slug: dogfood-hardening
status: done
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
</obstacles>

<log>
- 2026-09-17 — 008/09 started: live-found compounding H1 duplication + validated churn on re-import cycles (blocks the closeout live cells)
- 2026-09-17 — 2026-09-17 — coder — StripLeadingHeadings/CollapseLeadingHeadings on import (H1-free canonical DB bodies; tasks keep one H1 for filename derivation — deliberate, in obstacles); materializer strips before prepending canonical H1 (self-healing); PlanningRoundTripTests pin byte-idempotency + FrozenAt stability + healing; 544/544 green
- 2026-09-17 — 2026-09-17 — light review — PASS-WITH-NITS; code-side AC green; remaining: live healing cell on the dogfood profile (first cycle collapses duplicated H1s, second cycle is a git-diff no-op) — runs after the final 0.1.1 repack
- 2026-09-17 — 2026-09-17 — live healing cell GREEN: cycle 1 collapsed duplicated H1s (001 feature.md 3x -> 1x) and reset churn; cycle 2 zero artifacts; cycle 3 byte-identical BACKLOG tree (sha256 of sorted file hashes equal). AC fully met
- 2026-09-17 — AC met incl. live cells: healing + no-op cycles verified on the dogfood profile; 544/544; review PASS-WITH-NITS
</log>