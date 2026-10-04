---
feature: 122
slug: artifact-validation-buttons
status: draft
created: 2026-10-04
---

# Plan 122 — Approve/Decline buttons on artifact views (human validation door)

<goal>
Small backend route first, web surface second.
</goal>

<sequence>
- 01 (dotnet coder): human-door decline route — POST artifact decline {reason?} → [ui] log line append (043 marker), 400s consistent with the family; tests.
- 02 (web coder): view-level Approve (reuses the wired validate mutation) + Decline (reason prompt → 01 route) on spec/plan/result views; frozen-state behavior; source pins + gates.
- Reviews: one light review per task, batched when both land. Result after.
</sequence>

<verification>
- Decline route: reason appends, empty reason appends bare line, task artifact → 400.
- Web: approve works without opening the editor; buttons absent on validated; return after edit-demote.
- Full web + dotnet gates green at closeout.
</verification>

<refs>
- Spec 467ba128; artifact-editor.tsx; feature-workspace.tsx; AgentPlanningApi.
</refs>

<log>
- 2026-10-04 [agent] — plan draft.
</log>