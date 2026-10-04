---
feature: 105
task: 02
slug: register-all-task-sections
status: todo
review: light
created: 2026-10-04
depends_on: 01
brief: Door pins + consistency + architect + result
---

# Task 02 — Door pins + closeout

<goal>
Pin the behavior on both doors and close the wave.
</goal>

<scope>
- MCP/HTTP door pins: canonicalization observable through both doors; import round-trip byte-stable for already-canonical bodies.
- Consistency pass; architect light; result with post-release click-check (upsert a body with an inline section, patch it).
</scope>

<acceptance-criteria>
- [ ] Door pins green; import round-trip pinned.
- [ ] Architect verdict recorded; result drafted with click-check.
</acceptance-criteria>

<refs>
- Spec 110bcde0; plan 7eecf2c9 (task 2).
</refs>