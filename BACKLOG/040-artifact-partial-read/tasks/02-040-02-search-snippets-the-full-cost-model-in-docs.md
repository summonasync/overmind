---
feature: 040
task: 02
slug: artifact-partial-read
status: done
review: light
created: 2026-09-22
brief: Search snippet projection (adapter-only, ~5-line window, zero bodies) + CONTEXT/STRUCTURE cost model goes bidirectional
---

# 040/02 — Search snippets + the full cost model in docs

<goal>
The MCP `search` tool stops returning whole bodies: each hit becomes `{id, title, type, projectId, snippet}` with a bounded window of lines around the first query-term match. The docs' write-cost bullet grows into the full read/write cost model.
</goal>

<context>
`search` (OvermindTools.cs:141-160) is a raw passthrough of `/api/search?...&limit=20&mode=...` — the field reported it "returns whole bodies" (FEATURELIST/01/03/05). The web UI consumes the same endpoint and must not change, so the projection is adapter-only, exactly the 041 pattern. After this task the cost model is symmetric: writes (039) and reads (040/041) both carry tiers.

Executor reads ONLY this file.
</context>

<scope>
- Mcp: project each search hit to `{id, title, type, projectId, snippet}` — snippet = window of ~5 lines around the first matching line; case-insensitive; whole-query match first, fallback to the first term that matches (spec open-question resolution); ellipsis markers at truncated edges; bodies absent from the response. Handle hits whose fields differ (entries carry title/content; verify the actual SearchApi response shape first and adapt gracefully — e.g. kind+order label if no title; do not invent fields).
- Parameters unchanged: query, projectId, mode (plus the built-in limit=20).
- Description of `search`: additively state that results are metadata+snippet, bodies are not returned (read a full entry via read_entry / an artifact via planning_read_artifact).
- Docs: CONTEXT.md — extend the write-cost bullet (039) into the full read/write cost model naming all five priced tools; STRUCTURE.md — extend the priced-writes bullet with the read sentence. Surgical edits only.
- Tests (Mcp): projection is body-free; snippet window bounded with ellipsis markers when truncated and contains the matching line; whole-query-then-first-term fallback pinned; description pin.
</scope>

<anti-scope>
- No server changes: SearchApi, Core, Infrastructure, web untouched (grep gate in review).
- No snippet parameters (fixed window), no highlighting, no semantic-mode changes.
</anti-scope>

<constraints>
- No comments; English strings; no new packages; no commits.
- Gates: dotnet build (0W/0E) + dotnet test green (baseline after task 01).
</constraints>

<acceptance-criteria>
- search returns zero full bodies; snippets structurally correct (bounded, marked, match-line included).
- Description pinned; docs updated surgically.
- git status: only src/Overmind.Mcp, tests/Overmind.Mcp.Tests, CONTEXT.md, STRUCTURE.md.
</acceptance-criteria>

<refs>
- Spec 040 D4, R4, R6; plan risks (snippet quality is structural, not aesthetic).
- OvermindTools.cs:141-160 (current passthrough); 041 adapter-projection pattern; 032 pin pattern.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-22 — Light review PASS-WITH-NITS (0 BLOCKER / 0 IMPORTANT / 3 NIT, audits/review-040-02.md): adapter-only verified by grep gate, snippet algorithm discriminating-tested (first-term-in-query-order proven against a plausible wrong implementation), no contamination of 01's files, docs surgical. All three NITs → microbatch #41.
- 2026-09-22 — Accepted — snippets shipped, cost model bidirectional. Feature 040 assembled (2/2 tasks, both PASS-WITH-NITS); the artifacts-outside-search gap it surfaced is now feature 053 (user verdict).
</log>
