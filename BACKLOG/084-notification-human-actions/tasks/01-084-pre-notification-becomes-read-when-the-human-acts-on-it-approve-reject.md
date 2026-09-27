---
feature: 084
task: 01
slug: notification-human-actions
status: todo
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