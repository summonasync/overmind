---
feature: 074
slug: materialization-run-diagnostics
status: done
priority: p1
version: 1
created: 2026-09-24
---

# Feature 074 — Materialization run diagnostics: lastSuccess/lastAttempt, per-file outcome, atomicity, diagnostic tool

<goal>
An agent (or human) can read the true state of the materialized mirror from tool responses: last success and last attempt reported separately, per-file run outcome (written/skipped/refused), stalePaths instead of a one-line prose blob, atomic run semantics (write everything or nothing), and an index_status-style diagnostic MCP call answering "what lives where, why did it refuse, when was the last success" without filesystem access.
</goal>

<requirements>
- overmind_onboard / planning_materialize responses distinguish lastSuccess from lastAttempt (the BobHouse 020 session saw lastRun=never(failed …Refusing to overwrite…) while the file had actually been regenerated a minute earlier).
- Per-file run outcome and stalePaths surfaced structurally.
- Materialization run is atomic: a refusal mid-run must not leave the tree in a mixed state.
- Diagnostic tool (index_status-style) exposing mirror state, tracking, last outcomes — the "agent could never look inside the state" gap; DB closed, filesystem unreachable, tool answers are the only channel.
</requirements>

<non-goals>
- Changing the clobber-guard policy itself (033/067 behavior stays); git hygiene (075); naming (076).
</non-goals>

<refs>
Source: note «Полевые заметки агента: Overmind MCP на живой ceremony-сессии (BobHouse 020, 2026-09-24)», B1 + "честное ограничение" section (entry c7f34f40-3cea-49ec-8e23-bb1d8fe8e9c9). Related done work: 054, 033, 067. Microbatch relatives: #49, #56c.</refs>

<log>
- 2026-09-25 [auto] — artifact created → inProgress
- 2026-09-25 [auto] — all tasks done + spec + plan → review
- 2026-09-25 [agent] — Done by user request in-session («закрывай»): both tasks done (01 through a BLOCKED→fixed→re-approved cycle), spec 1f69dd0b + plan b8e98220 validated, results ecff2ad2 validated, live atomic-abort walk verified on an isolated host. Policy flag (067-D2 supersede: skip-not-abort → refusal+atomic-abort) documented in the result deferred section — user review welcome; field verification by MCP users after the deploy.
</log>
