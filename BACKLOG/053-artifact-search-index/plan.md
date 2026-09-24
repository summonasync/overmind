---
feature: 053
slug: artifact-search-index
status: validated
created: 2026-09-24
validated: 2026-09-24
---

# Plan 053 — Artifact search index: тела артефактов планирования в поиске (FTS twin для entry_fts), единый search с source-дискриминатором

<approach>
Two tasks along the data path: schema first (twins + triggers + backfill — pure migration, provable on real data), then the search surface (union service + hit shapes + MCP projection + pins). The entry_fts pattern is copied, not invented; the risky part is the UNION (ranking merge, limit-after-merge) and the entry byte-compatibility, so task 02 pins both hard.
</approach>

<tasks>
- [ ] 01-fts-twins-backfill — Infrastructure migration: feature_fts(title, body) + artifact_fts(body), contentless fts5 with insert/update/delete triggers over PlanningFeatures and PlanningArtifacts (the entry_fts pattern at InitialSchema.cs:227-251, incl. its contentless_delete mechanics — copy exactly how entry handles delete); backfill both from existing rows in the same migration; down-migration drops triggers+tables per the entry pattern. Tests: migration round-trip on a pre-existing DB shape (features+artifacts seeded before the migration migrate up → both fts populated with correct counts); trigger coverage per entity: create → found, body patch → new term found/old gone, delete → gone, import-path insert → found. Verify: build + tests green → tasks/01-fts-twins-backfill.md
- [ ] 02-union-surface — Search surface: the union query (entries + features + artifacts, source-tagged, bm25 rank merged, LIMIT applied AFTER the merge); SearchHitResponse extends per spec D3 (entries byte-identical; feature hits {featureSlug, title}; artifact hits {artifactId, featureSlug, kind, order, label "NNN kind"}); MCP search projection adds the source fields (still zero bodies) + the description states fulltext covers planning artifacts, semantic stays entries-only; pins: entry-compatibility (existing payload shape pinned), per-source hit shapes, union ranking order, projection source fields, description verbatim. Verify: build + full tests green + the field acceptance pinned (a term inside a task body found with snippet+featureSlug+artifactId) → tasks/02-union-surface.md
</tasks>

<risks>
- **Contentless delete mechanics**: entry_fts uses contentless_delete=1 — its trigger does a plain DELETE; verify that still holds for the twins (copy, don't reinterpret; if SQLite version behaves differently, surface it in the report, don't hack).
- **Rank merge across three sources**: bm25 scores are not comparable across tables — v1 merges by rank ordering (per-source order, interleaved); the spec's open question already recommends raw union; document the exact rule in the code's test names.
- **Web breakage via hit shape**: entries must stay byte-identical — the compatibility pin is the gate.
</risks>

<verification>
- dotnet build + dotnet test green after each task (baseline 1218).
- Migration round-trip on legacy shape; trigger coverage both entities; union ordering; entry compatibility; MCP projection.
- Reviews: light on both.
</verification>