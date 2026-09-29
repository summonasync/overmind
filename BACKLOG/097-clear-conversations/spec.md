---
feature: 097
slug: clear-conversations
status: validated
created: 2026-09-29
validated: 2026-09-29
---

# Spec 097 — Clear conversations: guarded bulk-delete of replied notifications (077 proof-aware)

<goal>
One button empties the bell of answered conversations WITHOUT deleting anything: replied rows get hidden (HiddenAt stamp), staying in the DB as permanent history and as eternally-valid 077 validation proofs. A "Show hidden" toggle in the center reveals the archive. The 077 proof guard problem dissolves entirely — hidden rows are still found by the proof search, so no cross-module guard is needed.
</goal>

<decisions>
- HIDE, not delete (user decision 2026-09-29: «удалять их из видимости просто, а не из базы? Оставлять для истории?»): AgentNotification gains nullable HiddenAt; "Clear conversations" bulk-stamps HiddenAt on replied rows (ReplyText != null && HiddenAt == null). Reversible by design, history preserved.
- What hidden means per surface:
  - GET /api/notifications (list + unreadCount): hidden rows excluded (filter before the Take(50) — hidden rows must not occupy list slots).
  - Agent GET /api/agent/notifications/replies: NOT filtered by hidden — the agent's answer history and the 077 proof search (BuildHumanProofLineAsync) stay unconditionally complete. This is the whole point: hiding is a UI concept, never a validation concept.
  - Per-row × (096): still a real DELETE — works on hidden rows from the archive view (the explicit hard-delete path, unchanged).
- Endpoint: POST /api/notifications/clear-conversations → { hidden: N } (human door, global, no projectId — 096 conventions). Idempotent (re-hidden rows don't re-count).
- Migration: AddNotificationHiddenAt (nullable datetime, no index — the table is small and list queries already order by CreatedAt).
- Web: "Clear conversations" in the center header next to "Clear read"; disabled when repliedCount == 0. Mutation: no optimistic patch — server-authoritative hidden-set, plain mutation + invalidate notificationKeys.all (096 finding 2 idiom, same honesty as markRead). After settle the replied rows vanish from the default view.
- Archive UX: "Show hidden" toggle in the notification center (client-side, switches the list query to include hidden; hidden rows visually dimmed, × available). No unhide action in v1 — hiding is permanent visibility-wise; × is the only way a row fully leaves.
- 096's "Clear read" (deletes read non-replied rows) stays as is — the two buttons now split cleanly: Clear read = hard-delete spendable rows, Clear conversations = hide the history rows.
</decisions>

<open-questions>
- none pending — the hide-vs-delete fork was decided by the user in chat 2026-09-29
</open-questions>

<requirements>
Backend:
- Core: AgentNotification.HiddenAt (nullable datetime, entity method Hide(); no Restore in v1) — migration AddNotificationHiddenAt.
- NotificationsService: ClearConversationsAsync(ct) → int (ExecuteUpdate HiddenAt=UtcNow where ReplyText != null && HiddenAt == null); GetOverviewAsync filters HiddenAt == null on both list and unreadCount; ListRepliesAsync unchanged (no hidden filter).
- NotificationsApi: POST /api/notifications/clear-conversations → 200 { hidden: N }; contract HideConversationsResponse(int Hidden). Human list endpoint gains optional ?includeHidden=true returning hidden rows (same response shape; items already carry HiddenAt).
- Contract pins: human list default excludes hidden (raw-JSON), includeHidden returns them; agent replies return hidden rows.
Tests (Api):
- clear-conversations hides only replied-unhidden; idempotent second call → 0; unread rows untouched; list drops them by default; includeHidden=true shows them with HiddenAt set; agent replies still return hidden rows; existing 077 suite stays green.
Web:
- Button state (disabled when no replied rows), mutation + invalidation via the 096 helpers; archive toggle renders hidden rows dimmed with × working; lint + tsc + build + tests green.
</requirements>

<non-goals>
- No unhide/restore action in v1 (hide is permanent visibility-wise; × is the exit).
- No per-row × changes, no read-state changes, no 077 changes (the proof search is untouched and now unconditionally safe).
- No agent-door surface; no auto-hide TTL (retention territory, not this).
- No optimistic web patch (documented decision — hidden-set is server-authoritative).
</non-goals>

<acceptance-ideas>
- User with 50 replied rows: click → bell shows only unread/live rows; "Show hidden" reveals all 50 dimmed; agent still sees every reply via overmind_replies; a pending draft artifact still validates via 077 (the proof row is hidden but present).
- Second click: { hidden: 0 } — idempotent.
- × inside the archive view hard-deletes that one row (096 × semantics unchanged).
</acceptance-ideas>

<refs>
- Feature body intake (field report 2026-09-29) + user fork decision in chat 2026-09-29 («удалять из видимости… оставлять для истории»).
- 096-notification-cleanup — mutation idiom, bulk endpoint conventions, header buttons row; architect-review-2026-09-28-096.md finding 1 (the exclusion this feature supersedes with hiding).
- 077 protocol — PlanningArtifactService.BuildHumanProofLineAsync (:417-436); hidden rows remain visible to it by design.
- 084 — reply⇒read; 020 — replies endpoint contract (unchanged).
</refs>

<log>
- 2026-09-29 [agent] — spec drafted from the field report: threshold-based proof guard computed inside the delete, honest {deleted, protected} split, badge-first UI.
- 2026-09-29 [agent] — spec v2: user forked to hide-not-delete in chat; rewritten — HiddenAt stamp, archive toggle, 077-safe by construction, no cross-module guard.
- 2026-09-29 [agent] — validated with human proof: notification e765b0e5-a78a-4178-993d-6ffa43c3a7ce replied 2026-09-29 10:53:47 UTC
</log>