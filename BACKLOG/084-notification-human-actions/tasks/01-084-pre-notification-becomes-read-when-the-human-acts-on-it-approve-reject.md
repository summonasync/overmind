---
feature: 084
task: 01
slug: notification-human-actions
status: done
review: light
created: 2026-09-27
---

# 084-pre — notification becomes read when the human acts on it (approve/reject)

<goal>
When the human presses Approve (or Reject) on a notification, that notification must immediately transition to read. Pressing an action on an unread notification and seeing it stay unread is a UX contradiction — the unread badge stops meaning anything.
</goal>

<requirements>
- Any human action on a notification that resolves it (approve/reject reply via the bell quick actions 088, or the notification-center action surface) marks the notification as read in the same operation — no separate "mark read" click.
- Applies to approval AND question kinds when a reply/decision is recorded.
- The unread count and bell badge update immediately (same request, no polling lag).
- Idempotent: acting on an already-read notification stays read, no error.
</requirements>

<non-goals>
- Auto-read on mere VIEW of the notification (human may read and deliberately defer a decision).
- Changes to the agent-side reply/read flow (020 contract).
</non-goals>

<refs>
- User intake 2026-09-28: «когда я жму аппрув — нотификация должна сразу же быть read — странно нажать аппрув а она unread остаётся».
- 088-bell-approval-quick-actions (bell ✓/✗ buttons — the action surface this rides on).
- 084 feature body (this task folds into the 084 scope when the feature is specced).
</refs>

<obstacles>
- Contract shift accepted by design: reply now implies read, so the `includeRead=false` filter on /api/agent/notifications/replies (and overmind_replies) can no longer match anything — every replied notification is read by construction. This is inherent to the feature (bell quick actions approve without reading; leaving those unread forever was the bug). Default agent polling (includeRead=true / omitted) is unaffected; the MCP parameter description was updated to steer agents to the default.
</obstacles>

<log>
- 2026-09-27 [agent] — [agent] taken into work — 084-pre read-on-approve, session start 2026-09-28
- 2026-09-27 [agent] — [agent] implemented: AgentNotification.Reply() now sets ReadAt ??= now (domain invariant, idempotent); web reply mutation optimistic update sets readAt; overmind_replies includeRead description updated; +2 API facts (reply-marks-read, read-at preserved). dotnet build green, notification suites green (Api 19, Mcp 10, Infra 1), web lint+tsc clean (6 pre-existing warnings in unrelated files). Light reviews dispatched (dotnet + next-js, sonnet).
- 2026-09-27 [agent] — [agent] merged <obstacles> into the original task (order 1); the duplicate spawned at order 2 is being tombstoned
- 2026-09-27 [agent] — [agent] fix round W1 (frontend review BLOCKED): reply optimistic update now also decrements unreadCount in the same setQueryData (becameRead guard + Math.max(0,)) so the bell badge ticks with zero refetch lag; single hoisted now timestamp for repliedAt/readAt. lint+tsc green. markRead-optimistics gap logged as follow-up candidate, out of scope.
- 2026-09-27 [agent] — [agent] review passed: backend APPROVED-WITH-NITS (nit fixed same-session: ReadAt pin on second reply), frontend BLOCKED → fix round W1 (unreadCount optimistic decrement + hoisted now) → re-review APPROVED. Verdicts transcribed to audits/review-084-01.md
- 2026-09-27 [agent] — [agent] done: read-on-approve shipped (domain invariant + optimistic UI), all verification green (build 0/0, NotificationApiTests 17/17, contract 10/10, web lint+tsc clean); follow-up markRead-optimistics queued for microbatch
</log>