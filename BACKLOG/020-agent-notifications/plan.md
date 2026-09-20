---
feature: 020
slug: agent-notifications
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Plan 020 — Agent → human notifications via MCP with deep links + reply path

<approach>
Three layers in dependency order: the backend (entity + migration + both-door NotificationsApi + tests) defines the contract; the MCP tools (overmind_notify + overmind_replies, contract pin 16→18) give agents the push/poll channel; the web (features/notifications slice + topbar bell + modal center with deep links and replies, riding the 017 polling) closes the loop for the human. Closeout includes a live end-to-end notify → bell → reply → overmind_replies pass on the isolated dev host pattern from 023.
</approach>

<contract>
- AgentNotification entity: Id, ProjectId?, Kind (approval/question/info), FeatureSlug?, Tab?, Text (≤2000), Agent (default "agent"), CreatedAt, ReadAt?, ReplyText?, RepliedAt?; migration AddAgentNotifications (index ProjectId+CreatedAt).
- Routes: agent POST /api/agent/notifications (create), GET /api/agent/notifications/replies (replied notifications, newest-first, includeRead filter); human GET /api/notifications (newest-first, cap 50, unreadCount), POST /api/notifications/{id}/read, POST /api/notifications/{id}/reply {text}. FluentValidation on create/reply.
- MCP: OvermindNotificationTools — overmind_notify(kind, text, projectId?, featureSlug?, tab?, from?) with project resolution via OVERMIND_PROJECT_ID/cwd when projectId omitted; overmind_replies(projectId?, includeRead?) passthrough. HandshakeContractTests array 16 → 18; McpSessionTestBase stubs.
- Web: features/notifications slice (STRUCTURE.md:80 patterns, LIVE_SYNC_INTERVAL_MS polling), bell + unread badge in the topbar ml-auto cluster, Modal center (kind tones, plain-anchor deep links /dashboard?project&feature&tab, mark-read, reply input for question/approval).
</contract>

<tasks>
- [ ] 01-notifications-backend — entity + migration + both-door API + validation + Api.Tests → tasks/01-notifications-backend.md
- [ ] 02-notifications-mcp — tool class, project resolution, contract pin 18, stubs, Mcp.Tests → tasks/02-notifications-mcp.md
- [ ] 03-notifications-web-closeout — slice + bell + center, live e2e notify→bell→reply→replies, result artifact, closeout → tasks/03-notifications-web-closeout.md
</tasks>

<risks>
- The topbar is shared chrome — keep the bell self-contained (its own component consuming the notifications slice's public API; no feature imports from widgets beyond the established app-shell pattern).
- Deep links must use plain <a href> (static export, no next/link/useRouter) and valid tab values only.
- The live e2e (03) needs the isolated dev host trick from 023 (scratch cwd DB, Overmind__BindUrl) — the production host predates the tools.
</risks>

<verification>
- dotnet build/test green (record counts); web lint + tsc + build green.
- Live e2e on the isolated host: overmind_notify lands → GET list shows it unread → mark-read + reply → overmind_replies returns the reply text.
</verification>
