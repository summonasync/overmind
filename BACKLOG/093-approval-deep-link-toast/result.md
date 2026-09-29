---
feature: 093
slug: approval-deep-link-toast
status: validated
created: 2026-09-28
validated: 2026-09-29
---

# Result 093 — Approval reach: agent quotes a deep-link in chat + global approval toast in an open Overmind

## Outcome

All three tasks done, all layers green, feature live in the working tree (uncommitted, awaiting the wave commit):

- **01 Host (done, PASS-WITH-NITS):** agent-door create response stamps an absolute deep-link `url` (BrowserUri.FromLoopback origin + NotificationLinks builder, param order project→feature→tab mirroring web's notificationHref). Field literally omitted without featureSlug ([JsonIgnore(WhenWritingNull)]). Human door byte-identical (separate record + untouched mapper). Origin: singleton bound after StartAsync, per-request fallback (request Host → FromLoopback) for unbound hosts (WebApplicationFactory), port-0 guard. 4 Api pins.
- **02 MCP (done, PASS):** Notify() verified pure passthrough (OvermindApiClient returns the host document unmodified) — url reaches the tool result for free; code untouched. Quote-the-url instruction landed on all three agent-facing surfaces (tool description, ServerInstructions, SessionBootstrapPrompt both branches); 4 contract pins incl. a verbatim-survival pin. Tool count unchanged (34).
- **03 Web (done, PASS-WITH-NITS):** awaiting filter widened via new isAwaitingResponse (kind ∈ {approval, question}, unread, unreplied) — bell's read-tolerant isAwaitingApproval untouched; question rows render text + Open without Approve/Reject; mixed-set "more" button opens the center unfiltered (centerKind="all" when a question awaits, else 084's approval focus) with an honest label; (N) title counter as a pure formatter + SSR-safe effect riding the unconditionally-mounted ApprovalBubble; quiet/nag + deadTarget + dismiss discipline govern both kinds. 9 new web tests (36/36 total).

## Verification

- dotnet build 0/0; dotnet test **1644/1644** (Core 225, Infrastructure 535, Api 496, Mcp 388) — architect-reviewer-run.
- Web gates reviewer-run: lint 0 errors (6 pre-existing warnings in untouched files), tsc clean, static export build OK, 36/36.
- **Live e2e** (isolated host: HOME=/tmp/overmind-093-walk, port 4895, PID-scoped cleanup; prod 4761 untouched):
  1. approval w/o projectId → `"url":"http://overmind.localhost:4895/dashboard?feature=…&tab=spec"` (FromLoopback rewrote 127.0.0.1; no project param)
  2. approval with projectId → 3 params in project→feature→tab order
  3. question w/o featureSlug → no url field in raw JSON (omission, not null)
  4. GET the deep link → **200 text/html**, including the overmind.localhost:4895 Host form the stamped url actually uses
  5. reply on approval → ReadAt stamped (084 invariant live), unreadCount 3→2
  6. UI layer (bubble visuals, title counter) is not headless-verifiable — user click-check, listed below.

## Deviations & notes

- Task-01 premise «app.Urls is empty under WebApplicationFactory» was inaccurate: the test host yields the placeholder `http://127.0.0.1:0` — hence the port-0 guard; the fallback path is what Api pins exercise, bound path covered by BrowserUriTests + live e2e (architect finding 4).
- Repo web runner is node:test, not vitest (task wording); coder followed the repo, noted in obstacles.
- Architect APPROVED (audits/architect-review-2026-09-28-093.md): 2 SUGGESTIONs microbatched (#135 web pin for notificationHref + deep-link ADR; #136 single const for the triplicated url-quoting instruction), 5 READ planes clean. SUGGESTION/nits from per-task reviews: microbatch #128–#134.
- Deep-link mortality (096 fallout): url targets feature pages, not notification rows — deleted rows cannot 404 a pasted link; the bubble already dims dead targets.
- UI click-check (user door): question bubble surface without Approve/Reject, (N) tab counter appearing/dropping, «N more → open all» label on a mixed set.

## Follow-ups

- Poll cadence (15s → faster per-query acceleration) — only if latency bites; SSE tombstoned in spec.
- #135/#136 + #128–#134 in the microbatch queue.
- 087 spec inputs: awaitingCount/centerKind exported ready; Replies mapper swap if reply rows must re-quote urls.

<log>
- 2026-09-28 [agent] — result drafted after architect APPROVED + live e2e; feature moves to user's result-validation gate.
</log>