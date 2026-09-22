---
feature: 043
task: 02
slug: status-provenance
status: done
review: light
created: 2026-09-22
brief: Three factual lifecycle rules + marker legend in quickstart (pinned), marker round-trip through import, result artifact
---

# 043/02 — Lifecycle docs + marker legend + round-trip + result

<goal>
The quickstart/onboard lifecycle block states the three factual rules the field could not derive, plus the marker legend; marker lines survive materialisation import intact; the feature gets its result artifact.
</goal>

<context>
Task 01 stamped the lines; this task makes the rules they express discoverable BEFORE an agent hits them. The three rules come from the spec's code survey (D3): done + new artifact stays done; no plan → no auto-promotion even at 100% tasks done; import restores statuses verbatim (imported done ≠ auto-closed).
</goal>

<scope>
- Quickstart (OnboardQuickstart.Text) lifecycle block: add the three factual rules (one line each, precise wording) and the marker legend (one line: `[agent]`/`[ui]`/`[auto]`/`[import]` in log lines mark who moved a status).
- If the onboard lifecycle block is separate from the quickstart, mirror there.
- Materialisation round-trip test: a materialised file with sourced lines re-imports without corruption or duplication (legacy + marker lines together).
- Pins: quickstart rules verbatim; marker legend; service-line formats from task 01.
- Result artifact on the board (HTTP POST to the production host 4761, route shape from the repo's Api code; no MCP) — what shipped, evidence, deviations.
</scope>

<anti-scope>
- No web UI rendering of sources; no transition changes; no audit table.
- No deploy (wave bundles at the end).
</anti-scope>

<constraints>
- English texts; no comments; no new packages; no commits; do not flip feature/task statuses — orchestrator's move.
- Gates: dotnet build (0W/0E) + dotnet test green (baseline after task 01).
</constraints>

<acceptance-criteria>
- Quickstart carries the three rules + legend, pinned; round-trip green; result artifact on the board.
- git status: quickstart/onboard + tests + the result materialisation only.
</acceptance-criteria>

<refs>
- Spec 043 D3, R4, R5; task 01's landed line formats.
- OnboardQuickstart.Text (042 pattern); 032 pin pattern.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-22 — Execution starts: coder takes the docs/legend/round-trip contract (task 01 reviewed PASS-WITH-NITS, its two nits fixed in-tree by a parallel micro-round).
- 2026-09-22 — Light review PASS (0 findings, audits/review-043-02.md): all three rules fact-checked against the code, marker legend matches the enum, round-trip asserts the second pass for the right reason, result artifact verified live.
- 2026-09-22 — Accepted — provenance shipped end to end: sourced lines on every door, factual lifecycle documentation, marker round-trip. All tasks of 043 done; the p1 field-feedback batch is complete.
</log>
