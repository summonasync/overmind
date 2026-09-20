---
feature: 012
task: 01
slug: web-ux-polish
status: done
review: light
created: 2026-09-20
---

# 012/01 — Tag colors backend (Tag.Color + TagsApi)

<goal>
Tags gain a nullable Color (#RRGGBB) exposed through two additive endpoints: GET /api/tags (full list with colors) and PUT /api/tags/{id} (set/clear/untouched semantics). No rename, no delete, no MCP changes.
</goal>

<context>
Feature 012-web-ux-polish, backend half. Task 03 (tag manager UI) consumes this. Tags are a proper many-to-many (Tags + EntryTags), auto-created find-or-create in EntriesApi.ResolveTagsAsync — no tag endpoints exist today.
</context>

<scope>
- src/Overmind.Core/Entities/Tag.cs — nullable Color (string?).
- EF migration AddTagColor (nullable, max length 16 — mirror the FrontmatterKey.Color config precedent in OvermindDbContext).
- New static API class src/Overmind.Host/Api/TagsApi.cs (Map<TagsApi> convention): GET /api/tags → TagResponse[] {id, name, color} ordered by name; PUT /api/tags/{id} with UpdateTagRequest {color: string?} — absent/null = untouched, "" = clear, "#RRGGBB" = set; unknown id → 404 via HttpErrors; register the map in Program.cs next to the siblings.
- Contracts in Api/Contracts (TagResponse, UpdateTagRequest) + FluentValidation: color optional, when non-null and non-empty must match ^#[0-9A-Fa-f]{6}$ with max length 16, "" allowed (clear).
- Update path: a small TagsService or direct DbContext usage — follow how the codebase treats similar tiny surfaces; color unchanged (null input) must not bump UpdatedAt semantics that don't exist on Tag today (Tag has no UpdatedAt — just write the column when it changes).
- Tests in tests/Overmind.Api.Tests/: seed entries with tags → GET returns them with color state; PUT set → reflected; PUT "" → cleared; PUT absent → untouched; PUT "blue" / "#GGG" → 400; PUT unknown id → 404.
</scope>

<anti-scope>
- No rename/delete endpoints, no tag merging, no per-project scoping.
- No MCP tool changes, no web changes (tasks 03–04).
- No changes to EntriesApi tag resolution.
</anti-scope>

<constraints>
- .NET 10 / C# 14; no new packages; no comments; English-only; static Map<ClassName> API convention.
- dotnet build + dotnet test green (record counts; baseline 794).
</constraints>

<acceptance-criteria>
- [ ] GET /api/tags returns all tags with id/name/color after seeding via entry creation.
- [ ] PUT with "#3B82F6" sets the color; "" clears; absent leaves untouched.
- [ ] PUT with "blue" or "#GGGGGG" → 400 validation; unknown id → 404.
- [ ] Migration applies cleanly (test host migrates at boot).
- [ ] dotnet test green incl. new tests.
</acceptance-criteria>

<refs>
- Spec: feature 012, spec artifact (validated 2026-09-20).
- Pattern refs: Tag.cs, EntriesApi.ResolveTagsAsync (259–289), FrontmatterKeysApi.cs (tiny surface precedent), FrontmatterKey.Color config (OvermindDbContext.cs:198), 20260919164533_AddFrontmatterKeyRegistry.cs (migration precedent).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — dotnet-coder subagent (parallel with task 02, disjoint file sets).
- 2026-09-20 — Coder done: Tag.Color + SetColor (""→null), AddTagColor migration, TagsApi GET/PUT (untouched/clear/set semantics, 404), TagContracts, UpdateTagRequestValidator (regex + max 16), Program/DI registrations, 7 tests (formats Theory, lifecycle, 404). Suite 794 → 801. To review.
- 2026-09-20 — Review PASS-WITH-NITS (audits/review-012-01.md): 0 blockers, 1 actionable NIT + 5 observations. NIT (regex $ accepts trailing \n → invalid CSS tint downstream) FIXED in-tree: anchor switched to \z, test case "#3B82F6\n" added to the format Theory; suite 801 → 802. Observations → microbatch queue (redundant max-length, unpaged GET waived by spec, uppercase-hex test symmetry). Done.
</log>
