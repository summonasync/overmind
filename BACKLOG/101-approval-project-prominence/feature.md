---
feature: 101
slug: approval-project-prominence
status: done
priority: p1
version: 1
created: 2026-10-03
---

# Feature 101 — Project prominence: LARGE project name on approval surfaces + always-visible header on feature page/dashboard/entries

<goal>
Project prominence everywhere — one UX problem, two surface families (merged with 102-project-header-on-feature-page 2026-10-04):
(A) Approval/question notifications must make the REQUESTING PROJECT unmissable — big, prominent project name on every approval surface (bell rows, attention cards, global toast, future OS toasts), visually dominant over agent/feature context lines. Multi-project reality: Overmind, Mindlash, BobHouse, Squeezr all ask — the human must see WHO asks at a glance.
(B) Content pages must always show which project you are in: the feature page renders the project name at the top above the dashboard section + entries; the dashboard and entries views carry the same persistent project header. Trigger case: a notification deep-links into a feature page and the human lands with zero context.
</goal>

<requirements>
1. (A) Approval surfaces render the project name prominently — large title-position text or strong badge: bell rows (084/096), attention page cards (087), global approval toast (093); 095 OS toasts follow when they land. Feature/slug + agent lines demote to secondary context.
2. (B) Feature page: project name at the top, above dashboard + entries, visible without scrolling, visually primary. Dashboard and entries views: same persistent header treatment. One consistent pattern across surfaces, not ad-hoc stylings.
3. Shared rendering primitive (FSD shared/widgets) powering both surface families — 101 and 102 were two answers to the same "which project am I looking at" question; the merged feature keeps one voice.
4. Project name resolves from the notification's / page's ProjectId (data already present — no payload changes expected); unknown/deleted project degrades gracefully.
</requirements>

<non-goals>
- Payload/API changes (ProjectId already travels).
- Per-project color coding (candidate at spec time, not committed).
- Notification center grouping/filtering by project (separate concern).
- Sidebar/workspace project-context behavior changes; global search; per-project theming.
</non-goals>

<refs>
- 102-project-header-on-feature-page — merged into this feature (tombstone-done 2026-10-04).
- 084-notification-human-actions, 087-attention-page, 093-approval-deep-link-toast, 095-os-desktop-notifications, 020-agent-notifications.
</refs>

<log>
- 2026-10-03 [agent] — user request in-session 2026-10-04: merged with 102-project-header-on-feature-page (user reordered the board to put both on top and ordered the merge) — one feature, two surface families: approval-surface prominence (former 101 scope) + persistent project header on feature page/dashboard/entries (former 102 scope); shared rendering primitive required. 102 goes tombstone-done per the 060/061→062 precedent.
- 2026-10-03 [auto] — artifact created → inProgress
- 2026-10-03 [auto] — all tasks done + spec + plan validated → review
- 2026-10-03 [agent] — done by user request in-session: result b8b7fa98 validated (notification proof 726d55e8 «approved» 2026-10-03T22:31, chat mandate «Поехали дальше»); 4/4 tasks done (3×PASS-WITH-NITS, 0 blockers), architect light APPROVED clean (0/0/4 informational); merged 101+102 per user order
</log>
