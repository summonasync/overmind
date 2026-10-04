---
feature: 115
slug: issues-board-navigation
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 115 — Issues Board: third project tab + cross-project sidebar view (new/unread first)

<goal>
Issues Board navigation matches where the work lives: a project's issues are a third tab on the project (dashboard / entries / issues), and the global sidebar Issues Board (without a project context) shows ALL issues across ALL projects instead of bouncing the user to project pages.
</goal>

<scope>
- R1. Project third tab: project detail gains an «issues» tab (existing ProjectTabs pattern, deep-link `?tab=issues` like the other tabs). Content: the existing per-project issues list (web/src/features/issues re-used, no new components if avoidable).
- R2. Global cross-project list: the sidebar Issues Board page (/issues, no project selected) renders every issue from every project. New backend endpoint: cross-project GET (aggregated list, project name + id per row, status filter). Sorting v1: OPEN issues first (open before triaged before promoted/closed), then newest-first by creation. **Challengeable:** the user asked for «новые/непрочитанные» — issues have no read-state; v1 treats «unread» as «not yet triaged/closed» via the sort above. A per-issue read marker is a v2 candidate, not built here.
- R3. Cross-project rows carry the project identity (101 ProjectNameBadge, deep link into that project's issues tab).
- R4. Backend: one read-only cross-project endpoint (IssuesService aggregate query + API + tests). MCP cross-project list — NON-GOAL v1 (agents work per-project; no consumer yet).
</scope>

<anti-scope>
- No issue read-state model (v2 candidate, challengeable above).
- No MCP changes; no promote/status flows touched; no issues board kanban (028 decision stands).
- No notification-center changes.
</anti-scope>

<constraints>
- Web follows FSD; the project tab re-uses features/issues surfaces (barrel imports only).
- The cross-project endpoint is read-only and cheap (single query with project join, no N+1).
</constraints>

<acceptance-criteria>
- [ ] Project detail has the issues tab, deep-linked (?tab=issues), listing that project's issues.
- [ ] /issues without a project renders the cross-project list: open-first sort, project badge per row, deep link into the project tab.
- [ ] Cross-project endpoint tested (aggregation, filters, sort); web gates green; result drafted.
</acceptance-criteria>

<refs>
- User intake 2026-10-04 (two messages); 028 (issues domain + per-project API); 101 (ProjectNameBadge); app-sidebar.tsx:44 (global entry).
</refs>

<log>
- 2026-10-04 [agent] — spec drafted; «unread» semantics marked challengeable (no read-state on issues; v1 = open-first sort).
- 2026-10-04 [agent] — validated with human proof: notification 807f7db6-d5b2-4288-8f00-291986dfab83 replied 2026-10-04 18:51:13 UTC
</log>