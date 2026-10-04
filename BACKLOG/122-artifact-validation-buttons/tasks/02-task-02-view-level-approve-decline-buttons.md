---
feature: 122
task: 02
slug: artifact-validation-buttons
status: done
review: light
created: 2026-10-04
depends_on: 01
brief: Web: Approve/Decline on artifact views (draft state)
---

# Task 02 — View-level Approve/Decline buttons

<goal>
Validation happens where the user reads: Approve and Decline on the spec/plan/result views.
</goal>

<scope>
- feature-workspace artifact views (draft state): Approve button re-uses the wired validate mutation (same as the editor button — no editor detour); Decline opens a small reason prompt (optional) and calls the task-01 route; both absent on validated (frozen) artifacts; Approve returns after an edit demotes to draft.
- Source pins + web tests per repo patterns.
</scope>

<anti-scope>
- Task views (own status flow); notification center; MCP.
</anti-scope>

<acceptance-criteria>
- [x] Draft views show Approve (works without the editor) and Decline (reason → log line); frozen views show neither; post-edit return pinned.
- [x] lint/tsc/test/build green.
</acceptance-criteria>

<refs>
- Spec 467ba128; plan 5a1e9c7b; feature-workspace.tsx:283-312; artifact-editor.tsx:123.
</refs>

<log>
- 2026-10-04 [agent] — task cut post-validation; waits on task 01 (decline route).
- 2026-10-04 [agent] — [agent] Light review passed (PASS-WITH-NITS, 0 blockers — report audits/review-122-02-web.md; low-nit stale approveError + 2 info → microbatch #195/#196). Gates confirmed by reviewer: lint 0e, tsc 0, 113/113, build ok. Code in 7c43e67. Both AC ticked; wave closing.
</log>