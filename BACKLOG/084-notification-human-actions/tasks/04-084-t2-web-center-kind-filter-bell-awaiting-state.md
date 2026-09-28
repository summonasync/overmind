---
feature: 084
task: 04
slug: notification-human-actions
status: done
review: light
created: 2026-09-28
brief: Notification center kind filter + bell awaiting-state for unresolved approvals
---

# 084/T2 — Web: center kind filter + bell awaiting-state

<goal>
Notification center gains a kind filter (the landing surface for the bubble's «ещё N → open approvals» badge), and the bell shows a distinct awaiting-state while any approval is unresolved.
</goal>

<scope>
- web/src/widgets/app-shell/ui/notification-center.tsx — kind filter (all/approval/question/info, default all); externally settable preset (approval) so the bubble badge can land with the filter pre-applied (component prop or shared state — no next/link, static-export rule).
- web/src/widgets/app-shell/ui/notification-bell.tsx — awaiting visual state when ≥1 notification with kind=approval and no replyText exists (independent of unread count; no TTL here — bell state is constant, not a nag surface).
- types only if needed within features/notifications.
</scope>

<anti-scope>
- Bubble component (T3); settings UI (T4); any new backend surface.
</anti-scope>

<constraints>
- Static export: plain anchors/props only, no next/link/useRouter. TypeScript strict, English-only.
</constraints>

<acceptance-criteria>
- [x] Center filters by kind; default all; read/unread styling unaffected.
- [x] Approval preset reachable from outside the modal (landing point for T3).
- [x] Bell awaiting-state appears with an unresolved approval and clears when the last one is answered.
- [x] npm run lint && npx tsc --noEmit green; npm test green.
</acceptance-criteria>

<refs>
- Plan 084 T2; spec 084 (bell awaiting-state decision).
</refs>

<log>
- 2026-09-28 [agent] — [agent] started: kind filter via initialKind prop (plan sanctions "параметр компонента"; Modal remounts children on open, so prop landing works without shared open state), bell awaiting predicate from existing query items.
- 2026-09-28 [agent] — [agent] done: all 4 AC ticked. Center: kind chips (all/approval/question/info, default all, house chip pattern from board-filter-bar) + initialKind prop as the external approval preset — Modal unmounts children while closed, so each open re-applies the prop; T3 bubble lands via its own Modal + <NotificationCenter initialKind="approval" />. Bell: awaiting = any approval with non-string replyText (same semantics as NotificationRow's replied), warning border/text + pulsing dot, independent of unreadCount, driven by existing LIVE_SYNC_INTERVAL_MS query. lint 0 errors (6 pre-existing warnings, unrelated files), tsc clean, tests 16/16. No new deps, no backend.
- 2026-09-28 [agent] — [agent] review APPROVED-WITH-NITS; nit #1 fixed in-tree by orchestrator: awaiting-approval predicate extracted to isAwaitingApproval (features/notifications/lib/approval-reply.ts, barrel export), bell now uses it — T3 consumes the same helper. lint+tsc+test 16/16 green. Notes (initialKind mount-time read, a11y) accepted as-is.
</log>
