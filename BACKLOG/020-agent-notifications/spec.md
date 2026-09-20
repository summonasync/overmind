---
feature: 020
slug: agent-notifications
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Spec 020 — Agent → human notifications via MCP with deep links + reply path

<decisions>
- Storage: a single Notifications table (SQLite, FrontmatterKey-migration template): Id, ProjectId (nullable — notifications may be project-scoped via the tool's projectId param or cwd-resolved), Kind (approval/question/info), FeatureSlug (nullable), Tab (nullable), Text, Agent (free string, default "agent"), CreatedAt, ReadAt (nullable), ReplyText (nullable), RepliedAt (nullable). No retention job in MVP — the center paginates/caps the list; cleanup deferred (decision delegated by the user 2026-09-20).
- Two additive MCP tools (16 → 18, HandshakeContractTests pin grows): `overmind_notify` (kind, text, projectId?, featureSlug?, tab?, from?) — POSTs to a new agent route; the adapter resolves the project from OVERMIND_PROJECT_ID/cwd (013's onboarding resolution) when projectId is omitted; `overmind_replies` (projectId?, includeRead?) — GETs replies on that project's notifications addressed from agents (the agent's answer channel; returns ReplyText/RepliedAt for replied notifications). Caller identity rides the /api/agent route prefix per STRUCTURE.md:33 — no auth beyond loopback.
- Host routes (both doors pattern): agent door POST /api/agent/notifications + GET /api/agent/notifications/replies; human door GET /api/notifications (list, unread count), POST /api/notifications/{id}/read, POST /api/notifications/{id}/reply {text}. Typed errors follow the KitImportException non-planning precedent if needed (validation only in MVP — plain 400s via FluentValidation suffice).
- UI: a bell button in the topbar's ml-auto cluster (next to StatusChip) with an unread counter badge; click opens a notifications Modal (no popover primitive exists — the modal is the notification center) listing newest-first: kind icon/tone, agent, text, relative time, deep link /dashboard?project=X&feature=Y&tab=Z when target present (plain <a href> — static-export rule, no next/link), Mark-read on click of the link or an explicit button, and a reply Input + Send for question/approval kinds (POST reply). Notifications query rides LIVE_SYNC_INTERVAL_MS (017) — fresh unread counts without reloads.
- Read model: clicking the deep link marks read (fire-and-forget) + navigates; explicit mark-read button for info kinds.
- Reply path closes the loop: the human replies in the center → overmind_replies returns it to the agent on its next poll. No push to agents (polling only, per the feature body's MVP reading).
</decisions>

<open-questions>
- none (user delegated autonomous decisions 2026-09-20)
</open-questions>

<requirements>
Backend:
- Core entity AgentNotification (+ Restore), EF config + migration AddAgentNotifications (index on ProjectId+CreatedAt; ReadAt/RepliedAt nullable).
- NotificationsApi (static Map<> convention, both doors): agent POST create (validation: kind enum, text required ≤2000, featureSlug/tab optional), agent GET replies (notifications with ReplyText, newest-first, includeRead filter); human GET list (newest-first, cap 50, unreadCount), POST {id}/read, POST {id}/reply (text required, appends nothing else — one reply per notification, MVP).
- MCP: OvermindNotificationTools ([McpServerToolType], Guarded, OvermindApiClient) — overmind_notify + overmind_replies; projectId resolution via env/cwd reusing the 013 onboarding helper logic (or accepting explicit projectId); ServerInstructions unchanged; HandshakeContractTests array grows to 18; McpSessionTestBase stub routes extended.
- Tests: Api.Tests (create/list/read/reply lifecycle, validation 400s, replies endpoint shape), Mcp.Tests (tool contract posts, replies passthrough).
Web:
- features/notifications slice (STRUCTURE.md:80 patterns): types, api fns, notificationKeys factory + queryOptions riding LIVE_SYNC_INTERVAL_MS, mutations (mark read, reply) with invalidation.
- widgets/app-shell: bell button (unread counter badge) in the topbar cluster; Modal center (kind tones, deep links as plain anchors, reply input for question/approval, mark-read).
- Verify: dotnet build/test green; web lint + tsc + build green.
</requirements>

<acceptance-ideas>
- Agent posts overmind_notify(question, "Spec gate wants your call", projectId, featureSlug, tab=spec) → within ~15s the bell shows +1; clicking the item opens /dashboard?project&feature&tab=spec; the human types a reply → overmind_replies returns it to the agent.
- info notification with no target → no link, just text + mark-read.
- Unread count drops on link click; read items dim; replies stick.
</acceptance-ideas>
