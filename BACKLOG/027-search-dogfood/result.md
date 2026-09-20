---
feature: 027
slug: search-dogfood
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 027 — Search dogfood: semantic + fulltext systematic pass

<outcome>
Feature 027 opened as the systematic search-dogfood pass triggered by the user's first semantic query failing with a bare "search failed" (2026-09-20). Its carved finding 027/01 — the web dropped the honest 503 detail the backend already carried — is fixed and deployed: SearchError renders the full detail inline with an "Open settings" link on embedding-shaped failures. The systematic pass itself (snippets/ranking/empty states) continues opportunistically under future dogfood sessions; the founding incident is closed end-to-end.
</outcome>

<evidence>
- Live incident capture 2026-09-20: API 503 with rich detail ("Embedding provider 'ollama' failed for model 'all-minilm-l6-v2': 404 …") vs bare UI failure — before-state in the task log.
- Fix: web/src/features/search/components/search-error.tsx (+barrel, search-page rewiring); review PASS (audits/review-027-01.md) — chain readError→ApiError→React Query→render verified end-to-end, three 503 shapes routed correctly (embedding → link; vector-store-down / dimension-mismatch → detail without link).
- Gates: lint 0 / tsc 0 / static build green; strings verified in the exported chunk; deployed in the 20:16 pack on the live host.
- Related fixes that landed the same day and closed the incident's other legs: 025 (model setting + wizard), 031 (update_entry NRE that would have blocked correcting this very report).
</evidence>

<deferred>
- Microbatch 27: stable errorCode in ProblemDetails extensions instead of the string marker.
- Microbatch 28 (pre-existing): mode-select lock when Qdrant is down in semantic mode.
- The broader dogfood pass (snippet quality, ranking, empty states) — future sessions, findings become tasks on this feature's pattern.
</deferred>
