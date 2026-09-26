---
feature: 089
slug: feature-number-collision-guard
status: draft
priority: p2
version: 1
created: 2026-09-26
---

# Feature 089 — Guard: create_feature отклоняет конфликт номера в слаге (две фичи №NNN недопустимы)

<goal>
Number prefixes on the NN-slug convention are the board's addressing layer (066 badge, references like "070/03"), yet nothing validates them: create_feature checks slug uniqueness only. Two features can legitimately end up sharing a number (e.g. a fresh `082-field-nits-batch` next to a done `082-whatever`), and references become ambiguous. The orchestrator hit the CONFUSION class today (2026-09-26): invented a nonexistent `082-status-model` slug in a report, chased a phantom collision, deleted and recreated an intake batch under 090 to be safe. A server-side guard makes the class impossible.
</goal>

<requirements>
- planning_create_feature (and the HTTP door) extracts the number prefix from the slug and refuses with a typed 409 NumberCollision when another feature already carries that number, any status (done included).
- The 409 detail names the colliding feature and suggests the next free number.
- planning_import_feature: verify behavior on number collisions with disk-wins stance and pin/document it (import restores verbatim — tolerance may be correct, but it must be explicit).
- Pins: collision refused 409 with the colliding slug + suggested number; distinct-number create unaffected; non-prefixed slugs stay legal (no number → no check).
</requirements>

<non-goals>
- Renaming/re-numbering existing features; enforcing the prefix convention on slags that legitimately have no number; batch/import behavior beyond documenting it.
</non-goals>

<refs>
- Orchestrator confusion incident 2026-09-26 (no live collision — the class is unexercised; this feature makes it impossible).
- 066-feature-number-styles, 065 badge, create_feature MCP + HTTP doors, planning_import_feature (058).
</refs>