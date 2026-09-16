---
feature: 001
task: 02
slug: overmind-mvp
status: done
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
- `dotnet ef` was already installed globally (10.0.9); it prints a non-fatal "tools older than runtime 10.0.11" warning. No install performed.
- `Microsoft.EntityFrameworkCore.Design` 10.0.11 added to `Directory.Packages.props` + `Overmind.Infrastructure.csproj` (`PrivateAssets="all"`). It is the design-time companion of the already-pinned EFCore.Sqlite and is required by `dotnet ef` scaffolding. Runtime dependency set unchanged.
- FTS5 gotchas: plain `content=''` contentless tables do not support UPDATE/DELETE at all, so the migration creates `entry_fts` as `fts5(title, content, content='', contentless_delete=1)` (SQLite >= 3.43; Microsoft.Data.Sqlite 10 bundles a newer SQLite). Consequence: column values of a contentless table read as NULL, so `snippet()` returns NULL — `FtsSearchHit.Snippet` is coalesced to `string.Empty`. Ranking via `bm25()` works fine.
- EF Core stores `Guid` keys as UPPERCASE TEXT in SQLite; hand-written SQL must pass `guid.ToString().ToUpperInvariant()` for comparisons (`FtsSearchService` does). EF's own parameters are unaffected.
- EF Core change-tracking gotcha: an entity with a store-generated (ValueGeneratedOnAdd) non-default Guid key that is added to a collection of an already-tracked principal is treated as *existing* (state Modified), producing an UPDATE that affects 0 rows. Fix: `EntryVersion.Id` is configured `ValueGeneratedNever()` in `OvermindDbContext` (Guid is assigned by the factory, not the DB). Migration was re-scaffolded after this change.
- Microsoft.Data.Sqlite connection pooling keeps a WAL file handle open after context dispose on Windows, blocking temp-file cleanup in tests; test connection strings use `Pooling=false`.
- Review-gate follow-up: `Versions.Max()` in `UpdateContent` was unreliable across sessions (in-memory collection empty on fresh load → duplicate `Version = 1` → unique-index violation). Fixed with persisted `ContextEntry.CurrentVersion` counter (option a). Lesson: never derive persisted sequence numbers from unloaded navigation collections.
</obstacles>

<log>
2026-09-06 — writing-plans — task created from approved spec.
2026-09-06 — implementer — Domain model in Overmind.Core (entities + factories with invariants, EntryType/AgentPlatform enums, IEmbeddingProvider/ISemanticSearch/EntrySearchHit). Overmind.Infrastructure: OvermindDbContext (WAL via connection interceptor), InitialSchema migration with FTS5 virtual table entry_fts + 3 sync triggers (insert/update/delete, trigger-only sync), FtsSearchService (MATCH + bm25 + snippet, project filter). Tests: 29 Core invariant tests, 9 integration tests (real SQLite temp file: CRUD roundtrip, FTS insert/update/delete sync, version row on update, WAL mode, sqlite_master check for entry_fts + 3 triggers). dotnet build: 0 warnings; dotnet test: 38/38 green. See <obstacles> for EF Guid-tracking gotcha, FTS5 contentless limitations and the EFCore.Design package addition.
2026-09-06 — implementer — Review-gate fix: version numbering moved from in-memory Versions.Max to persisted ContextEntry.CurrentVersion counter (domain invariant, incremented in UpdateContent). Fixes duplicate Version=1 on first update of an entry loaded without Include(Versions) in a fresh context (unique-index violation). Migration re-scaffolded cleanly (ContextEntries.CurrentVersion column); FTS5 DDL re-applied. Added multi-session integration test (3 separate contexts, versions 1..2 persisted) + Core test for counter start value. dotnet build: 0 warnings; dotnet test: 40/40 green (Core 30, Infrastructure 10).
</log>
