---
feature: 104
task: 02
slug: materialization-off-write-path
status: done
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
- [x] Timeout → probe → all three outcome texts pinned on the stub.
- [x] One shared handler, no per-tool copies.
</acceptance-criteria>

<refs>
- Spec 9d4e5c9b; plan 6b287810 (task 2).
</refs>

<log>
- 2026-10-04 [agent] — [agent] Taken by orchestrator after task 01 closed (review PASS-WITH-NITS, W1 fixed in 7366ff1) — dispatching dotnet-coder (sonnet).
- 2026-10-05 [agent] — [agent] Light review PASS-WITH-NITS (report audits/review-104-02.md; gates re-run: build 0w, Mcp 451, format clean). W1+W2 fixed in-tree (5189ae6): probe now matches the tail of the whole log section (multiline logNotes), and a section-less read-back settles unknown instead of no-trace — a committed write can no longer be cleared for resend. Mcp re-run 454/454 (+3 pins). N1 (marker reuse/concurrent-write residual) — recorded in closeout result; N2 (entries/notifications raw wording) — decision deferred to 104/03; N3 (unpinned decisions ct-cancel/probe-unreachable/STOP-passthrough) → microbatch. Both AC ticked. Closing task.
</log>
