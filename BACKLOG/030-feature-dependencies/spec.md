---
feature: 030
slug: feature-dependencies
status: validated
created: 2026-09-24
validated: 2026-09-24
---

# Spec 030 — Feature dependencies: structured blockedBy/blocks (не текстом в body)

<goal>
Structured inter-feature dependencies (blockedBy/blocks) as first-class data with enforcement: a feature with unclosed blockers cannot be picked up through MCP doors, plus a dedicated editing surface — UI panel for the human, dedicated MCP tools for agents — replacing free-text dependency notes in bodies.
</goal>

<context>
Mindlash feedback 2026-09-20: dependencies lived as text in feature bodies. 2026-09-24 the user hardened the requirements twice: (1) enforcement — a blocked feature cannot be started before its blockers close; (2) a dedicated editor surface, not a text block. User decisions from the Socratic round (2026-09-24, in-session): cycles rejected at create; current_task stops issuing but reading stays open; a dependency closes at done OR review; enforcement sits on MCP doors only; UI editor lives in the feature workspace; MCP surface = dedicated tools.
</context>

<scope>
- Data: PlanningFeature gets dependency references blockedBy/blocks (slug + resolved id, same project). One direction stored, both directions derived and exposed.
- Cycle prevention: adding a dependency rejects any edge that completes a cycle (direct or transitive), reporting the chain.
- Enforcement — MCP doors only:
  - planning_current_task on a feature with unclosed blockedBy returns a stop state listing unclosed blockers; the feature and its artifacts stay readable.
  - planning_set_current refuses to pin a blocked feature, with diagnostics.
  - planning_set_feature_status and every other surface stay open — the human door is never blocked.
- A dependency is closed when the blocking feature reaches done or review; any other status (draft/ready/inProgress) keeps the block.
- MCP surface: add_feature_dependency(projectId, slug, blockedBySlug) / remove_feature_dependency / list_feature_dependencies — dedicated tools; create/update feature signatures unchanged.
- UI: feature workspace gets a Dependencies panel — add/remove blockers, chips "waits for N"; dashboard card shows a blocked indicator/chips only, no editing from the card.
- Board: "blocked" filter among status filters; blocked features visually distinct.
- Materializer writes dependency links into feature.md frontmatter; importer reads them back — round-trip.
- Auto-promotions ignore blocked state: dependency is about "who is next", not about statuses.
</scope>

<anti-scope>
- Cross-project dependencies — 028-cross-project-bug-intake.
- Enforcement on feature status transitions (set_feature_status works on blocked features).
- Dependency editing from the dashboard card.
- Queue/priority ordering semantics — 072-planning-task-queue, built on top of this data.
- archived/delete interplay (OM-17) — revisit when an archived state exists.
</anti-scope>

<constraints>
- Same project only (single projectId).
- Provenance markers ([agent]/[ui]/[auto]/[import]) apply to dependency mutations like everywhere else.
- Import restores dependencies verbatim from frontmatter; export/import round-trip must not lose links.
</constraints>

<acceptance-criteria>
- [ ] add_feature_dependency records a link; list_feature_dependencies shows both blockedBy and blocks sides
- [ ] add_feature_dependency rejects a cycle (direct or transitive), naming the chain
- [ ] planning_current_task on a blocked feature returns a stop state listing unclosed blockers; the feature body stays readable
- [ ] planning_set_current refuses a blocked feature with diagnostics
- [ ] Blocker reaching review or done lifts the block; a move back to draft/ready/inProgress re-blocks
- [ ] Dependencies panel in the feature workspace adds/removes blockers; the dashboard card shows blocked chips, no editor
- [ ] Board "blocked" filter shows only features with unclosed blockedBy
- [ ] Materialization round-trip: dependencies survive export and import via feature.md frontmatter
- [ ] dotnet build passes
</acceptance-criteria>

<refs>
028-cross-project-bug-intake (cross-project deps, future); 072-planning-task-queue (consumer of this data); feature body 030 (user decisions 2026-09-24)
</refs>

<obstacles></obstacles>

<log></log>