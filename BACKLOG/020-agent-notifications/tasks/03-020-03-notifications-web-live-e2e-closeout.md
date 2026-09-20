---
feature: 020
task: 03
slug: agent-notifications
status: done
review: light
created: 2026-09-20
---

# 020/03 — Notifications web + live e2e closeout

<goal>
The human side: a bell with unread counter in the topbar, a notification center modal (kind tones, deep links, mark-read, replies) polling at the live-sync interval; the feature closes with a live end-to-end pass (notify → bell → reply → overmind_replies) on the isolated dev host.
</goal>

<context>
Feature 020, layer 3 of 3. Depends on 01 (API) + 02 (tools for the e2e). Topbar cluster: app-topbar.tsx:50-59 (ml-auto, StatusChip + avatar). No popover primitive — the Modal is the center. Polling constant: shared/lib/live-sync.ts. Slice patterns: STRUCTURE.md:80.
</context>

<scope>
- web/src/features/notifications/: types (NotificationResponse {id, kind, featureSlug?, tab?, text, agent, createdAt, readAt?, replyText?, repliedAt?}), api.ts (list, markRead, reply), queries.ts (notificationKeys factory + queryOptions with refetchInterval LIVE_SYNC_INTERVAL_MS), lib/use-notification-mutations.ts (mark read, reply — invalidate the list key), index.ts public API.
- widgets/app-shell/ui/notification-bell.tsx: bell button + unread Badge (from the slice's useNotifications hook), opens the center Modal; slots into app-topbar's ml-auto cluster next to StatusChip; app-topbar imports the widget's public API only.
- notification-center (inside the widget or the slice — follow the FSD layering): Modal listing newest-first — kind icon/tone (approval warning, question info, success info), agent + relative time, text; deep link as a plain <a href="/dashboard?project=X&feature=Y&tab=Z"> when featureSlug present (fire-and-forget markRead on click); explicit Mark read button; reply Input + Send for question/approval kinds (one reply, overwrites — disabled + shows the existing reply once set); read items visually dimmed; unreadCount from the same query.
- Verify: npm run lint && npx tsc --noEmit && npm run build.
- Live e2e (orchestrator): isolated dev host (scratch cwd DB, Overmind__BindUrl per 023) — POST notify via the agent route → GET /api/notifications shows it unread → mark-read + reply → GET replies returns the text. Evidence into the result artifact. Cleanup: kill by exact PID (never pkill -f).
- Closeout: result artifact, validate, feature done via human door, unpin current, re-materialize, single feat-commit (no push).
</scope>

<anti-scope>
- No backend/MCP changes (fix-loops via status flow), no toast system, no push/SSE, no retention UI.
</anti-scope>

<constraints>
- Static export: plain anchors only, no next/link/useRouter; valid tab values; TypeScript strict; English-only; a11y (bell aria-label with count, modal semantics via the shared Modal).
</constraints>

<acceptance-criteria>
- [ ] Bell shows the unread count; center lists newest-first with tones/links/replies.
- [ ] Deep link lands on the workspace tab; click marks read; unread count updates.
- [ ] Reply flow: input for question/approval, one reply, shows after set.
- [ ] Live e2e evidence captured (notify → unread → reply → replies).
- [ ] lint + tsc + build green; result artifact validated; feature done; committed (no push).
</acceptance-criteria>

<refs>
- Spec: feature 020 — UI decisions. Plan: tasks + risks (topbar self-containment, isolated host).
- Pattern refs: app-topbar.tsx:50-59, shared/ui/modal.tsx + badge.tsx, features/health (slice + widget consumption precedent via StatusChip), features/planning/queries.ts (factory), shared/lib/live-sync.ts.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Web slice execution started — next-js-coder subagent (backend 01+02 in tree; review of 02 running in parallel; live e2e + closeout stay with the orchestrator).
- 2026-09-20 — Review PASS-WITH-NITS (audits/review-020-03.md) → WARN-1 fixed in-tree by the orchestrator: reply form now renders ErrorText on failure (a silent failed reply looked sent and the agent would never see the answer — the feature's core path). WARN-2 (keepalive on fire-and-forget markRead during full navigation — recovery exists via button + 15s poll) + 3 INFO (relative-time duplicate → shared/lib promotion candidate; redundant markingThis guard; badge without aria-live) → microbatch. LIVE E2E PASSED on the isolated host: agent-door notify (question, full target incl. projectId on the item) → unread 1 → mark-read → 0 → human reply → agent reads 'Ship 15s, revisit with SSE later' via replies endpoint. Scratch host killed by exact PID; prod 4761 verified alive. tsc/lint/build green after the fix. Done.
</log>
