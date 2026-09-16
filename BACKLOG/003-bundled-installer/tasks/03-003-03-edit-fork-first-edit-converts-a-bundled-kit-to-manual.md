---
feature: 003
task: 03
slug: bundled-installer
status: done
review: light
created: 2026-09-16
---

# 003/03 — Edit-fork: first edit converts a bundled kit to manual

<goal>
The first content edit of an entry owned exclusively by a bundled kit converts that kit in place to `Manual` (origin fields cleared). Shared entries mutate directly with no fork. Manual/imported kits behave exactly as today.
</goal>

<context>
Spec D4/D7: bundled kits are useful precisely until the user personalizes them — then they become ordinary user kits so upgrade sync never clobbers them. The hook lives on the entry update path. The 002a web already renders whatever `sourceKind` the API returns, so the badge flip is automatic. Executor reads ONLY this file.
</context>

<scope>
- Entry update path (`src/Overmind.Host/Api/EntriesApi.cs` `Update` handler — check whether an EntryService exists; if the house keeps entries handler-direct, put the hook in a small Infrastructure-side helper the handler calls, mirroring how planning extracts shared logic)
- Behavior: on successful content update, if the entry is linked to exactly one `SkillKitEntry` and that kit's `SourceKind == Bundled` → clear the kit's origin (reuse the Core conversion — note `ClearSource()` refuses bundled by design from task 01, so add the dedicated conversion method, e.g. `SkillKit.ForkToManual()`, setting kind/path/nulls in one step) and log a structured line
- Entries linked to 2+ kits or to a non-bundled kit: no conversion
- Title-only/tag-only/skillfile-only updates do NOT fork — content change only
- Tests (Api.Tests): edit-fork fires on content change of an exclusive bundled entry; shared entry edit mutates without fork; manual/imported kit edits unchanged; title-only update does not fork; after fork the kit response shows `manual` origin
</scope>

<anti-scope>
- No UI changes (badge flip is data-driven; button hiding is task 05)
- No copy-on-edit for shared entries (spec D7 explicitly excludes it in 003a)
- No changes to the boot lifecycle
</anti-scope>

<constraints>
- No new NuGet packages; no comments; English-only messages
- `dotnet build Overmind.sln` + `dotnet test Overmind.sln` green
- No auto-commits
</constraints>

<acceptance-criteria>
- [x] Content edit of an exclusive bundled entry forks the kit to manual (pinned)
- [x] Shared-entry edit and non-bundled kits unaffected (pinned)
- [x] Non-content updates do not fork (pinned)
- [x] Full suite green
</acceptance-criteria>

<refs>
- @../spec.md — D4, D7, R7 (reviewer reads, not executor)
- @../plan.md — task 03 row
- tasks/01-bundled-origin.md — ClearSource refusal is intentional; this task adds the fork conversion
</refs>

<obstacles>
</obstacles>

<log>
2026-09-16 — Implemented.

Core: `SkillKit.ForkToManual()` sets SourceKind=Manual, SourcePath=null, ImportedAtUtc=null in one
step; deliberately unconditional (no bundled guard) because the call site gates on
SourceKind == Bundled and the method is the dedicated bundled->manual escape hatch next to the
refusing `ClearSource()`.

Hook placement: house keeps entries handler-direct, so the logic lives in
`BundledKitForkService` (Infrastructure/Kits, scoped, mirrors EntryIndexingService injection into
the handler). The service stages the conversion on the tracked kit and does NOT call
SaveChangesAsync itself — the handler's existing save persists entry update + kit fork atomically
in one SQLite transaction (divergence between "entry saved" and "kit forked" is impossible, which
matters because upgrade sync would clobber un-forked kits).

Content-change detection: ordinal `string.Equals` of request.Content vs entry.Content captured
before `UpdateContent` (which unconditionally bumps the version — existing behavior, untouched).
Title/tag/skillfile-only PUTs resend identical content, so they never fork. The extra query only
runs when content actually changed; single query loads all owning kits tracked, fork applies only
when exactly one owner and it is Bundled.

Structured log on conversion at Information level with KitId/KitName/EntryId.

Tests: 4 new Api.Tests in BundledKitForkTests (exclusive bundled fork + response shows manual
origin + second edit stays manual; shared entry in two bundled kits stays bundled; manual kit via
API and imported kit via seeded MarkImported stay untouched; title-only PUT stays bundled).
Seeding reuses the KitImportApiTests pattern (scope + DbContext + MarkBundled/MarkImported).

Verification: `dotnet build Overmind.sln` — 0 warnings, 0 errors.
`dotnet test Overmind.sln` — Core 100, Infrastructure 203, Api 164, Mcp 59 = 526 passed, 0 failed
(Api.Tests includes the 4 new ones; filtered run 4/4 green).
</log>
