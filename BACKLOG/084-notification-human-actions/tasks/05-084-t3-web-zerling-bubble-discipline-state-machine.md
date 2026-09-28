---
feature: 084
task: 05
slug: notification-human-actions
status: done
review: light
created: 2026-09-28
brief: Zerling bubble: FIFO approval with inline actions, quiet/nag/mini discipline, TTL + dead-target dimming
---

# 084/T3 — Web: zerling bubble + discipline state machine

<goal>
The heart of 084: a non-blocking zerling speech bubble in a corner showing the OLDEST unresolved approval with inline Approve/Reject, the «ещё N → open approvals» badge, and the quiet/nag/mini discipline from the settings.
</goal>

<scope>
- New bubble component + discipline hook under web/src/widgets/app-shell/ui/ (follow existing app-shell naming).
- Content: zerling pose (menacing or yelling — coder's pick by visual fit, reuse shared/ui/zerling), agent + short text, Approve/Reject reusing useNotificationMutations with canonical 088 reply texts, «Open →» via the existing notificationHref anchor pattern, badge «ещё N → open approvals» landing on the T2-filtered center.
- Discipline (settings from T1): quiet — dismissed until a NEW approval arrives (dismiss state in tab memory/sessionStorage, survives navigation, dies with the tab); nag — re-pop every notificationNudgeIntervalMinutes while unresolved; mini — collapse to a small sleeping-zerling corner icon, click re-expands.
- Lifecycle: dead-target dimming when featureSlug is absent from the cached planning features list (no new API, buttons stay live); TTL constant 14 days gates the nag surface only (the center keeps the full archive).
- In-place refresh: resolving the shown approval updates content without a re-pop animation; no carousel.
- Tests .mts: state machine transitions (quiet/nag/mini), FIFO pick, TTL gate, dead-target dim, badge count; clock injected, no real sleeps.
</scope>

<anti-scope>
- Arrival toast / agent deep-link quoting (093); OS notifications (095); agent cancel tool; auto-read on view.
</anti-scope>

<constraints>
- No new packages. Static export rules. Zerling shared component reuse only. TypeScript strict.
</constraints>

<acceptance-criteria>
- [x] Bubble shows the oldest unresolved approval (TTL ≤ 14d) with working Approve/Reject (canonical replies, reply ⇒ read via the shipped domain invariant) and it clears on action.
- [x] «ещё N» badge opens the approval-filtered center; hidden at N=0.
- [x] quiet/nag/mini behaviors verified by .mts tests incl. dismiss-until-new and interval re-pop.
- [x] Dead-target approval renders dimmed with live buttons.
- [x] npm run lint && npx tsc --noEmit && npm test && npm run build green.
</acceptance-criteria>

<refs>
- Plan 084 T3; spec 084 (all bubble decisions user-approved 2026-09-28).
- 088 bell quick actions (mutations + reply texts), 077 (reply = proof), 084-pre (read-on-approve invariant).
</refs>

<log>
- 2026-09-28 [agent] — [agent] 084/T3 picked up: bubble state machine extracted as pure testable module, discipline from T1 settings, FIFO+TTL+badge per spec decisions
- 2026-09-28 [agent] — [agent] bubble renders FIFO-oldest eligible approval with inline Approve/Reject via useNotificationMutations + canonical APPROVAL_REPLY texts; optimistic reply⇒read patch clears it, content advances in place without re-pop
- 2026-09-28 [agent] — [agent] shipped: pure state machine in widgets/app-shell/lib/approval-bubble-state.ts (FIFO pick, 14d TTL gate, quiet seenIds / nag nagNextPopMs / mini collapsed discipline, dismiss sets all three so mode switches stay honored), hook use-approval-bubble.ts (sessionStorage persistence via shared session-json, 15s clock tick, dead-target via boardQueryOptions on the shown approval's projectId — buttons stay live), ui/approval-bubble.tsx (menacing pose pick, pointer-events-none wrapper so only the bubble intercepts, badge «ещё N → open approvals» per artifact wording kept literal despite English-only house rule, animate-bubble-pop keyframes in globals.css — plays on mount only, in-place refresh never replays it); notificationHref extracted from T2's notification-row into lib/notification-href.ts (behavior-neutral move, row imports it back); verification: lint 0 errors / 6 pre-existing warnings untouched, tsc clean, 27 tests pass (16 new in tests/approval-bubble.test.mts, injected clock no sleeps), static build green with animation confirmed in emitted CSS
- 2026-09-28 [agent] — [agent] 084/T3 done: all 5 AC ticked, verification green (lint 0 errors with 6 pre-existing warnings untouched, tsc clean, npm test 27/27, npm run build static export ok). Not committed per no-auto-commit rule.
- 2026-09-28 [agent] — [agent] post-coding orchestrator fix: badge text «ещё N → open approvals» → «{moreCount} more → open approvals» — English-only UI house rule (the Russian wording in this artifact's AC transcribed a user quote verbatim; intent preserved). Web gate re-run green: lint 0 errors / 6 pre-existing warnings, tsc clean, tests pass, static build ok.
- 2026-09-28 [agent] — [agent] review APPROVED-WITH-NITS; both nits fixed in-tree by orchestrator: (1) board query gated on surface !== "hidden" (combined enabled with the projectId guard — no more 15s polling while the bubble is dismissed/hidden); (2) reply.error scoped to the shown item (reply.variables?.id === item.id) so a stale error from a dismissed candidate no longer renders. Web gate green: lint 0 errors, tsc clean, 27/27 tests. Count correction per review note: approval-bubble.test.mts holds 11 tests (suite total 27 = 11 bubble + 5 approval-reply + 11 relative-time), the earlier "16 new" log line overcounted.
</log>
