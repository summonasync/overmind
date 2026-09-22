---
feature: 041
slug: entries-metadata-listing
status: validated
created: 2026-09-22
validated: 2026-09-22
---

# Spec 041 — Entries listing: `list_entries` отдаёт метаданные по умолчанию, тела — по явному флагу

<decisions>
- **D1 — The projection lives on the MCP layer; the HTTP contract is untouched (agent).** The web UI reads `GET /projects/{id}/entries` with full bodies and must not break; the loopback transfer of full bodies costs nothing real — the pain is tokens in the LLM context, which is exactly where the adapter already projects for `write_entry`/`update_entry` (`CompactEntry`, 037 precedent). `list_entries` currently returns the raw HTTP payload (`OvermindTools.cs:37-51`), which is how nine short notes became 56 831 characters in one response.
- **D2 — Metadata by default, content on explicit opt-in (agent).** Default projection per entry: `id`, `type`, `title`, `tags`, `updatedAt`, `contentLength` (computed at the adapter — the API owes nothing new). Full bodies come back only with `include_content: true`; the per-entry deep read goes through the existing `read_entry`.
- **D3 — The tool description documents the shape it returns (agent).** Following the 032/039 self-documentation pattern: the description states metadata-by-default, the `include_content` escape hatch, the `read_entry` path for one body, and the factual listing behaviour (no pagination today; ordering as the API returns — pinned in the task by observation, then stated).
- **D4 — Expose the filters the HTTP door already has (agent).** `ListByProject` accepts `type`, `tag` and `q`; the MCP tool surfaces only `type`. `tag` and `q` are added to the tool signature — free at the API, and they let an agent narrow before reading anything. No new API work.
</decisions>

<open-questions>
- Should `contentLength` be characters or bytes? Recommendation: characters (it approximates token cost, which is the number the consumer reasons about).
- Should the default projection include `createdAt` alongside `updatedAt`? Recommendation: yes if the HTTP payload already carries it (verify in task; do not extend the API for it).
</open-questions>

<requirements>
- R1 — `list_entries` default response: array of metadata projections (D2); no `content` field at all (absent, not empty string).
- R2 — `include_content: true` returns entries with full content (today's shape), same filters.
- R3 — Tool parameters: `projectId`, `type?`, `tag?` (new), `q?` (new), `include_content?` (new, default false).
- R4 — Description per D3, pinned by a description pin-test (032 pattern).
- R5 — Tests: default projection has zero content substrings and correct `contentLength`; `include_content` round-trips full bodies; `tag`/`q` filters reach the HTTP call (assert the query string); existing `read_entry` untouched.
</requirements>

<non-goals>
- Changing `GET /projects/{id}/entries` or any other HTTP contract — web UI keeps its payload.
- Pagination/limits on the API — documented as factual behaviour, not redesigned here (scale beyond dozens of entries is a later question).
- Partial reads of planning artifacts — 040's territory.
- Entries materialization — 051.
</non-goals>

<context>
Field source: FEATURELIST/10-list-entries-token-blowup.md — live capture: `list_entries(type: "note")` on nine short notes (900–4600 chars each) returned 56 831 characters as one line, truncated by the client; the agent parsed titles out of a host-saved file with python. README FEATURELIST ranks this the cheapest of the three granularity fixes and the first one every agent hits. Code facts: `OvermindTools.cs:37-51` (raw passthrough), `CompactEntry` at `:137` (projection precedent), `EntriesApi.cs:29-36` (`type`/`tag`/`q` already accepted). This is the read-side opening move of the cost model 039 D9 established for writes.
</context>