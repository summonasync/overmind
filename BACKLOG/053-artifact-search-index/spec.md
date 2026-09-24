---
feature: 053
slug: artifact-search-index
status: validated
created: 2026-09-24
validated: 2026-09-24
---

# Spec 053 — Artifact search index: тела артефактов планирования в поиске (FTS twin для entry_fts), единый search с source-дискриминатором

<decisions>
- **D1 — Triggers, not service-layer indexing (agent).** The entry_fts pattern (contentless FTS5 + insert/update/delete triggers) is replicated for planning: every door that writes a body — upsert, patch, append_log, import, delete — is covered by construction, nothing can forget to index. Service-layer hooks would repeat the 043 audit exercise forever.
- **D2 — Two twin indexes, not one (agent).** artifact_fts over PlanningArtifacts (column: body) and feature_fts over PlanningFeatures (column: body — feature titles ride the board listing, not search). Separate tables because Features and Artifacts have independent rowid spaces; a unified index would need a surrogate-key map table no trigger can maintain. Search = UNION of the two queries with a source discriminator, merged and re-ranked by rank.
- **D3 — The hit shape extends, entries stay byte-compatible (agent).** SearchHitResponse gains source ("entry" | "feature" | "artifact"); entry hits keep entryId/title/type exactly as today (the web UI eats this payload untouched); feature hits carry featureSlug + title; artifact hits carry artifactId, featureSlug, kind, order and a human label ("039 spec" style) in the title slot. The MCP projection (040/02) adds the source fields, still zero full bodies.
- **D4 — Artifacts are fulltext-only in v1 (agent).** Semantic (Qdrant) keeps covering entries; artifact/feature bodies stay out of vector search until a chunking story exists (the bodies are 13-20KB contracts — naive whole-body embeddings are noise). The search tool's mode note states this.
- **D5 — The migration backfills (agent).** The new migration creates the twins + triggers and backfills from existing bodies (INSERT INTO … SELECT for both tables); upgrade path for live projects (ours included) is the migration alone — no re-import, no manual reindex.
</decisions>

<open-questions>
- Should feature TITLE be searchable (not just body)? Recommendation: yes, cheap — feature_fts gets (title, body) like entry_fts; the title match surfaces the feature even when the term lives only in the title.
- Ranking across sources: raw bm25 union, or a light source bias (entries first for equal rank)? Recommendation: raw union in v1 — the field asked "which artifact discusses X", not "rank sources".
</open-questions>

<requirements>
- R1 — Migration: feature_fts(title, body) + artifact_fts(body), contentless + triggers per the entry pattern; backfill both from existing rows.
- R2 — FtsSearchService (or a sibling) returns the union: entries (as today) + features + artifacts, source-tagged, rank-merged, limit applied AFTER the merge.
- R3 — SearchApi hit shape per D3; web UI payload for entries byte-identical (pinned by existing web tests if any, plus a new Api pin).
- R4 — MCP search projection carries source + the per-source id fields; the description states entries + planning artifacts (features and their spec/plan/task/result bodies) are fulltext-searchable, semantic is entries-only.
- R5 — Tests: migration backfill counts; trigger coverage (create → found, patch body → new term found, old term gone, delete → gone, import → found) for BOTH entities; union ranking; hit shapes per source; entry-compatibility pin; MCP projection pin.
- R6 — The field acceptance: a term living inside a task body is found with featureSlug + artifactId + a snippet — no grep over BACKLOG/ needed.
</requirements>

<non-goals>
- Semantic/vector search over artifacts or features (D4).
- Searching the web UI's own surfaces; new UI.
- Version-history indexing (current bodies only).
- Cross-project search.
</non-goals>

<context>
Field sources: FEATURELIST/05 (grep-equivalent ask, three authors), the 2026-09-22 finding (note fa93b4ca: both indexes cover ContextEntries only), OM-6/OM-14 round 3 (search "does not see artifacts"). Code facts: entry_fts pattern at InitialSchema.cs:227-251; FtsSearchService.SearchAsync (MATCH + LIMIT); SearchApi maps hits at :92; PlanningArtifact bodies mutate through the surveyed 043 doors; PlanningFeature bodies through UI/agent/import. User verdict 2026-09-22 raised this to p0: "artifacts are an important part; without them every lookup falls back to grepping files".
</context>