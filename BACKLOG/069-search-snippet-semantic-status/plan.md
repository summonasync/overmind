---
feature: 069
slug: search-snippet-semantic-status
status: validated
created: 2026-09-25
validated: 2026-09-25
---

# Plan 069 — Search: починить пустые сниппеты у fulltext-хитов + строка Semantic search: available/unavailable(reason) в onboard (OM-14)

<approach>
Single-concern fix on a verified root: one task carries the whole wave (they share the failing-test-first discipline and the same suite run). Entries join the feature/artifact C#-window pattern in FtsSearchService; onboard gains the semantic block in the 042 idiom. No sequencing risk — straight shot, one dispatch.
</approach>

<tasks>
- [ ] 01-snippets-and-semantic-line — FtsSearchService.SearchEntriesAsync: SELECT e."Content" instead of snippet(entry_fts,…), WindowSnippet(content, matchQuery), remove ReadSnippetAsync; red-first Infrastructure pin (term in Content → non-empty snippet containing the term); Overmind.Mcp onboard: "Semantic search: available / unavailable (<reason>)" block in the 042 Append style after validation, sourced from the existing vector health probe (025), reason verbatim (404-model case included); canned fixtures + contract tests both branches; CONTEXT.md search section one-liner if it enumerates search capabilities → tasks/01-snippets-and-semantic-line.md. Verify: dotnet build 0 warnings; dotnet test Overmind.sln (baseline 1415)
</tasks>

<verification>
- Fulltext entry hit with a real term returns a non-empty snippet containing the term (was "" pre-fix) — pinned.
- Onboard shows the semantic line both ways: healthy probe → available; broken probe (404 model) → unavailable with the verbatim reason — contract tests.
- Suite green 1415+; build 0 warnings.
</verification>