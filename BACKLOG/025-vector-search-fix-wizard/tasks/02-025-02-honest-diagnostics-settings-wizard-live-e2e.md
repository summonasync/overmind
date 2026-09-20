---
feature: 025
task: 02
slug: vector-search-fix-wizard
status: done
review: light
created: 2026-09-20
---

# 025/02 — Honest diagnostics + settings wizard + live e2e

<goal>
Health tells the truth per leg (Qdrant + embedding with provider/model/error), the settings page gains the missing fields (Qdrant endpoint, Ollama model) and a Test button, the degraded banner names the failing reason and links to Settings, a reindex nudge appears after provider/model/endpoint changes — closed by a live e2e on the user's real stack.
</goal>

<context>
Feature 025, task 2 of 2. Depends on 01 (proxy bypass + model setting + honest 503s — in tree, reviewed). CRITICAL W2 trap from review-025-01: IEmbeddingProvider.Dimensions is a per-instance cache that reads 384 until the first embed — the health embedding leg MUST probe with a real (tiny) embed call or report dimensions "unknown", never trust Dimensions.
</context>

<scope>
- Health: extend /api/health vectorSearch into { qdrant: { up, endpoint, error? }, embedding: { provider, model, up, error? }, up } — keep a flat top-level up boolean for old consumers; the embedding leg does a tiny "ping" embed (short string, 5s timeout) against the ACTIVE provider. Warning-level logs on failures.
- POST /api/settings/test: body = candidate { provider, ollamaBaseUrl, ollamaModel, openAiBaseUrl, qdrantEndpoint } (current form values, not yet saved) → per-leg { up, error } verdicts using the same probes; loopback-only.
- SettingsApi: persist qdrant.endpoint (validate http(s) URL; SettingKeys already has QdrantEndpoint); detect provider/model/endpoint changes → response carries reindexRecommended.
- Web: health types extend; DegradedBanner shows the failing leg reason + "Open settings" link (plain anchor); StatusChip keeps working off the flat up; settings form adds Qdrant endpoint + Ollama model inputs + Test button rendering per-leg verdicts; save invalidates the health query; reindex nudge row (link to the existing Reindex action) when reindexRecommended.
- Verify: dotnet build/test green; web lint/tsc/build green.
- Live e2e (orchestrator): on the user's stack — health object green with nomic-embed-text configured (model setting), Test button verdicts, reindex with 768 dims (collection recreation visible in logs), semantic search returns real hits; wrong-model case shows the honest embedding-leg error.
- Closeout (orchestrator): result artifact, validate, human door done, unpin, materialize, single feat-commit (no push).
</scope>

<anti-scope>
- No auto-reindex (nudge only), no Docker orchestration, no external channels.
</anti-scope>

<constraints>
- The embedding probe is a REAL embed call (W2) — Dimensions is untrusted until then; report dims as discovered-or-unknown in the health payload if surfaced at all.
- .NET 10 / C# 14; no new packages; no comments; English-only; web: static-export rules, TypeScript strict.
</constraints>

<acceptance-criteria>
- [ ] /api/health returns the structured object; flat up preserved; embedding leg reflects a real probe (wrong model → red with the model-not-found error).
- [ ] Settings: Qdrant endpoint + Ollama model saved; Test button shows per-leg verdicts with errors.
- [ ] Banner names the failing leg + links to settings; StatusChip unaffected.
- [ ] Changing provider/model/endpoint → reindexRecommended true; the nudge renders.
- [ ] Live e2e on the user's stack green (nomic-embed-text end-to-end, semantic hits).
- [ ] dotnet test green; web lint/tsc/build green; closeout artifacts done.
</acceptance-criteria>

<refs>
- Spec: feature 025 (health/test/banner/nudge decisions). Plan: risks.
- Pattern refs: HealthApi + HealthContracts, QdrantSemanticSearch.HealthAsync, EmbeddingProviderFactory (probe seam), SettingsApi/SettingsContracts (extend), settings-form.tsx/settings-page.tsx, degraded-banner.tsx, use-health.ts, health/queries.ts, reindex-button.tsx.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Starting execution: backend health/test endpoints + settings fields + web banner/form/nudge, then live e2e on the user's stack. User-directed session (push + finish 025 + then 023).
- 2026-09-20 — Review fix-loops (both non-gating WARNINGs): W1 probe-embed TTL cache landed (VectorSearchHealthCache singleton, TTL 30s = web health polling interval; failures cached too; settings/test candidate path uncached; stale-after-PUT ≤1 TTL window accepted) — 919 tests (+2). Web W1 nudge save.reset() in flight via next-js-coder. Microbatch: W2 onnx fresh-install wording + web stale-verdicts + poll/cache interval check.
- 2026-09-20 — Live e2e PASSED on the user's stack (fresh 0.1.9 pack from the working tree, deployed to ~/apps/overmind, host 4761): (1) structured /api/health — flat up preserved, qdrant leg green with endpoint, embedding leg honestly red pre-config (all-minilm 404, dims null); (2) POST /api/settings/test candidate nomic-embed-text → both legs green, dimensions 768 from the REAL probe (W2 closed live); (3) PUT settings saved model → reindexRecommended: true (flat response); (4) reindex 33/33 entries, 0 failed, 768-dim collection, 3m31s; (5) semantic hits real: EN paraphrase "escaping feature burial" → escape-hatch decision top hit (rank 0.617), RU query hit; health fully green (nomic-embed-text, dims 768); (6) wrong-model test → honest embedding-leg error naming the model; (7) 023/07 live check: scratch feature draft→done one PUT → 200, cleanup 204. Deploy note: pack.sh needed bash invocation (not executable bit); old host restored after a 15s false-start outage before the proper pack.
- 2026-09-20 — Done: implementation (backend 917→919 tests incl. W2 regression pin + web wizard, gates green), light review PASS-WITH-NITS both halves (audits/review-025-02.md; both non-gating WARNINGs fixed in-loop: probe TTL cache 30s + nudge save.reset), live e2e passed on the user's stack with nomic-embed-text end-to-end (evidence in log). Remaining nits → microbatch queue (W2 fresh-install wording, stale test verdicts, poll/cache interval).
</log>
