---
feature: 049
task: 02
slug: backlog-index-retirement
status: todo
review: light
created: 2026-10-06
depends_on: 01
brief: Rework PlanningMaterializerTests index pins and add migration pins a–f: strip keeps prose, drift refuses, force warns, no-markers NotFound, fresh project no index, archive switch, no false drift.
---

# Task 02 — Migration + regression pins

<goal>
PlanningMaterializerTests stops pinning index generation and pins the new contract: no pass creates the index; a legacy tracked row migrates on the next pass with 033 semantics intact.
</goal>

<scope>
File: `tests/Overmind.Infrastructure.Tests/PlanningMaterializerTests.cs`. Rework existing index assertions (~lines 77, 173–266, 434–448, 543, 641–676, 827–888, 965, 1017, 1063, 1341–1366, 1539): generation expectations flip to "no BACKLOG.md written, no row tracked". Add migration pins:
a) legacy tracked row + block in sync -> the pass strips the block, hand prose stays byte-identical, row deleted, subsequent passes never touch the file again;
b) drifted (hand-edited) block -> refusal with nothing written; with `force` -> strip + per-file warning (033 kept-foreign path);
c) file without markers (hand-only content) -> row dropped as NotFound, file untouched;
d) fresh project, strategy on -> no BACKLOG.md on disk, no tracked row;
e) strategy switch archive->off with a legacy archive-root copy -> archive copy deleted, repo prose kept (extends the existing `StrategySwitch_ArchiveToOff_*` trio);
f) drift detection on a legacy row before any pass -> no false Modified (the row hash covers the block only).
</scope>

<anti-scope>
Consumer test files (task 03 owns them); production code beyond what task 01 already changed; schema.
</anti-scope>

<constraints>
Tests describe behavior, not implementation details; keep the file's existing helper/naming style; `dotnet test tests/Overmind.Infrastructure.Tests` green before handoff.
</constraints>

<acceptance-criteria>
- [ ] All generation pins reworked; suite green.
- [ ] Pins a–f present and passing.
- [ ] No test asserts the index is created or refreshed anywhere.
</acceptance-criteria>

<refs>
spec cc4c4392-566e-4ff3-a84a-ff6aa61914fd; plan cf343934-d939-482d-9dd2-36dc8e41287b step 2; 033-materializer-clobber-guard.
</refs>

<obstacles>
None at creation.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan step 2
</log>