---
feature: 053
slug: artifact-search-index
status: inProgress
priority: p0
version: 2
created: 2026-09-22
---

# Feature 053 — Artifact search index: тела артефактов планирования в поиске (FTS twin для entry_fts), единый search с source-дискриминатором

<goal>
Planning artifact bodies (spec/plan/task/result, the BACKLOG/ contracts) become searchable. Today both indexes cover ContextEntries only (`entry_fts` FTS5 triggers + Qdrant payloads), so "which artifact discusses X" has exactly one answer: grep over the materialised files — the fallback the field and the user both hit. Artifacts are the longest, most contract-heavy documents in the store; leaving them outside search makes the grep-equivalent ask of FEATURELIST/05 half-delivered (040 gave snippets for entries, nothing finds artifacts).
</goal>

<requirements>
- Fulltext index over artifact bodies (and decide in the spec: feature bodies too) — the entry_fts trigger pattern already exists; an artifact_fts twin is the proven road.
- Hits surface through the existing search path: one tool the agent already knows, not a second search to discover. Unified hit shape with a source discriminator (entry vs artifact) carrying artifactId, featureSlug, kind, order, snippet.
- Snippets via the same SQLite snippet() machinery the entries path uses; MCP projection extends the 040/02 shape with the source fields, still zero full bodies.
- Description of `search` states what is searchable (entries + planning artifacts) once both are in.
- Spec decides: semantic (Qdrant) for artifacts now or later (chunking story differs); reindex/upgrade path for existing projects (migration must populate the new index without a full reimport).
</requirements>

<non-goals>
- Searching across projects (single-project scope as today).
- Version history search (045 is diff, not index; only current bodies).
- Web UI changes beyond what falls out of the shared endpoint naturally.
</non-goals>

<refs>
- Board note fa93b4ca (2026-09-22) — the finding, written up during 040/02 execution with candidate approaches.
- FEATURELIST/05-no-partial-artifact-read.md — the original grep-equivalent ask (three authors).
- FEATURELIST/01/03 — "search cost unclear, returns whole bodies" — solved for entries by 040/02; this extends it to artifacts.
- Implementation seam: FtsSearchService / entry_fts trigger pattern; 040/02 projection (src/Overmind.Mcp/SearchSnippet.cs) ready to carry source fields.
- User verdict 2026-09-22: artifacts are an important part; without them every lookup falls back to grepping files.
</refs>

<log>
- 2026-09-23 [auto] — artifact created → inProgress
</log>
