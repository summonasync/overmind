---
feature: 101
slug: approval-project-prominence
status: draft
priority: p1
version: 1
created: 2026-10-03
---

# Feature 101 — Approval notifications: LARGE project name of the requester on every surface

<goal>
Approval/question notifications must make the REQUESTING PROJECT unmissable: when agents from Overmind, Mindlash, BobHouse, Squeezr all ask for approvals, the human must see at a glance WHICH project is asking — big, prominent project name on every approval surface, not a subtle metadata line.
</goal>

<requirements>
1. Every approval/question notification surface renders the project name prominently — large title-position text or a strong badge, visually dominant over the agent/feature context lines. Surfaces: bell rows (084/096), attention page cards (087), the global approval toast (093). When 095 lands, OS toasts follow the same rule.
2. The project name is resolved from the notification's ProjectId (data already present — no payload changes expected); unknown/deleted project degrades gracefully.
3. Feature/slug and agent lines demote to secondary context under the project name — hierarchy: WHO asks (project, big) → what (feature/preview, small).
4. Consistency across surfaces: one shared rendering primitive if the house layout allows it (FSD shared/widgets), not four ad-hoc stylings.
</requirements>

<non-goals>
- Payload/API changes (ProjectId already travels).
- Per-project color coding (candidate at spec time, not committed).
- Filtering/grouping the notification center by project (separate concern if wanted).
</non-goals>

<refs>
- 084-notification-human-actions (bell actions), 087-attention-page, 093-approval-deep-link-toast (toast), 095-os-desktop-notifications (future OS surface).
- 020-agent-notifications (payload source).
</refs>