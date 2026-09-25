---
feature: 078
slug: upsert-log-guard
status: draft
priority: p1
version: 2
created: 2026-09-24
---

# Feature 078 — Upsert log guard: full-body upsert may only append to <log>, never rewrite history

<goal>
The <log> section is append-only on EVERY write door: planning_upsert_artifact resends the full body and currently rewrites <log> history — the evidence base of "who knew what and when" — while the patch door is already guarded (patching log returns 400).
</goal>

<requirements>
- On upsert, compare the incoming <log> section with the stored one; only tail-appenditions are allowed, anything else is rejected with diagnostics.
- The existing append path (planning_append_log) and patch-door guard (039) stay as they are.
- Pin with a test: upsert attempting to modify/remove a historical log line fails; pure tail-append passes.
</requirements>

<non-goals>
- Status-move log lines (planning_set_status mechanics); version-diff tooling (045).
</non-goals>

<refs>
Source: note «Полевые заметки агента (BobHouse 020, 2026-09-24)», B5 (entry c7f34f40-3cea-49ec-8e23-bb1d8fe8e9c9). Related: 039 (review — patch-door guard), microbatch #36/#43 (body-mutating paths outside snapshot discipline).</refs>

<log>
- 2026-09-24 [ui] — status moved to 'inProgress' via ui
- 2026-09-24 [ui] — status moved to 'draft' via ui
</log>
