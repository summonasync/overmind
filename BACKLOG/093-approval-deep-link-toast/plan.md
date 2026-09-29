---
feature: 093
slug: approval-deep-link-toast
status: validated
created: 2026-09-28
validated: 2026-09-28
---

# Plan 093 — Approval reach: agent quotes a deep-link in chat + global approval toast in an open Overmind

## Contract

- Shape confirmed by 2026-09-28 research: host stamps `url` on notification-create responses; adapter proxies it; web bubble widens to questions + title counter. No new endpoints, no schema changes, no new web deps.
- Layers: (01) Host, (02) MCP adapter, (03) Web + closeout. 01∥02 (disjoint files), 03 after both (its live e2e validates the whole chain).

## Tasks

- [ ] 01-host-deep-link-url — host stamps url onto agent create-response → tasks/01-host-deep-link-url.md
- [ ] 02-mcp-quote-url — adapter passthrough pin + description/bootstrap instructions → tasks/02-mcp-quote-url.md
- [ ] 03-web-question-bubble-title-closeout — question kind in bubble + (N) title counter + live e2e + result → tasks/03-web-question-bubble-title-closeout.md

## Risks

- Browser origin at startup: `app.Urls` may be empty under WebApplicationFactory — the origin resolver needs a test-viable fallback (request-derived) or the ApiFactory needs a bound URL; coder documents the choice in obstacles.
- `Notify` may already pass the host JsonElement through untouched — if so, 02's code delta is description/bootstrap text + contract pins only; that is the expected shape, not a skipped layer.
- Bubble widening touches approval-bubble-state discipline logic (quiet/nag) — question items must honor the same settings; "N more → open approvals" button currently opens the center pre-filtered to approval — recheck initialKind for mixed sets.
- document.title writes are browser-API effects (allowed), but must restore the original title on empty awaiting set and be SSR/no-op safe (static export).

## Verification

- dotnet build + dotnet test green (Api.Tests pin url presence/absence; Mcp.Tests pin passthrough + instruction text).
- Web: npm run lint && npx tsc --noEmit && npm run build; existing vitest suite stays green.
- Live e2e on isolated host: notify(approval+featureSlug) → tool response url pastes to the feature tab; question notification surfaces in the bubble on a non-dashboard page; title shows "(1)"; approve from bubble → title drops.

<log>
- 2026-09-28 [agent] — plan drafted after spec validation; 3 tasks, (01∥02)→03 pipeline.
- 2026-09-28 [agent] — validated with human proof: notification 9fded394-4560-4db8-a752-fede21bc2171 replied 2026-09-28 14:35:52 UTC
</log>