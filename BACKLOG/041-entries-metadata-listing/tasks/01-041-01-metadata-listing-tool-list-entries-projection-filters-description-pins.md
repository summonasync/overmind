---
feature: 041
task: 01
slug: entries-metadata-listing
status: done
review: light
created: 2026-09-22
brief: list_entries metadata projection by default (content absent), include_content hatch, tag/q filters exposed, D3 description, adapter-only with pin-tests
---

# 041/01 — Metadata listing tool: list_entries projection, filters, description, pins

<goal>
`list_entries` returns metadata by default (id, type, title, tags, updatedAt, createdAt-if-present, contentLength) with `content` absent — not empty; full bodies only via `include_content: true` or per-entry `read_entry`. The `tag` and `q` filters the HTTP door already accepts become tool parameters. The description documents the shape it returns.
</goal>

<context>
Adapter-only task. `src/Overmind.Mcp/OvermindTools.cs:37-51` passes the raw HTTP payload through — that is how nine short notes became 56 831 characters. The projection precedent is `CompactEntry` at `:137` (037). The HTTP endpoint (`EntriesApi.cs` ListByProject) already accepts `type`, `tag`, `q` — the tool surfaces only `type` today. The web UI reads the same endpoint with full bodies and must not notice this change.

Executor reads ONLY this file.
</context>

<scope>
- `list_entries` default: map each entry to the metadata projection; `contentLength` in characters, computed at the adapter from the content the API returned; `content` field absent from the projection.
- `include_content` parameter (default false): true returns today's full shape (entries with content), same filters.
- `tag` and `q` parameters: forwarded into the HTTP query string alongside `type` (escape properly, skip when null/whitespace).
- Description per spec D3: metadata-by-default, `read_entry` for one body, `include_content` hatch, factual note that there is no pagination and the ordering is as the API returns it (observe the actual ordering from the endpoint code and state it factually — do not invent).
- Tests (Overmind.Mcp.Tests, follow the existing stub/contract patterns): default projection is content-free and contentLength is correct; `include_content` round-trips full bodies; `tag`/`q` reach the HTTP query string (assert the built path); description verbatim pin-test (032 pattern — live ListToolsAsync); `read_entry` untouched.
</scope>

<anti-scope>
- No changes to EntriesApi.cs, Contracts, Core, Infrastructure, or web/ — a grep that these are untouched is part of your own final check.
- No pagination design, no API extensions.
- No planning-artifact reads (040) and no entries materialization (051).
</anti-scope>

<constraints>
- English-only strings; no comments (non-obvious only, default none); no new packages; no git commits.
- Open-questions from the spec resolved as recommended unless the code contradicts: contentLength in characters; include createdAt only if the payload already carries it.
</constraints>

<acceptance-criteria>
- `dotnet build Overmind.sln` + `dotnet test Overmind.sln` green.
- Default `list_entries` response contains zero content substrings; contentLength matches; `include_content` returns full bodies; tag/q asserted in the query string; description pinned verbatim.
- `git status` shows changes only under src/Overmind.Mcp and tests/Overmind.Mcp.Tests.
</acceptance-criteria>

<refs>
- Spec 041 D1–D4, R1–R5; plan 041 risks/verification.
- OvermindTools.cs:37-51 (current passthrough), :137 (CompactEntry precedent).
- 032 pin-test pattern; 037 slimming contract; 039 D9 cost-model context (this is the read-side opening move).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-22 — Execution starts: coder sub-agent takes the adapter-only projection contract (plan gate passed — "ехала").
- 2026-09-22 — Light review PASS-WITH-NITS (0 BLOCKER / 0 IMPORTANT / 3 NIT), gates re-verified: build 0W/0E, tests 1114/1114. Description claims verified against EntriesApi.cs (sorting, no pagination, q cap 100 — all true); content key proven absent; include_content non-leak pinned exactly. Report: audits/review-041-01.md.
- 2026-09-22 — Accepted: NIT-1 (missing-createdAt branch coverage) → microbatch #39; NIT-2/NIT-3 need no fix (wire-contract name, cheap deep-copy). Feature 041 assembled — single-task quick win delivered.
</log>
