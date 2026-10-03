---
feature: 028
slug: cross-project-bug-intake
status: validated
created: 2026-10-02
validated: 2026-10-03
---

# Plan 028 — Cross-project bug intake: чужой код → чужая борда (bug-сущность + onboarding правило)

5 tasks. Chain 01→02, then 03 and 04 run in PARALLEL (disjoint file sets: Mcp vs web/), 05 closes. No per-task live dogfood — batch dogfood after release (user strategy).

## Key implementation decisions

- **New tools class, not the god file.** All six issues_* tools live in a new `src/Overmind.Mcp/IssuesTools.cs` — OvermindPlanningTools.cs (1199 lines, feature 100's exhibit A) does not grow by this wave.
- **Promotion is a service-level composition:** IssueService.Promote composes the task artifact through the existing planning artifact service (same path as a human-door task upsert), filling goal from title+body, scope from the body template sections, obstacles from the append trail, refs with the issue id — then flips the issue to promoted + artifact link in the same SaveChanges.
- **Notification on foreign create:** issues_create with authorProjectId != project posts an entry through the existing notification infrastructure (AgentNotification/020 + notification-href deep-link pattern) — details belong to task 02; the tool call itself stays thin.
- **Provenance validation everywhere:** authorProjectId must reference an existing project (404 otherwise) — that is the only "check"; lifecycle moves carry the 043-style source marker.

## Task chain

1. **01 — Core + Infrastructure.** Issue + IssueAppend entities; IssueService (create/append/status machine open→triaged→promoted→closed + promote composition); migration AddIssues; full unit matrix (forward chain, close from any, no reopen, promote only from open/triaged, append-only invariant, promotion composes the task and links both ways).
2. **02 — Host API + notification.** Both doors (agent + human) over IssueService; contracts; notification-center entry on foreign create with deep-link; tests.
3. **03 — MCP tools (parallel with 04).** IssuesTools.cs: issues_create/list/get/append/set_status/promote with protocol-first descriptions; registration; contract tests (cap-free but echo-shape per 037, description asserts).
4. **04 — Web Issues Board (parallel with 03).** Left-menu tab, list with status/author filters (title, status chip, author-project, age, append count), detail with append trail + triage/close/promote actions (feature picker), FSD, React Query per house rules, lint/tsc/build/test gates.
5. **05 — Closeout.** Onboarding quickstart foreign-project protocol paragraph; architect light over the wave; result artifact. NO live dogfood — batch dogfood after release per user strategy.

<log>
- 2026-10-03 [agent] — plan drafted after spec validation; 01→02→(03∥04)→05, new IssuesTools.cs instead of growing the god file, no per-task live dogfood per the user's batch strategy.
- 2026-10-03 [agent] — validated with human proof: notification 1df93602-db52-4c82-96ed-e81fce6c716d replied 2026-10-03 15:16:40 UTC
</log>