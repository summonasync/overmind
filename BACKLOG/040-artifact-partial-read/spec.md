---
feature: 040
slug: artifact-partial-read
status: validated
created: 2026-09-22
validated: 2026-09-22
---

# Spec 040 — Artifact partial read: read-artifact-by-id + параметр sections + сниппеты в search

<decisions>
- **D1 — Read-one-artifact lands on the agent door as a new GET (agent).** The human door already has `GET /planning/artifacts/{id}` (PlanningArtifactsApi.cs:18); the agent door has mutations only — no GET. Add `GET /api/agent/planning/artifacts/{id}` and the MCP tool `planning_read_artifact(artifactId, sections?)`. This is what `planning_current_task` hands out: an artifact id, not a feature.
- **D2 — `sections` filters at the section level via the 039 parser (agent).** `sections` is a comma/space-separated list (e.g. `log,obstacles`); the response carries the named sections' content plus the artifact metadata, and a `missingSections` array naming requested-but-absent sections. Read operations do not 400 on a missing section — the soft diagnostic lets an agent self-correct without a failed call (asymmetric to patch's strictness, which guards writes). `PlanningSections.Find/List` from 039 does the work; no new parsing.
- **D3 — Read-side cost tiers, mirroring 039 D9 (agent).** Descriptions state the price: `planning_read_artifact` with `sections` — "cheapest read: only the named sections"; without — "one artifact, full body"; `planning_read_feature` — "most expensive read: the whole feature with all artifacts; prefer planning_read_artifact for one document". The session bootstrap gains a read routing line next to the write one ("one section → planning_read_artifact with sections; one artifact → planning_read_artifact; whole feature → planning_read_feature").
- **D4 — Search snippets are an adapter projection, not a server change (agent).** The web UI consumes `/api/search` and must not change; the loopback transfer costs nothing real. The MCP `search` tool (raw passthrough today, OvermindTools.cs:141-160) projects each hit to `{id, title, type, projectId, snippet}` — the snippet is a small window of lines around the first query-term match in the body (case-insensitive, ellipsis-marked edges), never the whole body. Fixed window (~5 lines) — no parameter yet; simplicity over configurability until the field asks.
</decisions>

<open-questions>
- Should `planning_read_feature` grow a `sections`/`artifactKind` filter too? Recommendation: no — YAGNI; read_artifact covers the targeted case, and feature-level filtering invites a combinatorial contract.
- Snippet term matching: plain case-insensitive contains of the raw query vs per-term matching? Recommendation: per-whole-query first, fall back to the first term that matches; document whichever lands in the description.
</open-questions>

<requirements>
- R1 — Agent GET `/api/agent/planning/artifacts/{id}`: artifact metadata (id, kind, status, version, order, updatedAt, brief) + body; `?sections=` returns only those sections' content + `missingSections`.
- R2 — MCP `planning_read_artifact(artifactId, sections?)`: ReadOnly, Idempotent; echo of the same shape; 404 mapping consistent with existing agent-door errors.
- R3 — Cost-tier descriptions per D3 on both read tools + the read routing line in `SessionBootstrapPrompt` (both branches), all pinned verbatim (032 pattern).
- R4 — MCP `search`: metadata + snippet projection per D4; zero full bodies in the response; the query still accepts `query`, `projectId`, `mode` as today.
- R5 — Tests: read_artifact full-body default; sections filter returns exactly the named sections; missingSections lists the absent ones; search projection carries snippets and no bodies; description/bootstrap pins; existing `planning_read_feature` untouched.
- R6 — Docs: extend the CONTEXT.md write-cost bullet to the full cost model (reads join the tiers); one line in STRUCTURE.md's priced-writes bullet.
</requirements>

<non-goals>
- Changing `planning_read_feature`'s response shape or adding parameters to it.
- Server-side `/api/search` changes or web UI work.
- Pagination for reads; version diff (045); briefing tool (052); entries listing (041, shipped).
</non-goals>

<context>
Field sources: FEATURELIST/05 (grep-equivalent absent; read_feature ~150k chars on a live project), 01 (one spec section ≈7K tokens vs sed ≈500), 03, 08 ("sed -n '55,80p' for the obstacles section; here it is all or nothing"). Code facts: human-door GET exists (PlanningArtifactsApi.cs:18), agent door has no GET; `planning_read_feature` and `planning_current_task` are the only planning reads (OvermindPlanningTools.cs:68/:77); `search` is a raw passthrough (OvermindTools.cs:141-160); `PlanningSections.Find/List` (039) is ready for section extraction. This is the read-side completion of the cost model 039 D9 opened and 041 continued.
</context>