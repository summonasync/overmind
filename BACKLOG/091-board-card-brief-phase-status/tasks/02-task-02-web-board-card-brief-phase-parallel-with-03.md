---
feature: 091
task: 02
slug: board-card-brief-phase-status
status: todo
review: light
created: 2026-10-03
depends_on: 01
brief: Card renders brief + phase label from the new payload fields, presence tests
---

# Task 02 — Web board card: brief + phase (parallel with 03)

<goal>
Card renders the ceremony context: brief between priority/version row and task counter, phase label next to the status column.
</goal>

<scope>
- web feature board card components: brief line (from the new payload fields) rendered between the priority/version row and the task counter; phase label rendered next to the status column — consistent tone/styling with existing chips.
- Done cards follow the existing done treatment (phase shows done or is suppressed — match the current done-card look, do not invent a second representation).
- Presence tests: brief renders when present, absent when null; phase label renders per state; card without body renders without brief line.
</scope>

<anti-scope>
No queue work (task 03), no backend changes, no new packages.
</anti-scope>

<constraints>
FSD, English UI, no comments by default, no auto-commits; gates lint/tsc/build/test real numbers.
</constraints>

<acceptance-criteria>
- [ ] lint + tsc + build + test green
- [ ] Brief renders between priority/version row and task counter when present; absent when null
- [ ] Phase label renders per state; done cards match existing done treatment
- [ ] Presence tests pin the three render branches
</acceptance-criteria>

<refs>
- Spec: d066ec12 (Surfaces).
- Task 01 (order 1) — payload fields.
</refs>

<obstacles></obstacles>

<log></log>