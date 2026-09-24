---
feature: 068
slug: mutation-transparency
status: draft
priority: p1
version: 1
created: 2026-09-24
---

# Feature 068 — Mutation transparency: маркеры на всех путях записи, statusChanged в ответах, current_task различает result-состояния (OM-11/12/13)

<goal>
The agent SEES what its mutation did. Today: append_log and patch(logNote) write <log> lines WITHOUT the [agent] marker (only set_status paths carry it — OM-11); patching a validated artifact silently demotes it to draft — the response says "patched — version 2", the log carries only the user note, the feature stays in review on a now-invalid spec, and the tool text says "validated" while the 409 says "frozen" (OM-12); planning_current_task cannot tell result-drafted from result-validated ("write the result artifact" after it exists) and hands out tasks without warning when spec/plan are missing or unvalidated (OM-13).
</goal>

<requirements>
- OM-11: one log-line formatter for ALL write paths — append_log, patch (both ops), upsert-with-logNote, set_status, auto/import lines. Marker [agent] wherever an agent door wrote it (absorbs microbatch #45).
- OM-12: patch/upsert responses carry status and statusChanged ("validated → draft") when a demotion happens; a [auto] log line records it; decide-and-document whether the feature demotes from review when its spec stops being validated (either fix the rule or state it in the quickstart); unify wording "validated"/"frozen" to one term.
- OM-13: current_task distinguishes result-drafted ("validate it") and result-validated ("close the feature"); task payloads carry warnings: ["spec missing", "plan not validated"] when applicable (soft, not STOP).
</requirements>

<non-goals>
- Changing demotion semantics themselves (only their visibility + the review-question decision).
- Batch ops (044), diff (045).
</non-goals>

<refs>
- FEATURELIST/overmind-features-2026-09-24.md OM-11/OM-12/OM-13 (P1, with exact response examples).
- 043-status-provenance (the marker system this extends); 055/056 (the response/warning idioms this follows).
- Microbatch #45 (append_log markers — absorbed here, remove from the queue when this lands).
</refs>