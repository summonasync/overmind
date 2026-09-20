---
feature: 027
task: 01
slug: search-dogfood
status: done
review: light
created: 2026-09-20
brief: Bug: semantic search failure surfaces in web as bare "search failed" — the honest 503 detail (provider/model/error + hint) never reaches the user; render it + link to Settings.
---

# 027/01 — Search failure UX: render the honest 503 detail instead of a bare "search failed"

<goal>
When semantic search fails in the web UI, the user sees WHY (provider/model/error from the ProblemDetails detail) and gets a path to Settings — instead of a bare "search failed" toast/message.
</goal>

<context>
First live dogfood incident 2026-09-20: user's first semantic query in the web → "search failed". API evidence from the live host (HEAD build 4d31643, port 4761): GET /api/search?q=vector&mode=semantic → 503 with a rich honest detail: "Semantic search is unavailable because Embedding provider 'ollama' failed for model 'all-minilm-l6-v2': Response status code does not indicate success: 404 (Not Found). Check the embedding settings or use mode=fulltext." The 025/01 backend work made errors honest — the web error path drops them on the floor. Root incident resolved separately by 025/02 (model setting UI); this task is the UX leg of the story.
</context>

<scope>
- Trace the web search error path (search input/hook/query + error rendering — likely features/search or entries slice) to where the 503 lands today; capture what the user actually sees (screenshot or text) as evidence in the log.
- Render the ProblemDetails detail (or its meaningful parts: provider, model, inner error) in the existing error surface; keep it non-blocking and dismissible per current UX patterns; add an "Open settings" link when the failure is embedding-settings-shaped (503 semantic-unavailable). Plain anchor, same pattern as the 025/02 DegradedBanner link.
- Verify: lint + tsc + build green; live repro — wrong-model 503 shows the reason in the UI (works on the user's live host after 025/02 deploys, or against a locally started host with a dead model name).
</scope>

<anti-scope>
- No backend changes (503 payload already carries everything needed); no retry/backoff logic; no toast system rework — ride the existing error surface.
</anti-scope>

<constraints>
- TypeScript strict; RSC-first; error state lives where the query lives (React Query error) — no useEffect fetching; English-only UI text.
</constraints>

<acceptance-criteria>
- [ ] Evidence captured: what the user sees today on a semantic 503 (before-state).
- [ ] After: the UI shows provider/model/error detail from the 503 payload + "Open settings" link on embedding-shaped failures.
- [ ] fulltext mode unaffected; healthy semantic search unaffected.
- [ ] lint/tsc/build green.
</acceptance-criteria>

<refs>
- Live evidence: 2026-09-20, host 4761 (HEAD 4d31643) — exact 503 payload in this task's context.
- Pattern refs: 025/02 DegradedBanner (failing-leg reason + settings link), ApiError error surfacing (review-023-03 traced the 400→ApiError→ErrorText chain — reuse it).
- Backend (read-only ref): src/Overmind.Host/Api/SearchApi.cs:10-109 (503 shape), EmbeddingFailedException (Core).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Carved from the first dogfood incident; API evidence captured live from the running host; before-state screenshot still to be taken during execution.
- 2026-09-20 — Done: TransitionOnArtifactCreate — Review→inProgress demote on Task create only (non-task creates on review untouched, done never resurrects), both create branches + same SaveChanges; 3 tests (922/922). Review PASS 0 findings-blocking (audits/review-023-06.md, 3 INFO). Follow-up candidate noted (INFO-1): reopening a done task on a review feature does not demote — carve if multi-wave flows start reopening tasks after review.
</log>
