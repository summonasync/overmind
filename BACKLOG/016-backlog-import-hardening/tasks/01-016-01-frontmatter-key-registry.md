---
feature: 016
task: 01
slug: backlog-import-hardening
status: done
review: light
created: 2026-09-19
---

# 016/01 — Frontmatter key registry

<goal>
A persisted registry of frontmatter keys (name → color) exists: pre-seeded with the known ceremony keys plus the Mindlash set, auto-registering unseen keys with a stable palette color, exposed additively over the API for the future UI.
</goal>

<context>
Feature 016 (spec/plan validated 2026-09-19). Dogfood finding A1: the backlog importer silently drops unknown frontmatter keys (depends_on, phase, decision, created_during). User decision: nothing is dropped — every key is supported via a registry; new keys auto-register with a "random pretty color" and get reported to the user. This task is the storage + service foundation; the importer wiring is task 02. The executing subagent reads ONLY this file.
</context>

<scope>
- New entity FrontmatterKey (src/Overmind.Core/Entities/): Id (Guid), Name (string, unique, ordinal-ignore-case lookup is fine), Color (string, hex), CreatedAt. Follow existing entity style (private setters, factory/Restore pattern if used by neighbors).
- EF config + migration in src/Overmind.Infrastructure (OvermindDbContext): unique index on Name. Migration: `dotnet ef migrations add AddFrontmatterKeyRegistry --project src/Overmind.Infrastructure`.
- Curated palette (~12-16 pleasant distinct hex colors, matching the web UI tone — check web/ brand tokens for hints, e.g. web/brand-book.md if present); assignment is DETERMINISTIC: hash the key name → palette index (same name always same color across runs/profiles).
- Seed on migration or first use: ceremony keys (status, review, version, created, feature, task, slug, validated) + Mindlash set (depends_on, phase, decision, created_during) — each with a hand-picked fixed color.
- Service (Infrastructure/Planning or a new small service where neighbors live): GetOrRegisterAsync(name) → (color, isNew). Case-normalize names (trim, lower) so "Depends_On" and "depends_on" are one key.
- Additive API exposure: GET /api/frontmatter-keys (list of {name, color}) — additive-only contract, no changes to existing DTOs. Host↔MCP contract untouched otherwise.
- Tests (tests/Overmind.Infrastructure.Tests + Api.Tests per existing patterns): seed present; auto-register returns isNew=true once then false; same name → same color (determinism); case-insensitive dedupe; API returns the list.
</scope>

<anti-scope>
- No importer/materializer changes (task 02/03), no CLI (task 04)
- No UI rendering of keys (no surface exists today; registry is API-exposed for later)
- No MCP tool for the registry (additive API is enough; MCP exposure can ride 013 or later)
</anti-scope>

<constraints>
- .NET 10 / C# 14, EF Core + SQLite; no new NuGet packages; no comments in code
- dotnet build Overmind.sln && dotnet test Overmind.sln --no-build green (baseline 677 + new). Known flake: Internal CLR error → dotnet build-server shutdown, retry
- Dependency direction: Core ← Infrastructure ← Host; registry lives Core+Infrastructure, exposure in Host
</constraints>

<acceptance-criteria>
- [ ] FrontmatterKey table with unique Name; migration applies cleanly on a copy of the live profile
- [ ] Seed covers ceremony + Mindlash keys with fixed colors
- [ ] GetOrRegisterAsync deterministic color + isNew semantics; case-insensitive
- [ ] GET /api/frontmatter-keys returns seeded registry
- [ ] dotnet build + test green
</acceptance-criteria>

<refs>
- @src/Overmind.Infrastructure/Persistence/OvermindDbContext.cs — entity config patterns
- @src/Overmind.Infrastructure/Planning/ — neighbors for service placement
- tasks/02-importer-fidelity.md — consumes this registry
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-19 — Created from validated plan 016
- 2026-09-19 — Started: frontmatter key registry (coder dispatched)
- 2026-09-19 — Coder done: registry + palette + seed + GET endpoint; 689/689 green; light review dispatched
- 2026-09-19 — Light review PASS-WITH-NITS (0 blocker/important, 4 nit), audits/review-016-01.md; nit-1 (log level) fixed in-tree
</log>
