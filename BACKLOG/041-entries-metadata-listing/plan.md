---
feature: 041
slug: entries-metadata-listing
status: validated
created: 2026-09-22
validated: 2026-09-22
---

# Plan 041 — Entries listing: `list_entries` отдаёт метаданные по умолчанию, тела — по явному флагу

<approach>
One task, adapter-only: the projection, the two new filter parameters, the description, and the pin-tests all live in `src/Overmind.Mcp/OvermindTools.cs` and `tests/Overmind.Mcp.Tests/`. No Core, no Infrastructure, no Host, no web — the HTTP contract is deliberately untouched (spec D1), so the whole feature rides one file pair plus its test neighbours. The `CompactEntry` helper (`OvermindTools.cs:137`) is the shape precedent; the listing projection is its metadata-only sibling and may share internals where that reads naturally.

Sequencing note: this rides after 039 in the same deployment wave (user decision: bundle at the end of the wave, no intermediate deploys).
</approach>

<tasks>
- [ ] 01-metadata-listing-tool — Mcp: `list_entries` default response becomes the metadata projection (id, type, title, tags, updatedAt, createdAt-if-present, contentLength in characters; `content` absent — not empty); `include_content` (default false) returns today's full shape; `tag` and `q` parameters added and forwarded to the HTTP query string; description per spec D3 (metadata-by-default, read_entry path, include_content hatch, factual no-pagination note — observe the actual ordering and state it); tests: projection pins (zero content substrings, correct contentLength), include_content round-trip, tag/q query-string assertion, description pin-test (032 pattern); verify: `dotnet build` + `dotnet test` green → tasks/01-metadata-listing-tool.md
</tasks>

<risks>
- **Web UI regression via accidental API change.** Mitigation: the task touches only the Mcp project; a grep gate that `EntriesApi.cs` is untouched is part of the review checklist.
- **Projection drift from the real payload.** contentLength/createdAt must come from what the API actually returns, not from assumptions — the task verifies against the live payload shape in tests (stub reflects the true contract).
</risks>

<verification>
- `dotnet build Overmind.sln` + `dotnet test Overmind.sln` green.
- New pins: default projection content-free with correct contentLength; include_content full-shape; tag/q reach the query string; description verbatim pin.
- `git status` shows no Host/web changes.
- Review: light (single-surface adapter change).
</verification>