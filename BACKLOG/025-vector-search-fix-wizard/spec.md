---
feature: 025
slug: vector-search-fix-wizard
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Spec 025 — Vector search: proxy/model fixes + honest diagnostics + setup wizard

<decisions>
- Proxy fix mirrors the MCP-adapter precedent (commit 7394389): every outbound client to a LOOPBACK endpoint (Qdrant gRPC, Ollama embed, OpenAI-compatible embed) is built with UseProxy=false; non-loopback base URLs keep system-proxy support (the remote-providers ADR). Loopback detection: host is localhost/127.0.0.1/::1 (or resolves from such). This kills the "eternal down" class of bug permanently, regardless of the user's shell env.
- Ollama model becomes a setting: embedding.ollama.model (default stays all-minilm-l6-v2 for compatibility). Dimensions stop being hardcoded: the provider measures the first successful embed and caches the dimension per provider instance; the existing dimension-change collection recreation path stays as the guard. OpenAI model stays configurable-adjacent (existing behavior) — only Ollama gains the model setting in this feature.
- Health tells the truth: /api/health vectorSearch becomes an object { qdrant: { up, endpoint, error? }, embedding: { provider, model?, up, error? } } with vectorSearch.up = both legs up (additive: existing boolean consumers read .up; the web health types extend). The embedding leg probes the ACTIVE provider with a 5s timeout "ping" embed (cheap, cached for the health interval).
- Semantic 503 differentiates: qdrant-down vs embedding-failed vs dimension-mismatch — distinct messages naming the actual culprit and the next step (pull the model / fix the URL / reindex).
- Settings UI: adds Qdrant endpoint, Ollama model, and a Test button (POST /api/settings/test → pings qdrant + active embedding provider with the CURRENT form values, returns per-leg verdicts with errors); the banner shows the failing leg's reason + a link to Settings; save invalidates health so the chip refreshes immediately.
- Reindex nudge: settings save detects provider/model/endpoint changes and the response carries reindexRecommended:true; the settings page shows a "Reindex now" action when set (no auto-reindex — the user stays in control; CONTEXT.md wording corrected by the result artifact).
</decisions>

<open-questions>
- none (live-diagnosed on the user's machine this session)
</open-questions>

<requirements>
Backend:
- Proxy: loopback-scoped UseProxy=false for QdrantClient construction and embedding HttpClients; remote URLs unchanged.
- OllamaEmbeddingProvider: model from embedding.ollama.model (SettingKeys + defaults + settings contract + validation), dynamic dimension discovery; SettingsApi persists the new key.
- Health: structured vectorSearch object per above; SearchApi semantic errors carry honest typed distinctions; Qdrant health failure logs at Warning with the exception message (was Debug — invisible).
- POST /api/settings/test: pings Qdrant (existing HealthAsync path) + active embedding provider (tiny embed call) with provided candidate values; returns per-leg {up, error}; loopback-only.
- Tests: proxy-off for loopback clients (unit — handler config assert), ollama model setting flows, health shape both legs, honest 503s, settings/test contract, reindexRecommended on change.
Web:
- health types + StatusChip/DegradedBanner consume the structured object (reason text + link to settings when a leg is down).
- settings form: Qdrant endpoint + Ollama model fields; Test button with per-leg results; Reindex nudge banner on reindexRecommended; health invalidation on save.
Verification: dotnet build/test green; web lint/tsc/build green; live e2e on the user's machine: with proxy env restored, health shows up after the fix; wrong model named honestly; nomic-embed-text configured end-to-end + reindex works (768 dims).
</requirements>

<acceptance-ideas>
- Host started WITH the user's proxy env: vectorSearch up (Qdrant loopback bypasses proxy), embedding leg green for the configured model.
- Ollama model set to nomic-embed-text: embed works (768 dims), collection recreated with a clear warning, reindex succeeds, semantic search returns hits.
- Wrong model name: health embedding leg red with "model not found" + the 503 names the model — not "vector store down".
- Qdrant stopped: banner says Qdrant + endpoint + error, links to settings; Test button in settings shows the same per-leg verdict.
</acceptance-ideas>
