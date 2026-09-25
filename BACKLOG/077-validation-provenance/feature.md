---
feature: 077
slug: validation-provenance
status: draft
priority: p1
version: 1
created: 2026-09-24
---

# Feature 077 — Validation provenance: planning_validate requires proof of human approval (token or explicit human path)

<goal>
planning_validate carries structural proof of human approval, not the agent's word: today, with humanGate=off, an agent validated both spec and plan in one call with no STOP and no confirmation token — "spec/plan validated by the user" rests entirely on prompt fidelity.
</goal>

<requirements>
- For spec/plan validation require evidence: a token issued via the overmind_replies path on an approval notification, or an explicit --by=human path — not prose discipline.
- In-session chat approval (the 055-legalized flow) must keep working — capture it through the same evidence mechanism.
- Provenance of "who validated" visible in the artifact log.
</requirements>

<non-goals>
- Flipping humanGate default (055 decided: off); gating task/result artifacts; UI changes.
</non-goals>

<refs>
Source: note «Полевые заметки агента (BobHouse 020, 2026-09-24)», B4 (entry c7f34f40-3cea-49ec-8e23-bb1d8fe8e9c9). Related done: 055, 043; microbatch #43 (Validate without sourced log line).</refs>