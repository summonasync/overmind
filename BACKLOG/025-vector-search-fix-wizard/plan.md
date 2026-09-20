---
feature: 025
slug: vector-search-fix-wizard
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Plan 025 — Vector search: proxy/model fixes + honest diagnostics + setup wizard

<approach>
Fix first, wizard second. Task 01 is the bug-killer (proxy bypass + configurable model + honest errors) — backend only, ships the user's pain away. Task 02 is the diagnostics/wizard layer (structured health, settings fields + Test button, banner reasons, reindex nudge) — backend + web. Live e2e on the user's real stack (squeezr Qdrant + Ollama with nomic-embed-text) closes it.
</approach>

<contract>
- 01: loopback UseProxy=false (Qdrant + embedding clients), embedding.ollama.model setting + dynamic dims, honest semantic 503s, Warning-level qdrant health logs.
- 02: structured health object, POST /api/settings/test, settings UI (endpoint + model + Test), banner reason + settings link, reindexRecommended nudge, health invalidation on save.
</contract>

<tasks>
- [ ] 01-proxy-model-honest-errors — backend fixes + tests → tasks/01-proxy-model-honest-errors.md
- [ ] 02-diagnostics-wizard-closeout — structured health + settings test UX + banner + live e2e on the user stack → tasks/02-diagnostics-wizard-closeout.md
</tasks>

<risks>
- QdrantClient gRPC handler configuration: the client lib takes an optional HttpClient/handler — verify the overload that lets us inject UseProxy=false without breaking the existing cache.
- Health object shape is additive: old boolean consumers must keep working (web StatusChip reads vectorSearch string today — coordinate 01→02 sequencing or keep .up flat field).
- The user's proxy env is present in agent-spawned shells — the 0.1.8 host currently runs WITHOUT it (orchestrator restart); after the fix, restart WITH the env to prove immunity.
</risks>

<verification>
- dotnet build/test green; web lint/tsc/build green; live: host restarted WITH proxy env → health up; nomic-embed-text end-to-end (768 dims, collection recreated, reindex green, semantic hits).
</verification>
