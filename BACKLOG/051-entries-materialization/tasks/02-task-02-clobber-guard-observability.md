---
feature: 051
task: 02
slug: entries-materialization
status: todo
review: light
created: 2026-10-04
depends_on: 01
brief: Clobber-guard over ENTRIES/ + report/status lines + tests
---

# Task 02 — Clobber-guard + observability

<goal>
033 semantics extend to ENTRIES/: hand edits refuse, force warns, failures surface.
</goal>

<scope>
- Clobber-guard over ENTRIES/ tracked files (hand-edited → refusal; force → per-file warnings).
- Skipped/refused lines ride the existing materialize report/status surfaces (074).
- Tests: guard refusal, force pass with warnings, report lines.
</scope>

<acceptance-criteria>
- [ ] Guard refusal + force warnings pinned.
- [ ] Report/status lines for ENTRIES/ present — pinned.
</acceptance-criteria>

<refs>
- Spec a87863c1; plan 46b7e368 (task 2); 033 guard; 074 reporting.
</refs>