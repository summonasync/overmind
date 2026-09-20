---
feature: 025
slug: vector-search-fix-wizard
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 025 — Vector search: proxy/model fixes + honest diagnostics + setup wizard

<outcome>
Feature 025 delivered both tasks live: vector search works end-to-end on the user's stack with a configurable Ollama model (nomic-embed-text, 768 dims). Task 01 fixed the loopback proxy bypass + hardcoded model + honest 503s; task 02 added the structured health report (per-leg qdrant/embedding with real probe-embed — the W2 Dimensions trap closed and regression-pinned), POST /api/settings/test with candidate values, qdrantEndpoint setting + reindexRecommended, and the web wizard (failing-leg banner + settings link, model/endpoint fields, Test button with per-leg verdicts, reindex nudge).
</outcome>

<evidence>
- Gates: dotnet 919/0 (baseline 888 → +31 across the feature); web lint 0 errors / tsc 0 / static-export build green.
- Reviews: audits/review-025-01.md (PASS-WITH-NITS), audits/review-025-02.md (PASS-WITH-NITS both halves); all non-gating findings either fixed in-loop (probe TTL cache 30s singleton, nudge save.reset) or queued to microbatch (fresh-install onnx wording, stale Test verdicts, poll/cache interval, QdrantClient dispose family, Grpc.Net.Client pin).
- Live e2e 2026-09-20 (fresh 0.1.9 pack deployed to ~/apps/overmind): structured health with flat up; candidate test → 768 dims from real probe; PUT → reindexRecommended; reindex 33/33 in 3m31s; semantic hits real (EN paraphrase + RU); wrong-model honest error; health fully green post-config.
</evidence>

<deviations>
- reindexRecommended triggers include base-URL changes (superset of spec) — sound, reviewed.
- PUT /api/settings response stayed flat (additive reindexRecommended field) instead of nested — backwards compatible, reviewed.
- Probe TTL cache is a singleton (30s = web health polling interval) with bounded ≤30s staleness after a settings save; candidate test path uncached.
</deviations>

<deferred>
- Microbatch queue items 15-22 (memory overmind-microbatch-queue): QdrantClient lifecycle, SearchApi generic-catch logging, Grpc.Net.Client explicit pin, onnx fresh-install "Timed out" wording, Canned.HealthJson stale fix, VectorSearchTester logger, stale Test verdicts, poll-vs-cache interval check.
- 027-search-dogfood (draft) inherits the UX leg: web still renders a bare failure for search 503s (027/01 carved).
</deferred>
