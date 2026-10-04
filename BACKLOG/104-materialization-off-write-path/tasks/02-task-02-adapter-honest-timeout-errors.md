---
feature: 104
task: 02
slug: materialization-off-write-path
status: todo
review: light
created: 2026-10-04
depends_on: 01
brief: Adapter: verify-probe on timeout keyed by logNote, three honest outcomes, stub tests
---

# Task 02 — Adapter: honest timeout errors

<goal>
A mutation timeout becomes a committed/not-committed answer, not silence.
</goal>

<scope>
- Shared mutation-path handler in the adapter: on timeout/dead connection run a verify probe — targeted read-back of the mutated artifact + logNote match against lastLogLine.
- Three outcomes: committed at vN — do not resend / no trace — resend safe / unknown — read before resending.
- Stub contract tests for all three outcomes.
</scope>

<acceptance-criteria>
- [ ] Timeout → probe → all three outcome texts pinned on the stub.
- [ ] One shared handler, no per-tool copies.
</acceptance-criteria>

<refs>
- Spec 9d4e5c9b; plan 6b287810 (task 2).
</refs>