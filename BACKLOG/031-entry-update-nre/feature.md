---
feature: 031
slug: entry-update-nre
status: done
priority: p2
version: 1
created: 2026-09-20
---

# Feature 031 — Bug: PUT /api/entries 500 (NRE in EntryMapper) when update carries no tags — breaks every MCP update_entry on tagged entries

<goal>
Bug fix: PUT /api/entries/{id} throws NullReferenceException (500) when the request carries no tags — the Update handler loads EntryTags without ThenInclude(Tag), and EntryMapper.ToResponse dereferences t.Tag. Affects every MCP update_entry on a tagged entry (adapter sends title+content only). The DB write SUCCEEDS before the crash — the caller sees 500 while the content IS updated (retries duplicate versions).
</goal>

<context>
Reported by a MindlashStudio dogfood session 2026-09-20 (note b8a0c450) as "PUT always 500"; root-caused live the same evening: NRE stack at EntryMapper.cs:19 via EntriesApi.cs:211; host log shows UPDATE + EntryVersions INSERT committed before the 500. Misleading "size" correlation: their payloads were large, but the actual trigger is tags-present + tags-absent-from-payload. Version snapshots preserved the original report body (update_entry overwrote it during the repro).
</context>

<acceptance>
- PUT /api/entries/{id} with {title, content} only (no tags) on a TAGGED entry → 200, tags preserved in response and DB, version snapshot written once.
- GET-пarity: response mapping never NREs regardless of include depth (null-safe mapper or ThenInclude parity).
- Regression test pinning: update-without-tags on tagged entry (the exact MCP adapter shape).
</acceptance>

<log>
- 2026-09-20 — by user request in-session («ага», 2026-09-20) — delegated close via the agent door: task 031/01 done (ThenInclude parity + null-safe mapper + regression test, review PASS, 943/943), fix deployed and live-verified through the MCP adapter, result artifact validated.
</log>
