---
feature: 101
slug: approval-project-prominence
status: validated
created: 2026-10-03
validated: 2026-10-03
---

# Spec 101 — Project prominence: LARGE project name on approval surfaces + always-visible header on feature page/dashboard/entries

## Goal

One UX problem answered everywhere: **which project am I looking at / who is asking**. Multi-project reality (Overmind, Mindlash Studio, BobHouse, Squeezr) floods the human with approvals and deep-links; today the project is a subtle metadata line at best. This feature makes the project name **large and unmissable** on (A) every approval/question surface and (B) always visible at the top of the main content pages. Web-only feature — zero backend changes, zero migrations.

## Decisions (user-approved in chat, 2026-10-04)

- **Merged feature.** 102-project-header-on-feature-page merged into 101 (both were unstarted drafts; 102 tombstone-done per the 060/061→062 precedent). One shared rendering primitive powers both surface families.
- **Hierarchy rule everywhere:** WHO asks / WHERE you are (project — large, primary) → what (feature title / preview / entry context — small, secondary).
- **Data is already there:** every notification and every content-page route carries ProjectId; the name resolves client-side from the projects the app already loads (or a tiny shared resolver hook). No payload/API changes.

## (A) Approval/question surfaces

- Bell rows (084/096): project name in the title position, large; feature/slug + agent demoted to a secondary context line; unread state unaffected.
- Attention page cards (087): same hierarchy, cards grouped or labeled by project — no new grouping semantics, just visual prominence.
- Global approval toast (093): project name as the toast's headline line.
- 095 (OS toasts) follows the same rule when it lands — out of scope here, consistency note only.
- Unknown/deleted project: graceful fallback (id-shown or generic label), never a crash.

## (B) Content pages

- Feature page: project name at the top, above the dashboard section + entries — visible without scrolling, visually primary (page-level header or strong breadcrumb segment).
- Dashboard view and entries view: the same persistent project header.
- Issues Board detail (?project=...) is NOT in scope (its filter already shows the project) — noted for consistency follow-up if wanted.

## Shared primitive

- One component/hook (FSD shared or widgets layer): takes projectId → renders the prominent project name; both surface families consume it. The primitive owns resolution (projects the app already has / resolver hook) and fallback behavior.

## Testing

- Web tests: primitive renders name from a known projectId, fallback for unknown; bell row / attention card / toast / feature page / dashboard / entries each assert the prominent hierarchy (at minimum: project name present in the primary position); notification-href regressions untouched.

## Non-goals

- Payload/API changes; per-project color coding; notification center grouping/filtering by project; sidebar/workspace context behavior changes; global search; per-project theming; Issues Board header.

<log>
- 2026-10-04 [agent] — spec drafted from the merged 101+102 scopes (user-approved merge); web-only, shared primitive, hierarchy rule, six surfaces + fallback.
- 2026-10-03 [agent] — validated with human proof: notification bf821977-66a1-49dc-8c45-85155d477964 replied 2026-10-03 21:14:52 UTC
</log>