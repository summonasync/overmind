---
feature: 001
task: 05
slug: overmind-mvp
status: done
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
- [x] Degraded-mode integration tests pass without Docker.
- [x] Switching provider in settings changes which provider resolves (unit test).
- [x] apiKey never appears in GET /api/settings responses (test).
- [x] Entry create with Qdrant down → 201 from API, error logged, FTS still finds the entry.
- [x] `IEmbeddingProvider` has `Dimensions`; Qdrant collection created with matching size (unit test on collection-config builder).
</acceptance-criteria>

<refs>
- @../plan.md — task 05
- @../spec.md — R3.3, R4, decisions/storage
- tasks/02-domain-and-persistence.md — IEmbeddingProvider/ISemanticSearch signatures (must be done first)
- tasks/03-core-api.md — NullSemanticSearch stub being replaced (can run in parallel with 04)
</refs>

<obstacles>
- Docker/Qdrant unavailable during execution: Qdrant-up coverage is a guarded live test (`QdrantLiveTests`) that probes `OVERMIND_QDRANT_ENDPOINT` (default `http://127.0.0.1:6334`) and self-skips when unreachable; the degraded-mode integration tests are the primary gate and all pass.
- Real ONNX model cannot be downloaded during tests. `OnnxEmbeddingProvider` is split from `OnnxEmbeddingPipeline` (`IOnnxEmbeddingPipeline` seam): the provider is unit-tested with a stub pipeline; pipeline construction (session + tokenizer) is only exercised against a real model at runtime. `ApiFactory` seeds a fake `model.onnx`/`vocab.txt` under a temp `OVERMIND_MODELS_DIR` so no test can ever trigger a real 90 MB download.
- `dotnet ef` requires the design package on the executing project: migration was added with `--project src/Overmind.Infrastructure` only (no `--startup-project`); without that flag ef errors about missing `Microsoft.EntityFrameworkCore.Design` in Overmind.Host.
- Qdrant.Client 1.19: `SearchAsync` is obsolete (warning CS0618) — used `QueryAsync` + `Conditions.MatchKeyword` instead; `QdrantClient(Uri, ...)` timeout param is named `grpcTimeout`.
- Windows file locking: `File.Move` over an open FileStream fails — the download stream is disposed before the move; also parallel xunit factory instances race on the shared fake model files, fixed with a static one-time seed in `ApiFactory`.
- `ScoredPoint.Payload` is a read-only MapField (no initializer assignment) — tests build it via `Payload.Add`.
- Pre-existing task-04 nullability warnings in `KitApiTests.cs` (`problem.Detail` after `ShouldNotBeNull()`) broke the 0-warning gate — fixed with `problem!`. Correction: the nullable is on `ProblemJson.Detail` (`string?`), so the real fix is `problem!.Detail!`; the "0 warnings" evidence in the first pass came from a stale incremental build. Verified with `dotnet build --no-incremental`.
- P2+ idea recorded per anti-scope: chunking strategies beyond whole-entry embedding (title + content) — not built.
</obstacles>

<log>
2026-09-06 — writing-plans — task created from approved spec.
2026-09-06 — dotnet-coder — implemented. Model choice: `sentence-transformers/all-MiniLM-L6-v2` (Apache-2.0), ONNX export downloaded on first run from `https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx` + `vocab.txt` (same repo) into `~/.overmind/models/all-MiniLM-L6-v2/` (override via `OVERMIND_MODELS_DIR`); 384 dims, mean pooling + L2 norm, 512-token cap. Tokenizer: `Microsoft.ML.Tokenizers` 2.0.0 pinned in `Directory.Packages.props` (MIT — verified per nuget-licensing-guard; `BertTokenizer` on vocab.txt, uncased options). Providers: ONNX (default, lazy pipeline), Ollama (`/api/embed`, default model `all-minilm-l6-v2`, 384 dims), OpenAI-compatible (`/v1/embeddings`, default model `text-embedding-3-small`, 1536 dims); embedding HTTP timeout 30 s, Qdrant gRPC timeout 10 s. Settings: new `AppSettings` key/value table + migration `20260906222446_AppSettings`; keys exactly as specced; `embedding.openai.apiKey` write-only (PUT accepts, GET returns only `openAiApiKeyConfigured` boolean). `QdrantSemanticSearch` (collection `overmind_entries`, Cosine, payload entryId/projectId/type/title/content) replaces `NullSemanticSearch`; `EntryIndexingService` swallows+logs Qdrant failures so FTS stays authoritative; `ReindexService` + `POST /api/search/reindex` returns `{total, indexed, failed}`. SearchApi gained `mode=fulltext|semantic` (semantic → 503 ProblemDetails when down). Verification: `dotnet build` 0 warnings/0 errors; `dotnet test` Core 30/30, Infrastructure 45/45, Api 44/44 — all green incl. degraded-mode integration tests and provider unit tests; Qdrant-up live test self-skips (no Docker).
2026-09-06 — dotnet-coder — review fixes (light review, 2 IMPORTANT + 1 NIT). (1) IMPORTANT: `OnnxEmbeddingProvider` dropped the caller's CancellationToken during model download/pipeline creation (`CancellationToken.None` inside a `Lazy<T>` factory) — replaced Lazy with a SemaphoreSlim-gated initialization that plumbs ct through `InitializeAsync` → `ModelDownloader.EnsureAsync` / `OnnxEmbeddingPipeline.CreateAsync`. (2) IMPORTANT: provider switch with different `Dimensions` (e.g. onnx 384 → openai 1536) never recreated the Qdrant collection, so every upsert/query failed with a dimension mismatch, got swallowed, and semantic search silently 503'd forever. Decision: `EnsureCollectionAsync` now reads `GetCollectionInfoAsync` and compares the collection's vector size with the active provider's `Dimensions` via the new testable seam `QdrantCollection.HasMatchingDimensions(VectorsConfig, int)`; on mismatch it logs a warning, drops and recreates the collection — intentional behavior, safe because `POST /api/search/reindex` repopulates (the warning message names it as the recovery path). Unit tests cover match/mismatch/named-vector-config/non-positive without live Qdrant. (3) NIT: degraded 503 test now also asserts `application/problem+json`, non-empty title/detail, and status 503 in the body. Also fixed the KitApiTests nullability warnings for real (`problem!.Detail!`). Verification: `dotnet build --no-incremental` 0 warnings/0 errors; `dotnet test` Core 30/30, Infrastructure 48/48, Api 44/44 — all green.
</log>
