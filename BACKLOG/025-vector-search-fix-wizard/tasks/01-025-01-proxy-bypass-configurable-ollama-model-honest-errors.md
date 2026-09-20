---
feature: 025
task: 01
slug: vector-search-fix-wizard
status: done
review: light
created: 2026-09-20
---

# 025/01 — Proxy bypass + configurable Ollama model + honest errors

<goal>
Kill the two live bugs and stop the 503 lying: loopback clients ignore the system proxy (the eternal "down"), embedding.ollama.model becomes a setting with dynamic dimensions, and semantic failures name their actual culprit.
</goal>

<context>
Feature 025, task 1 of 2. Live-diagnosed on the user's machine: proxy env (http_proxy=127.0.0.1:2081) kills the Qdrant gRPC client (MCP-adapter precedent: commit 7394389); OllamaEmbeddingProvider hardcodes all-minilm-l6-v2/384 while the user runs nomic-embed-text/768; SearchApi 503 always says "vector store is down".
</context>

<scope>
- src/Overmind.Infrastructure/VectorSearch/QdrantSemanticSearch.cs: build the Qdrant client with a handler/HttpClient that has UseProxy=false when the configured endpoint host is loopback (localhost/127.0.0.1/::1); keep the endpoint-keyed client cache; remote endpoints keep default proxy behavior. Health failure log Debug → Warning with the exception message.
- src/Overmind.Infrastructure/Embeddings/: EmbeddingProviderFactory — same loopback-scoped UseProxy=false for the Ollama/OpenAI-compatible HttpClients (ONNX untouched); OllamaEmbeddingProvider takes the model name (new setting embedding.ollama.model via SettingKeys/SettingDefaults, default all-minilm-l6-v2) and discovers dimensions from the first successful embed (cache per instance) instead of hardcoding 384.
- SettingsApi: persist/validate the new model key (non-empty, ≤100 chars); settings response carries it.
- SearchApi semantic path: distinguish qdrant-down (existing message) vs embedding failure (name the provider + model + error) vs dimension mismatch (existing recreation path message) — three honest 503 details.
- Tests: loopback proxy-off assert (handler/client configuration reachable via the factory seams), model setting round-trip + provider uses it, honest 503s (stub failures), dimension discovery (stub embed vector length), existing SemanticDegradedTests stay green.
</scope>

<anti-scope>
- No health-shape changes (task 02), no settings UI, no test endpoint.
</anti-scope>

<constraints>
- .NET 10 / C# 14; no new packages; no comments; English-only.
- dotnet build + test green (baseline 856; record counts). Flakes: Mcp port race (rerun), CLR (build-server shutdown). A web coder may run npm — ignore.
</constraints>

<acceptance-criteria>
- [ ] Loopback Qdrant/embedding clients constructed proxy-off; remote unchanged (assert via seams).
- [ ] embedding.ollama.model flows end to end; provider uses it; dims discovered from the response.
- [ ] Semantic 503 with a dead embedding provider names provider+model+error; qdrant-down keeps its message.
- [ ] Qdrant health failure logs at Warning.
- [ ] dotnet test green incl. new tests.
</acceptance-criteria>

<refs>
- Spec: feature 025 (live diagnosis chain), plan risks (QdrantClient handler overload).
- Pattern refs: QdrantSemanticSearch.cs:33-47,136-163; EmbeddingProviderFactory.cs:21-43,62; OllamaEmbeddingProvider.cs:6-10,29-45; SettingKeys/SettingDefaults; SearchApi.cs:57-69,92-96; MCP proxy precedent commit 7394389 (SocketsHttpHandler UseProxy=false).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — dotnet-coder subagent (parallel with the 024/02 web coder; disjoint trees).
- 2026-09-20 — Coder done: LoopbackHttp helper (Uri.IsLoopback + proxy-bypass handlers), QdrantClient via GrpcChannel+QdrantGrpcClient wrapper (SDK 1.19 has no HttpClient ctor; grpc-dotnet 2.71 renames the option to HttpHandler — verified via reflection), endpoint cache kept, health log Debug→Warning; embedding.ollama.model setting + dynamic dims (first-embed ??= cache); honest 503s via EmbeddingFailedException/VectorDimensionMismatchException (IEmbeddingProvider gained Model); settings round-trip+validation. LIVE PROOF: HealthApiTests with the real proxy env now reports vectorSearch "up" — the old test asserted the bug itself (rewritten to oneOf). Suite 856 → 888 (+32). To review.
- 2026-09-20 — Review PASS-WITH-NITS (audits/review-025-01.md): 0 blockers, 3 WARNING + 1 SUGGESTION + 8 INFO. GrpcChannel wrapper equivalence PROVEN by decompiling Qdrant.Client 1.19; live health "up" under the full proxy env re-proven by the reviewer; 888/888 re-run; the 384-provisional double-recreation traced impossible (EnsureCollection always sees the real vector length). W1 (scoped service → per-request channel pool, no dispose — singleton follow-up), S3 (residual catch-all masks timeouts as vector-down), S4 (explicit Grpc.Net.Client pin) → microbatch. W2 (per-instance Dimensions cache = 384 until first embed — task 02 health MUST probe by embed or report unknown) carried into 025/02's contract. Done.
</log>
