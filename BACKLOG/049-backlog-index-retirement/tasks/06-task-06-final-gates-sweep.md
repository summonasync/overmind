---
feature: 049
task: 06
slug: backlog-index-retirement
status: todo
review: light
created: 2026-10-06
depends_on: 04, 05
brief: Final gates: dotnet build + full serial dotnet test + BACKLOG.md grep sweep (residues only where intended) + two checkpoint commits (code+tests / migration+docs), no push.
---

# Task 06 — Final gates + sweep

<goal>
The feature closes with green gates and a clean reference sweep; work is checkpoint-committed in two reviewable commits.
</goal>

<scope>
1. `dotnet build` green; full serial `dotnet test` green.
2. `grep -rn "BACKLOG\.md"` over `src/`, `tests/`, living root docs, kit living files — only intended residues pass: materializer legacy-cleanup code + its tests, AgentFileConflictScanner + fixture, kit tombstone header, FEATURELIST history, materialized trees. Anything else is a finding -> fix or obstacles note.
3. Checkpoint commit per crew rule 6 (conventional, attributed, no push), split for reviewability: (a) code + tests, (b) migration + docs + pointer.
</scope>

<anti-scope>
New scope items; git push; any fix that escapes the feature's spec — new findings go to the board, not into this task.
</anti-scope>

<constraints>
No push; commits only as checkpoints per rule 6; sweep output recorded in the log.
</constraints>

<acceptance-criteria>
- [ ] Build + full serial tests green.
- [ ] Sweep clean of unintended references (output in log).
- [ ] Two checkpoint commits landed, push untouched.
</acceptance-criteria>

<refs>
spec cc4c4392-566e-4ff3-a84a-ff6aa61914fd; plan cf343934-d939-482d-9dd2-36dc8e41287b step 6.
</refs>

<obstacles>
None at creation.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan step 6
</log>