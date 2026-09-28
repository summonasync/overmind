---
feature: 084
slug: notification-human-actions
status: draft
created: 2026-09-28
---

# Result 084 — Notification center: human actions (approval/question) — видимость, прямые ссылки на overmind, actionability

## Outcome

- **084-pre (task 6cf21335, done)** — domain invariant: `AgentNotification.Reply()` sets `ReadAt ??= now` (idempotent). Both action surfaces (bell quick actions, center reply) ride one seam. Web reply mutation optimistically patches `readAt` AND decrements `unreadCount` in the same `setQueryData` (becameRead guard + Math.max(0)). Contract shift documented: `includeRead=false` can no longer match anything (reply ⇒ read by construction) — that shift IS the feature; default agent polling unaffected; MCP parameter description updated.
- **T1** — `notificationNudgeMode` (quiet|nag|mini, default quiet) + `notificationNudgeIntervalMinutes` (int > 0, default 10) as KV AppSettings keys, QdrantEndpoint precedent, no migration. Validation 400s on garbage. Contract round-trip with web 1:1 (verified by the T4 reviewer).
- **T2** — notification center kind filter (all/approval/question/info) with externally settable `initialKind` preset (Modal remount re-applies per open); bell awaiting-state driven by the shared `isAwaitingApproval` helper; question kind never surfaces in nag surfaces.
- **T3** — zerling speech bubble: pure state machine (`approval-bubble-state.ts`, 0 runtime imports) + `use-approval-bubble` hook + UI. FIFO oldest unresolved approval, 14-day TTL constant on nag surfaces, quiet/nag/mini discipline (server settings; dismiss in sessionStorage — survives navigation, dies with tab), dead-target dimming from the cached board query, badge «{moreCount} more → open approvals» landing on the approval-filtered center, in-place refresh without re-pop animation, non-blocking (pointer-events-none wrapper). Board polling gated on bubble visibility.
- **T5** — isolated-host walk + user smoke, details below.

## Verification

- Live walk (isolated host 127.0.0.1:4893, scratch HOME /tmp/overmind-084-t05, PID 86477, killed by PID): settings defaults quiet/10 on empty store; PUT nag/1 round-trip; invalid mode → 400; minutes 0 → 400; partial PUT restores quiet/10. Smoke scene: scratch project + live-target feature + 2 approvals (live slug / dead slug 099-vanished-feature) + question.
- **User click-check PASSED**: bubble pops top-center; Approve inline clears + unread badge drops + in-place FIFO advance to the dead-target approval rendered dimmed «target gone» with live buttons; badge lands on the approval-filtered center; question never bubbles; mini collapse/re-expand + «!» icon approved by the user («выглядит отлично»).
- Nag re-pop covered by unit tests with injected clock (not user-clicked — recorded honestly).
- Suites at close: web 27/27 (11 bubble + 5 approval-reply + 11 relative-time), lint 0 errors / 6 pre-existing warnings, tsc clean, static build ok; dotnet Api 488/488, Mcp 385/385, build 0 warnings / 0 errors (re-run by the hard reviewer independently).

## Deviations from plan

- Bubble position: plan implied bottom-right → **top-center, 100px**, per user feedback during smoke; mini icon at 56px, sprite doubled (28→56) + «!» badge — user-requested and approved on screen.
- **Center list dead-target dimming DESCOPED**: the spec's lifecycle (A) wording promised dimming in «bubbles/list», but only the bubble dims. The center keeps the honest full archive by design (dimming there would need a per-notification board query inside the modal for marginal value — the center is not a nag surface). User smoke validated the bubble-only dimming. Spec log carries the descope note; re-introducing list dimming is a microbatch candidate.
- Badge text is English-only — the task artifact's «ещё N» verbatim transcribed a user quote; house rule won, fix logged in T3.
- Docs: no CONTEXT.md/STRUCTURE.md changes — no undocumented server seam appeared (two KV settings keys ride the documented precedent; everything else is web-layer).
- Review nits across T1–T3 fixed in-tree same-session (awaiting helper extraction, honest minutes parsing + nag-leave reset, polling gate, error scoping, settings barrel export + CRLF sweep from the hard review) — verdicts in task logs.

## Follow-ups

- Duplicate task artifacts (73b6e0dc, d07f060a) tombstoned — UI deletion pending (human door); the upsert-without-order pitfall fired twice in one day → microbatch #126 guard candidate.
- markRead optimistic update — microbatch #124.
- Dead-target predicate extraction into the pure state module (unit coverage) — microbatch candidate from the hard review.
- Family queue: 093 arrival toast + deep-link quoting, 095 OS desktop notifications.

<log>
- 2026-09-28 [agent] — [agent] hard-review fixes folded in: center list-dimming descope recorded honestly (W1), barrel/deep-import and CRLF fixes verified in the web gate; hard review verdict APPROVED-WITH-WARNINGS, both warnings closed before the done gate
</log>
