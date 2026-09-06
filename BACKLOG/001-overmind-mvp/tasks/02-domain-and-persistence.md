---
feature: 001
task: 02
slug: domain-and-persistence
status: todo
review: light
created: 2026-09-06
---

# 001/02 — Domain model and persistence

<goal>
Core domain entities and interfaces are defined in `Overmind.Core`, and `Overmind.Infrastructure` persists them to SQLite via EF Core with a working FTS5 full-text index over entry content. Verified by unit and integration tests.
</goal>

<context>
Overmind stores per-project context entries (notes/decisions, gates, guards, skills) plus skill-kits and materialization state. SQLite is the only metadata store; FTS5 must always work with zero external services. Task 03 (API) and 04 (materialization) build directly on these types — signatures here are a contract.
</context>

<scope>
`src/Overmind.Core/`:
- `Project` — Id (Guid), Name, DiskPath, Description, CreatedAt.
- `ContextEntry` — Id, ProjectId, Type (`EntryType` enum: Note, Decision, Gate, Guard, Skill), Title, Content (markdown), CreatedAt, UpdatedAt. Skills additionally carry per-platform file payloads.
- `SkillFile` — Id, EntryId, Platform (`AgentPlatform` enum: Claude, KimiCode, Codex, Gemini, Antigravity), RelativePath (e.g. `reviewer/SKILL.md`), Content.
- `EntryTag` / `Tag` — many-to-many between entries and tags.
- `EntryVersion` — Id, EntryId, Version (int), Content snapshot, CreatedAt. New version row on every content update.
- `SkillKit` — Id, Name, Version, Description; `SkillKitEntry` join (KitId, EntryId).
- `ProjectKit` — ProjectId, KitId, IsActive (many-to-many with activation flag).
- Interfaces: `IEmbeddingProvider` (Task 05 implements: `Task<float[]> EmbedAsync(string text, CancellationToken ct)`, `int Dimensions`, `string Name`), `ISemanticSearch` (Task 05 implements: `Task<bool> HealthAsync(CancellationToken ct)`, `Task<IReadOnlyList<EntrySearchHit>> SearchAsync(Guid? projectId, string query, int limit, CancellationToken ct)` — `EntrySearchHit` defined here in Core). `IMaterializer` is NOT created here — task 04 defines it together with its DTOs.

`src/Overmind.Infrastructure/`:
- `OvermindDbContext` — EF Core SQLite, WAL journal mode on connection open.
- Entities mapped above; migrations in Infrastructure (`dotnet ef migrations add InitialSchema`).
- FTS5: migration creates `entry_fts` virtual table (`fts5(title, content, content='')`) + triggers syncing from `ContextEntries` (insert/update/delete). FTS sync via triggers ONLY — never in C# code.
- `FtsSearchService` — `Task<IReadOnlyList<EntrySearchHit>> SearchAsync(Guid? projectId, string query, int limit, CancellationToken ct)` using MATCH with bm25 ranking. `EntrySearchHit`: EntryId, Title, Type, Snippet (use `snippet()` function), Rank.

Tests:
- `tests/Overmind.Core.Tests/` — enum/entity invariants (e.g. Skill entries require ≥1 SkillFile — enforce via factory method, not data annotations).
- `tests/Overmind.Infrastructure.Tests/` — real SQLite in temp file: CRUD roundtrip, FTS trigger sync (insert → searchable; update → new content found; delete → not found), version row created on update.
</scope>

<anti-scope>
- No HTTP/API code, no endpoints.
- No embeddings/Qdrant/vector columns (task 05).
- No materialization logic (task 04) — only the entities it needs.
- No EF configurations for things not in scope.
</anti-scope>

<constraints>
- EF Core Sqlite (central versions from task 01). No other ORM.
- Factory methods on entities for creation with invariants; parameterless ctor private for EF.
- All timestamps UTC.
- `Guid` ids, not ints.
- No comments; English-only identifiers.
</constraints>

<acceptance-criteria>
- [ ] `dotnet build` clean; `dotnet test` green (all new tests pass).
- [ ] Migration applies to a fresh SQLite file and produces `entry_fts` + 3 sync triggers (verified in integration test via `sqlite_master` query).
- [ ] FTS test proves: insert→found, update→new content found, delete→gone.
- [ ] Updating an entry's content appends an `EntryVersion` row (integration test).
- [ ] `IEmbeddingProvider` (with `Dimensions`) and `ISemanticSearch` exist in Core with exactly the signatures above.
</acceptance-criteria>

<refs>
- @../plan.md — task 02
- @../spec.md — R3.1, R3.2, R5 (data model), decisions/data-model
- tasks/01-solution-scaffold.md — must be done first
</refs>

<obstacles>
<!-- Filled by executors during work -->
</obstacles>

<log>
2026-09-06 — writing-plans — task created from approved spec.
</log>
