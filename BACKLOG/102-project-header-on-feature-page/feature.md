---
feature: 102
slug: project-header-on-feature-page
status: draft
priority: p1
version: 1
created: 2026-10-03
---

# Feature 102 — Project name always visible on feature page (above dashboard+entries), dashboard and entries

<goal>
Permanent project identity on the main content pages: the feature page, dashboard, and entries views always render the project name prominently at the top, above the content. Trigger case: a notification deep-links into a feature page and the human lands there with zero context about WHICH project they are looking at — the page itself must answer that before anything else.
</goal>

<requirements>
1. Feature page: project name rendered at the top, above the feature content (dashboard section + entries) — visible without scrolling, visually primary (page-level header or a strong breadcrumb segment).
2. Dashboard and entries views: same persistent project header treatment — one consistent pattern across the three surfaces, not three ad-hoc stylings.
3. Project name resolves from the page's projectId (already in every route/payload); unknown project degrades gracefully.
4. Shared rendering primitive if the house layout allows (FSD shared/widgets), so 101 (approval notifications prominence) and this feature stay visually consistent — both answers to the same "which project am I looking at" problem.
</requirements>

<non-goals>
- Changing the sidebar/workspace project context behavior.
- Global search or cross-project navigation changes.
- Per-project theming (candidate at spec time if ever).
</non-goals>

<refs>
- 101-approval-project-prominence (sibling: project prominence on approval surfaces — same UX problem, notification-side).
- 093-approval-deep-link-toast / 028 Issues Board deep-links (the "lands with zero context" trigger).
</refs>