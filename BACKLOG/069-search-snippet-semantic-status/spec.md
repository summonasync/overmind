---
feature: 069
slug: search-snippet-semantic-status
status: validated
created: 2026-09-25
validated: 2026-09-25
---

# Spec 069 — Search: починить пустые сниппеты у fulltext-хитов + строка Semantic search: available/unavailable(reason) в onboard (OM-14)

<goal>
Fulltext entry hits stop returning empty snippets, and onboard states the semantic-search status ("available" / "unavailable (reason)") up front, so agents stop burning calls to discover a dead vector stack.
</goal>

<context>
OM-14 field report + 053 finding. Root cause verified in code 2026-09-25: SearchEntriesAsync (FtsSearchService.cs:112) builds entry snippets via SQLite snippet(entry_fts, …) — dead on contentless fts5 (053 finding), always returns ""; feature/artifact hits already use the correct pattern: SELECT the body column + WindowSnippet() in C# (:162, :199). ContextEntry.Content exists as the body column; WindowSnippet(body, matchQuery, window=160) (:206) is the shared helper. The adapter-side SearchSnippet.Window strips <mark> and re-anchors — harmless with mark-free C# windows, unchanged. Semantic status: the UI already probes vector health (025 honest diagnostics); onboard already renders resolved blocks in the 042 style (AppendMaterialization/AppendValidation in OvermindOnboardingTools). No open user decisions — the feature body + code root determine everything.
</context>

<decisions>
- Entries follow the feature/artifact pattern: SELECT e."Content" instead of snippet(entry_fts, …), build the snippet with WindowSnippet(content, matchQuery) — one snippet mechanism across all three FTS sources, all C#-side, immune to the contentless-fs5 limitation. bm25 ranking untouched.
- ReadSnippetAsync dies with the SQL snippet() call (single call site).
- Failing-test-first (root-cause rule): a test pinning that a fulltext entry hit with the term present in Content returns a NON-empty snippet containing the term, written red before the fix.
- Onboard gains "Semantic search: available" / "Semantic search: unavailable (<reason>)" — same resolved-block rendering style as the validation.humanGate block (042 AppendValidation pattern), sourced from the existing vector health probe the UI uses (025); a misconfigured model surfaces verbatim in <reason> (e.g. the all-minilm-l6-v2 404 case). Block placement: after the validation block in the onboard text.
- No adapter changes: SearchSnippet.Window works as-is on the new mark-free windows.
</decisions>

<scope>
- Infrastructure/Search/FtsSearchService.cs: SearchEntriesAsync body+snippet rework, WindowSnippet reuse, ReadSnippetAsync removal.
- Infrastructure tests: red-first snippet pin (term present → non-empty window), regression pins for ranking shape; FtsSearchServiceTests neighborhood.
- Overmind.Mcp: onboard semantic line (Append-style), Onboard tool response block; extension of the existing canned onboard fixtures + contract tests (042 test idiom).
- Docs: no separate doc task — the onboard block IS the user-facing doc; CONTEXT.md search section gains one line if it enumerates search capabilities.
</scope>

<anti-scope>
- Artifact/feature indexing changes (053 owns the index layer); search UI; reindexing pipelines; embedding provider config logic; Qdrant collection changes.
</anti-scope>

<constraints>
- No new NuGet packages; no comments; English strings; additive wire contract (snippet field stays, just stops being empty).
- dotnet build 0 warnings; dotnet test green (baseline 1415).
</constraints>

<acceptance-criteria>
- [ ] Red-first pin: fulltext entry hit whose Content contains the query term returns a non-empty snippet containing that term (written failing, then green).
- [ ] snippet(entry_fts, …) call and ReadSnippetAsync are gone; entries/feature/artifact snippets all come from the C# window mechanism.
- [ ] bm25 ordering and hit shape unchanged (existing tests stay green unmodified except the new pins).
- [ ] Onboard text renders "Semantic search: available" when the probe is healthy and "Semantic search: unavailable (<reason>)" with the probe's verbatim reason otherwise (contract tests both branches, 042 idiom).
- [ ] dotnet build 0 warnings; dotnet test Overmind.sln green.
</acceptance-criteria>

<refs>
Feature body 069 (OM-14); 053 finding (snippet() dead on contentless fts5; C# window shipped for artifacts); 040/02 (snippet projection, structural pins); 042 (onboard block pattern + test idiom); 025 (vector probe, model/proxy lessons); src/Overmind.Infrastructure/Search/FtsSearchService.cs:112,206; src/Overmind.Mcp/SearchSnippet.cs.
</refs>