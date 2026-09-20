---
feature: 034
slug: feature-priorities
status: draft
created: 2026-09-20
---

# Plan 034 — Feature priorities: прио как данные (поле + сортировка/фильтр борды + MCP + round-trip)

<approach>
Four tasks along the layer line: Core enum + storage + both create doors → disk round-trip → web surface → live dogfood. The priority is data from day one: integer-persisted enum, lowercase wire format ("p0".."p3"), P2 default everywhere (migration backfills all existing features), agent-door updates stay a non-goal v1 (no MCP update-feature tool exists — same door class 023/08 closed for status).
Pinned during spec walkthrough:
- PlanningFeatureTransitions-style single source: the enum lives in Core next to the status enums; serialization rides the existing camelCase JsonStringEnumConverter with lowercase members (member names P0..P3 serialize via custom naming — exact mechanism pinned at task level against how PlanningFeatureStatus serializes today; if the converter doesn't lowercase naturally, a JsonStringEnumConverter with a naming policy or explicit mapping in contracts).
- Human door after create: PUT UpdateFeature carries priority (web editor select needs it); agent door has no update tool — non-goal recorded in spec.
- Frontmatter `priority: p1` round-trips through feature.md; importer treats missing/unknown as P2 and adds a report line (import downgrade is visible, never silent).
- Web default sort becomes (priority asc, updatedAt desc) — the board's current sort (slug/number order today) is superseded; number prefix in slug keeps human readability inside a priority bucket.
</approach>

<tasks>
01 — Backend core: FeaturePriority enum (Core), PlanningFeature.Priority + EF migration (default P2, backfills existing rows), create paths on both doors (PlanningFeaturesApi.Create request field + planning_create_feature MCP param), response contracts + mapping, typed 400 on unknown value, tests (create with/without priority; default P2; unknown value 400; both doors).
02 — Disk round-trip: materializer emits `priority: pN` frontmatter in feature.md; importer parses it back (missing/unknown → P2 + report line); round-trip + byte-stability tests.
03 — Web: priority chip on cards + workspace header (tones p0 danger / p1 warning / p2 neutral / p3 muted), default sort (priority asc, updatedAt desc), filter by priority in board-filter-bar, priority select in feature editor (create + update via human door); lint + tsc + build green.
04 — Live dogfood + closeout: set the real queue priorities on this board per the 035 snapshot (026/029/028/030 → P1; default P2 stays; parked/dormant → P3 where fitting), materialize, verify frontmatter round-trip on disk, result artifact.
</tasks>

<verification>
- dotnet build + full suite green (baseline 969) after 01/02; web lint + tsc + build green after 03.
- Round-trip: create feature with priority p1 via MCP → materialize → frontmatter shows priority: p1 → wipe-and-import keeps it; unknown frontmatter value imports as P2 with a report line.
- Live: board sorted by priority buckets after 04; chips render with tones; filter works.
</verification>

<risks>
- Enum serialization casing: the wire format must be lowercase "p0".."p3" — pin by test, not by convention (the status enums' casing precedent decides the mechanism).
- Sort stability inside a priority bucket: updatedAt desc makes recently-touched features jump inside a bucket — accepted (spec decision), but pin the composite sort in a web test if one exists, else verify live.
- Migration on a live DB (prod board): additive column with default — safe shape, but the deploy rides the next pack anyway.
</risks>