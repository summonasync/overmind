---
feature: 082
slug: field-nits-batch
status: draft
priority: p1
version: 1
created: 2026-09-26
---

# Feature 082 — Field-report nits batch: brief/title patch, feature body-only read, resources URI fix, 404 alias hints, no-plan quickstart line

<goal>
Close the small items left after the BobHouse 020 and MindlashStudio intake waves — each too small for its own feature, together one focused batch.

<requirements>
1. brief/title patch: typo in one brief line must not cost a full body re-upsert + version bump (patch op for brief/title).
2. Body-only feature read: option to fetch just the feature body without dragging every artifact (planning_read_feature sections= or a flag).
3. `overmind://resources` returns "Unknown resource URI" — the instruction in the MCP server prompt leads nowhere. Either implement the resource or remove the pointer from the prompt/quickstart.
4. Artifact 404 hints: typoed artifactId → bare 404; error text should list nearby candidate ids (e.g. by order/kind within the feature) or support `NNN/T3` aliasing.
5. No-plan-no-auto-review (decision C from the 038 quirk note, 2026-09-20): state it in the quickstart and affected tool descriptions — "all tasks done + spec + plan → review; no plan → flip manually".
</requirements>

<non-goals>
- New listing surfaces (080), task dependencies (081), mutation-echo work (068).
</non-goals>

<refs>
- BobHouse 020 field note B6 (note deleted after intake; items survive here).
- 038 quirk note: auto-promotion requires spec AND plan — decision C (document, don't weaken the matrix).
- 047-api-read-surface-polish — sibling doc-gap batch (p2, queue pos 6).
</refs>