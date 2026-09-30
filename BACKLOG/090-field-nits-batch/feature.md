---
feature: 090
slug: field-nits-batch
status: inProgress
priority: p1
version: 1
created: 2026-09-26
---

# Feature 090 — Field-report nits batch: brief/title patch, feature body-only read, 404 alias hints, no-plan quickstart line (бывш. 082)

<goal>
Close the small items left after the BobHouse 020 and MindlashStudio intake waves — each too small for its own feature, together one focused batch. (Recreated under number 090: originally created as 082-field-nits-batch, colliding with the existing done 082-status-model-self-documentation — create_feature checks slug uniqueness but not the number prefix; that hole is 089.)

<requirements>
1. brief/title patch: typo in one brief line must not cost a full body re-upsert + version bump (patch op for brief/title).
2. Body-only feature read: option to fetch just the feature body without dragging every artifact (planning_read_feature sections= or a flag).
3. `overmind://resources` returns "Unknown resource URI" — RESOLVED BY 070 (both dead-pointer sites removed, absence-pinned); kept here for the record, mark superseded at spec time.
4. Artifact 404 hints: typoed artifactId → bare 404; error text should list nearby candidate ids (e.g. by order/kind within the feature) or support `NNN/T3` aliasing.
5. No-plan-no-auto-review (decision C from the 038 quirk note): state it in the quickstart and affected tool descriptions — "all tasks done + spec + plan validated → review; no plan → flip manually".
</requirements>

<non-goals>
- New listing surfaces (080), task dependencies (081), mutation-echo work (068/070).
</non-goals>

<refs>
- BobHouse 020 field note B6; 038 quirk note (decision C).
- 047-api-read-surface-polish — sibling doc-gap batch.
- Item 3 superseded by 070 (R5) — confirmed live 2026-09-26.
</refs>

<log>
- 2026-09-29 [auto] — artifact created → inProgress
</log>
