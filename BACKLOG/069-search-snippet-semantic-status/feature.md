---
feature: 069
slug: search-snippet-semantic-status
status: done
priority: p1
version: 1
created: 2026-09-24
---

# Feature 069 — Search: починить пустые сниппеты у fulltext-хитов + строка Semantic search: available/unavailable(reason) в onboard (OM-14)

<goal>
Two search-side field bugs: (1) fulltext hits return "snippet": "" even when the term exists in bodies — the snippet projection shipped in 040/02 produces empty windows for real hits (the field reproduced it: 4 hits on logNote, all empty snippets); (2) semantic mode dies with "Embedding provider 'ollama' failed for model 'all-minilm-l6-v2': 404" while a stray ~/.overmind/models/all-MiniLM-L6-v2 folder suggests misconfigured dual backends — and the agent learns this only by burning a call. Onboard should state "Semantic search: unavailable (<reason>)" like it states humanGate.
</goal>

<requirements>
- Fix the empty-snippet bug: the adapter window must anchor on the server snippet's match region (investigate why the current anchor finds nothing — likely the <mark>-stripped text vs term matching after stripping; pin a failing test first).
- Onboard gains a semantic line: available / unavailable (reason) — same resolved-block style as validation.humanGate; source = the health probe the UI already uses.
- The stale all-minilm model reference (project configured for a model the provider 404s) surfaces in that reason string verbatim.
</requirements>

<non-goals>
- Artifact indexing (053 — separate); search UI changes; reindexing pipelines.
</non-goals>

<refs>
- FEATURELIST/overmind-features-2026-09-24.md OM-14 (P1); OM-6 (round-2 original).
- src/Overmind.Mcp/SearchSnippet.cs (the window algorithm); 040/02 review (structural pins); 042 onboard-block pattern; 025 vector-search history (proxy/model lessons).
</refs>

<log>
- 2026-09-25 [auto] — artifact created → inProgress
- 2026-09-25 [auto] — all tasks done + spec + plan → review
- 2026-09-25 [agent] — Done by user request in-session («закрывай»): single task done, spec 74b6622e + plan 0e616f35 validated, review APPROVED-WITH-WARNINGS 0 blockers (audits/review-069-01.md), live red-first proof. Field verification of snippets/semantic-line by MCP users after the deploy, per user's plan.
</log>
