---
feature: 001
task: 05
slug: embeddings-qdrant
status: todo
review: light
created: 2026-09-06
---

# 001/05 — Embeddings and Qdrant semantic search

<goal>
Pluggable embedding providers (local ONNX default, Ollama, OpenAI-compatible) and a Qdrant vector store provide semantic search over entries. Without Docker/Qdrant the app runs in honest degraded mode: FTS works, semantic search reports "down". Provider and endpoint are configurable and persisted in settings.
</goal>

<context>
Spec R3.3/R4: Qdrant is the ONLY vector backend (hard Docker dependency, user decision). Embedding provider is selected in UI. ONNX model downloads on first run to `~/.overmind/models/` (not bundled). Task 03 left a `NullSemanticSearch` stub reporting down — this task replaces it.
</context>

<scope>
`src/Overmind.Infrastructure/Embeddings/`:
- `OnnxEmbeddingProvider : IEmbeddingProvider` — Microsoft.ML.OnnxRuntime + a MiniLM-class sentence model (choose a concrete MIT/Apache model at execution time, e.g. all-MiniLM-L6-v2 ONNX; record the choice in this file's <log>). Model path: `~/.overmind/models/<model-name>/`; `ModelDownloader` downloads with progress callback if missing. Tokenizer: use the model's tokenizer.json via a small MIT tokenizer lib (verify license via nuget-licensing-guard) or bundle tokenization through the ONNX pipeline if the chosen export includes it.
- `OllamaEmbeddingProvider` — `HttpClient` POST to `{baseUrl}/api/embed` (configurable base URL, default `http://127.0.0.1:11434`).
- `OpenAiCompatibleEmbeddingProvider` — POST `{baseUrl}/v1/embeddings` with configurable base URL + API key.
- `EmbeddingProviderFactory` — resolves active provider from `AppSettings` (new table: key/value; keys: `embedding.provider`, `embedding.ollama.baseUrl`, `embedding.openai.baseUrl`, `embedding.openai.apiKey`, `qdrant.endpoint`).

`src/Overmind.Infrastructure/VectorSearch/`:
- `QdrantSemanticSearch : ISemanticSearch` — Qdrant.Client; collection `overmind_entries` (vector size from active provider's `Dimensions`); payload: entryId, projectId, type, title. `HealthAsync()`, `UpsertEntryAsync(entry)`, `DeleteEntryAsync(entryId)`, `SearchAsync(...)` with projectId filter.
- `EntryIndexingService` — subscribes to entry create/update/delete (explicit calls from API layer, no EF interceptors): embeds content, upserts to Qdrant. Failures (Qdrant down) are logged and swallowed — FTS remains authoritative.
- `ReindexService` — re-embeds all entries (used when provider changes).
- Degraded mode: `ISemanticSearch.Health` drives `/api/health` `vectorSearch` field; when down, `SearchApi` gains `mode=semantic|fulltext` with semantic → 503 + ProblemDetails when down.

`src/Overmind.Host/Api/`:
- `SettingsApi` — `GET/PUT /api/settings` (the keys above; apiKey write-only, never returned).
- `POST /api/search/reindex` — triggers ReindexService.

Tests:
- Unit: factory resolution, settings roundtrip, provider request shapes (HttpMessageHandler mock).
- Integration (no Docker required): with Qdrant unreachable — `/api/health` shows down, FTS search works, semantic search returns 503, entry CRUD still succeeds (indexing failures swallowed).
- Qdrant-up tests: guard with a skip-if-unreachable check (Docker testcontainers NOT required).
</scope>

<anti-scope>
- No other vector backends (no sqlite-vec, no pgvector) — Qdrant only.
- No Docker orchestration (we don't start/stop containers).
- No UI (task 07).
- No chunking strategies beyond whole-entry embedding (chunking is a later optimization — record as P2+ idea, don't build).
</anti-scope>

<constraints>
- Qdrant.Client + Microsoft.ML.OnnxRuntime via central package management; license check (both Apache-2.0/MIT — verify via nuget-licensing-guard).
- HTTP timeouts: embeddings 30s, Qdrant 10s; all async with CancellationToken.
- API keys stored in SQLite settings table only, never logged.
- Model download resume not required in v1; partial download deleted on failure.
</constraints>

<acceptance-criteria>
- [ ] Degraded-mode integration tests pass without Docker.
- [ ] Switching provider in settings changes which provider resolves (unit test).
- [ ] apiKey never appears in GET /api/settings responses (test).
- [ ] Entry create with Qdrant down → 201 from API, error logged, FTS still finds the entry.
- [ ] `IEmbeddingProvider` has `Dimensions`; Qdrant collection created with matching size (unit test on collection-config builder).
</acceptance-criteria>

<refs>
- @../plan.md — task 05
- @../spec.md — R3.3, R4, decisions/storage
- tasks/02-domain-and-persistence.md — IEmbeddingProvider/ISemanticSearch signatures (must be done first)
- tasks/03-core-api.md — NullSemanticSearch stub being replaced (can run in parallel with 04)
</refs>

<obstacles>
<!-- Filled by executors during work -->
</obstacles>

<log>
2026-09-06 — writing-plans — task created from approved spec.
</log>
