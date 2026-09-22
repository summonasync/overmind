---
feature: 040
slug: artifact-partial-read
status: validated
created: 2026-09-22
validated: 2026-09-22
---

# Plan 040 — Artifact partial read: read-artifact-by-id + параметр sections + сниппеты в search

<approach>
Two tasks, adapter-and-endpoint sized: first the read tool (agent GET + MCP tool + sections + the read-side cost texts and pins), then the search snippet projection. Both reuse machinery that already exists — `PlanningSections` (039) for section extraction, the 041 adapter-projection pattern for snippets, the 032 pin-test pattern for descriptions. No Core or Infrastructure changes are expected at all: the human-door GET already loads the artifact; the agent door needs a route and a response shape, the rest is adapter work.

Design consequences held for the reviewer:
- **Reads are soft where writes are strict.** A missing requested section is reported in `missingSections`, never a 400 (spec D2) — the reviewer should reject any 400-on-missing-section drift.
- **The cost model becomes bidirectional in this feature.** Bootstrap carries a read routing line; both read tools and read_feature state their tier; the CONTEXT bullet grows from write-cost to the full cost model. Descriptions are contract here, not documentation garnish.
- **Snippets are adapter-only.** The server keeps returning full bodies to the web UI; nothing in Host/SearchApi/Core may change for this (grep gate in task 02's review checklist).
</approach>

<tasks>
- [ ] 01-read-artifact-tool — Host: agent GET `/api/agent/planning/artifacts/{id:guid}` returning metadata (id, kind, status, version, order, updatedAt, brief) + body, with `?sections=a,b` narrowing the body to those sections and adding `missingSections` (PlanningSections.Find/List; soft semantics per spec D2); 404 via existing error mapping. Mcp: `planning_read_artifact(artifactId, sections?)` ReadOnly+Idempotent mirroring the GET. Cost-tier descriptions on both read tools (D3) + read routing line in `SessionBootstrapPrompt` both branches; pins: full-body default, sections-exact, missingSections, description/bootstrap verbatim pins (032 pattern); verify: build + full tests green → tasks/01-read-artifact-tool.md
- [ ] 02-search-snippets — Mcp: `search` projects each hit to `{id, title, type, projectId, snippet}` — window of ~5 lines around the first query-term match, case-insensitive, ellipsis edges, whole-query match first with first-term fallback (spec open-question resolution); zero full bodies; parameters unchanged (query, projectId, mode). Docs: CONTEXT.md write-cost bullet becomes the full read/write cost model; STRUCTURE.md priced-writes bullet gains the read sentence; pins: snippet projection content-free, snippet window behaviour, description pin; verify: build + full tests green, git status shows no Host/SearchApi/web changes → tasks/02-search-snippets.md
</tasks>

<risks>
- **Snippet quality is judgement, not contract.** A bad window (match in a header line, tiny bodies) still satisfies "no full bodies". Mitigation: the pin-tests assert structural properties (bounded lines, ellipsis markers present when truncated, match line included), not aesthetics; the field will tell us if the window needs tuning.
- **Agent GET shape divergence from the human-door GET.** Two doors returning different shapes for the same entity drift apart silently. Mitigation: task 01 reuses the same loader/mapper as the human door where practical and pins the shape; the reviewer checks for copy-paste drift.
- **Search snippet across both entries and planning hits** (if /api/search returns both kinds): the projection must handle absent fields gracefully (title-less artifacts fall back to kind+order label). Verify against the actual SearchApi response shape in the task; do not invent.
</risks>

<verification>
- `dotnet build Overmind.sln` + `dotnet test Overmind.sln` green after each task (baseline 1114).
- Pins: read_artifact default/sections/missingSections; cost-tier descriptions verbatim; bootstrap read-routing line both branches; search projection body-free with bounded snippet.
- `git status` after task 02: only Mcp + tests + the two doc lines; Host untouched by 02, SearchApi/web untouched by both.
- Reviews: light on both tasks.
</verification>

<log>
- 2026-09-22 — Typo fix: closing tag </risisis> → </risks> before validation.
</log>
