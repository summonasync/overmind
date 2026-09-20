---
feature: 031
task: 01
slug: entry-update-nre
status: done
review: light
created: 2026-09-20
brief: Fix: ThenInclude(Tag) parity in Update fetch + null-safe EntryMapper.ToResponse + regression test (update without tags on tagged entry, exact MCP adapter shape).
---

# 031/01 — Fix the entries-update NRE + pin it

<goal>
PUT /api/entries/{id} with {title, content} (no tags) on a tagged entry returns 200 with tags preserved; the response mapping can never NRE; regression test pins the exact MCP adapter shape.
</goal>

<context>
Root cause proven live (host log stack): EntriesApi.Update fetch (line ~158-161) Includes EntryTags and SkillFiles but NOT the Tag navigation; EntryMapper.ToResponse (line 19 lambda over EntryTag) dereferences t.Tag → NRE when tags came from the DB unmodified (request.Tags == null skips ResolveTagsAsync, which is the only path that attaches Tag entities). DB write commits before the mapping crash — caller sees 500 on a succeeded update.
</context>

<scope>
- EntriesApi.Update: add ThenInclude(et => et.Tag) to the fetch (GET-parity; check GET/other fetches for the same gap).
- EntryMapper.ToResponse: null-safe tag projection (t.Tag?.Name ?? fallback) as defense-in-depth — check what the response contract needs when Tag is somehow null (the EntryTag always has TagId; a sane fallback keeps the API honest instead of 500).
- Regression test: create tagged entry → PUT {title, content} WITHOUT tags → 200, response tags intact, DB tags intact, one version snapshot. Follow existing entries API test patterns.
- dotnet build + dotnet test green. Baseline 942/0.
</scope>

<anti-scope>
- No adapter changes (adapter code is correct); no semantic/FTS indexing changes; no version-snapshot semantics changes.
</anti-scope>

<constraints>
- .NET 10 / C# 14; no comments; English-only; no new packages.
</constraints>

<acceptance-criteria>
- [ ] Update-without-tags on a tagged entry → 200 + tags preserved (test-pinned).
- [ ] ToResponse null-safe (no NRE path from any include depth).
- [ ] build 0 warnings; 943+/0 tests green.
</acceptance-criteria>

<refs>
- Feature 031 body (root cause + live stack); note b8a0c450 (original report + investigation).
- src/Overmind.Host/Api/EntriesApi.cs:158-211 (Update), src/Overmind.Host/Api/Mapping/EntryMapper.cs:10,19; GET handler for include-parity reference.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Carved after live root-cause (NRE stack captured from the running host).
- 2026-09-20 — Starting execution immediately (P0: breaks every MCP update_entry on tagged entries; dogfood-reported, live-root-caused).
- 2026-09-20 — Done: ThenInclude(Tag) on the Update fetch (GET-parity; the other three fetch sites already carried it — grep-verified), null-safe EntryMapper.ToResponse (skip-null-tag, no lying fallback), regression test Put_WithoutTags_OnTaggedEntry_KeepsTagsAndReturns200 pinning the exact MCP adapter shape (red on old code — 500 after committed SaveChanges). Review PASS (audits/review-031-01.md, 3 INFO → microbatch: explicit Tag navigation in EntryTag.Create kills the bug class). 943/943, build 0 warnings. Deployed (0.1.9 pack 19:54) and verified live through the MCP adapter: multi-KB body on a tagged entry → 200, currentVersion 3, all 6 tags preserved. Reporter's note b8a0c450 restored with the RESOLVED section + root cause; Fact-3 note marked resolved-by-023/08.
</log>
