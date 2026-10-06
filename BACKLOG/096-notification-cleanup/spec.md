---
feature: 096
slug: notification-cleanup
status: validated
created: 2026-09-28
validated: 2026-09-28
---

# Spec 096 — Notification cleanup: delete + bulk actions в колокольчике (per-row ×, mark all read, clear read)

<outcome>
Human-door housekeeping for AgentNotification: per-row delete, bulk «mark all read», bulk «clear read» (delete every read notification of the project). The bell stops being an append-only landfill: the human can empty it in two clicks. Agent door untouched.
</outcome>

<decisions>
- Hard delete, no archive/undo — notifications are ephemeral signal, not records. Simplest mental model: deleted = gone.
- Endpoints (human door, src/Overmind.Host/Api/NotificationsApi.cs, project resolved by existing convention — the door is GLOBAL, no projectId parameters):
  - `DELETE /api/notifications/{id:guid}` → 204; unknown id → 404 (same error shape as mark-read). Works on any row, including replied proof rows — explicit single-row act.
  - `POST /api/notifications/read-all` → marks every unread notification read (sets ReadAt=now where null); idempotent; returns { marked: int }.
  - `POST /api/notifications/clear-read` → hard-deletes every read notification (ReadAt != null) EXCEPT proof-bearing rows (ReplyText != null); returns { deleted: int }.
- Proof-row preservation (spec v2, 2026-09-28): the 084 reply⇒read invariant makes replied rows read, so a naive clear-read would destroy the 077 validation-proof trail (replied rows ARE the proof read model — architect-review-2026-09-28-096 WARNING-1). Bulk cleanup therefore never touches replied rows; per-row DELETE remains the explicit remover. User-approved.
- Service layer in src/Overmind.Infrastructure/Notifications/NotificationsService.cs — three new methods, each returns the affected count; EF ExecuteUpdate/ExecuteDelete style (no entity materialization for bulk), per-row delete by id.
- Reply⇒read invariant (084) holds: read-all and clear-read operate on ReadAt; replied notifications are legitimately read → preserved by clear-read as proof rows, deletable per-row. Approval awaiting reply stays unread → untouched by both.
- Web (FSD, features/notifications slice): api.ts gains deleteNotification/readAllNotifications/clearReadNotifications on the same relative paths; lib/use-notification-mutations.ts gains three mutations; optimistic cache surgery on the notifications query (remove item / patch ReadAt / drop the read tail minus replied rows) + family-wide invalidation (notificationKeys.all — future 087/093 queries under the same family stay fresh); unreadCount derives from cache patch so the badge moves instantly.
- UI placement (web/src/widgets/app-shell/ui/notification-center.tsx): header row of the center gets two ghost buttons — «Mark all read» (disabled when unreadCount=0) and «Clear read» (disabled when no clearable read items in the loaded list); notification-row.tsx gets a quiet × button (visible on hover/focus, aria-label «Delete notification», not rendered while the reply form of that row is pending). Destructive actions are one-click (no confirm modal) — the whole point is cheap cleanup; info lost is by design.
- MCP agent door: not extended. Agents create; humans clean.
</decisions>

<open-questions>
- none (user delegated autonomous decisions 2026-09-20; cleanup semantics fixed by the 2026-09-28 pain report — «жму колокольчик — там пиздец»)
</open-questions>

<requirements>
Backend:
- NotificationsService: MarkAllReadAsync, ClearReadAsync, DeleteAsync (+ count returns).
- NotificationsApi human group: three new endpoints per decisions; validation-free (no bodies except none), errors via existing PlanningErrors/error shape conventions.
- Tests (tests/Overmind.Api.Tests): delete removes row + 404 on unknown; read-all sets ReadAt only where null and returns count; clear-read deletes only ReadAt != null and returns count; replied notification is unaffected by read-all and removed by clear-read.
Web:
- features/notifications: api.ts + types (BulkResult { marked|deleted }) + queries invalidation wiring.
- notification-center.tsx header actions with disabled states; notification-row.tsx × button.
- No new shared UI primitives; ghost/quiet buttons per existing app-shell styles.
</requirements>

<anti-scope>
- Server-side retention/TTL, scheduled auto-purge.
- Agent-door MCP deletion tool.
- Undo/archive, pagination of deleted items.
- Filters/search/kind-tabs in the center (085/087).
</anti-scope>

<acceptance-criteria>
- [ ] DELETE /api/notifications/{id} removes the row; second call 404. Works on replied proof rows too (explicit per-row act).
- [ ] read-all marks all unread read (badge → 0), returns marked count; clear-read deletes the read set minus replied proof rows (077 trail preserved), returns deleted count.
- [ ] Bell center: «Mark all read» and «Clear read» work with optimistic badge/list updates; per-row × removes the row without reload.
- [ ] Replied/approval flow unbroken: reply still marks read (084 invariant), unread approvals survive read-all and clear-read; replied proof rows survive clear-read.
- [ ] lint + tsc + build green; dotnet test green including new coverage.
</acceptance-criteria>

<refs>
- BACKLOG/020-agent-notifications/spec.md — original notification center contract (cap 50, unreadCount, mark-read, reply).
- BACKLOG/084-notification-human-actions/ — reply⇒read invariant + kind filter (initialKind) this spec must not break.
- BACKLOG/095-os-desktop-notifications/ — future retention/TTL talk lives there.
- src/Overmind.Host/Api/NotificationsApi.cs:13-20 — existing doors/routes.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-28 [agent] — spec drafted from the user's cleanup pain report; routes verified against NotificationsApi.cs and web api.ts before writing.
- 2026-09-28 [agent] — validated with human proof: notification eabfb2e8-c909-4307-a290-f5e249d83203 replied 2026-09-28 10:56:10 UTC
- 2026-09-28 [agent] — review 096/01 clarification (NIT-2, audits/review-096-01.md): the human notification door is GLOBAL by existing convention (list/mark-read/reply carry no projectId) — the decisions wording «of the project» means the host's resolved project context convention, not a projectId parameter. Implementation is correct; task 02 (web) must not assume project-scoped endpoints.
- 2026-09-28 [agent] — spec v2: clear-read excludes replied proof rows (077 proof trail survives bulk cleanup; per-row DELETE is the explicit remover) + family-wide invalidation — architect-review-2026-09-28-096 WARNING-1/SUGGESTION-2, fix-round applied in-tree, going to user for re-validation
- 2026-09-28 [auto] — artifact demoted: validated → draft
- 2026-09-28 [agent] — AC section amended for spec v2: proof-row semantics on DELETE/clear-read.
- 2026-09-28 [agent] — validated with human proof: notification acae43e7-4f9d-46e9-8a4d-51c8a3d35e2c replied 2026-09-28 11:59:06 UTC
</log>