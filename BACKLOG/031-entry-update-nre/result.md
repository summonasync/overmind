---
feature: 031
slug: entry-update-nre
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 031 — Bug: PUT /api/entries 500 (NRE in EntryMapper) when update carries no tags — breaks every MCP update_entry on tagged entries

<outcome>
P0 bug fixed end-to-end same day: PUT /api/entries/{id} no longer 500s on tagged entries when the payload omits tags (the exact MCP adapter shape). Root cause: the Update fetch loaded EntryTags without ThenInclude(Tag) while the response mapper dereferenced t.Tag — the crash happened AFTER SaveChanges committed, so every "failed" call had actually succeeded (retries duplicated version snapshots). Fix: ThenInclude parity + null-safe mapper projection; regression test pins the adapter shape (red on the old code).
</outcome>

<evidence>
- Live root-cause: NRE stack captured from the running host (EntriesApi.cs:211 → EntryMapper.cs:19); log showed UPDATE + EntryVersions INSERT committed before the 500.
- The "size" correlation in the original report was coincidental — real trigger: tagged entry + tags-absent payload.
- Gates: build 0 warnings; 943/943 (+1 regression test). Review PASS (audits/review-031-01.md, 3 INFO → microbatch 26).
- Deployed (20:16-era pack lineage) and verified live through the MCP adapter: multi-KB body on a tagged entry → 200, currentVersion bumped, all tags preserved.
- Reporter's note (b8a0c450) restored with a RESOLVED section + root cause; version history preserved all payloads overwritten during repro probes.
</evidence>

<deferred>
- Microbatch 26: explicit Tag navigation in EntryTag.Create/AddTag — kills the whole null-Tag bug class (create path currently relies on EF relationship fixup).
- The bare-500-without-detail observability gap (original report's suggestion) remains for any future unhandled server fault.
</deferred>
