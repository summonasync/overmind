---
feature: 028
task: 05
slug: cross-project-bug-intake
status: done
review: light
created: 2026-10-02
depends_on: 03, 04
brief: Quickstart protocol paragraph, architect light (orchestrator dispatch), result with post-release batch-dogfood checklist
---

# Task 05 — Closeout: onboarding protocol + architect + result

<goal>
Wave closeout per the user's batch strategy: NO live dogfood — the batch dogfood happens after release. Docs, architect light, result.
</goal>

<scope>
- Onboarding quickstart: foreign-project protocol paragraph («need foreign code → foreign board: list_projects → issues_create on their project; do not touch their repo; follow-ups = issues_append; the owner team triages/promotes»); tool descriptions already carry it (task 03) — verify consistency, don't duplicate wording.
- Architect light over the whole wave (orchestrator dispatches separately — same split as 099).
- Result artifact: outcome per task, deviations, verification evidence (suite numbers), follow-ups (batch dogfood checklist for post-release: create/append/take-promote through real MCP from a second project; Issues Board click-check).
- Board work: logs, obstacles, AC ticks, status review.
</scope>

<anti-scope>
No live dogfood in this task (batch after release per user strategy). No product code beyond what the architect forces.
</anti-scope>

<constraints>
English-only, no auto-commits; quickstart text stays tight (loaded into every session).
</constraints>

<acceptance-criteria>
- [x] Quickstart carries the foreign-project protocol; tool descriptions and quickstart consistent
- [x] Architect light done (orchestrator dispatch), blockers fixed or waived with rationale
- [x] Full dotnet suite + web gates green on the final tree
- [x] Result artifact drafted with the post-release batch-dogfood checklist
</acceptance-criteria>

<refs>
- Spec: 1fd1454c (Onboarding protocol + Non-goals).
- Tasks 1–4 — the code this closeout verifies.
</refs>

<obstacles>
- Quickstart consistency: the foreign-project paragraph mirrors the issues_create description's protocol one-to-one (list_projects → issues_create on their project; do not touch their repo; follow-ups = issues_append; owner team triages/promotes) plus the automatic owner notification mention — one voice, no duplicated long wording (quickstart carries one paragraph; the long semantic detail stays in the tool description).
- Web gates on the final tree were run by task 04 (parallel coder lane): lint 0 errors, tsc clean, build clean with the /issues route, tests 52-52 — verified from the task 04 closeout log; the closeout round touched only OnboardQuickstart.cs (host text), so no web re-run was needed.
- Parallel sln test runs flake on this machine (TimeoutException on random Api tests under 4-host load — same phenomenon recorded in waves 099); final numbers from a sequential per-project run.
</obstacles>

<log>
- 2026-10-03 [agent] — dispatched to dotnet-coder (same session): quickstart protocol paragraph + result draft; architect light dispatched by the orchestrator separately (same split as 099); NO live dogfood — batch dogfood after release per user strategy
- 2026-10-03 [agent] — Closeout executed by dotnet-coder: OnboardQuickstart gained the foreign-project protocol paragraph (same voice as the issues_create description — no duplicated long wording; tool descriptions verified consistent from task 03). Full gates green on the final tree: dotnet build 0/0, sequential full suite 1855 passed / 0 failed (Core 274, Infra 565, Api 581, Mcp 435); web gates verified from task 04's closeout log (lint 0 errors, tsc clean, build clean, tests 52-52). Result artifact c909e954 drafted with the POST-RELEASE BATCH DOGFOOD CHECKLIST (5 scenarios) and the microbatch follow-up list. AC3 (architect light) intentionally open — orchestrator dispatch.
- 2026-10-03 [agent] — task 05 closeout executed to review by dotnet-coder: quickstart carries the foreign-project protocol (consistent with the task-03 tool descriptions), full gates green on the final tree (dotnet build 0/0, sequential suite 1855 passed / 0 failed — Core 274, Infra 565, Api 581, Mcp 435; web gates from task 04's closeout log), result artifact c909e954 drafted with the post-release batch-dogfood checklist and microbatch follow-ups. AC3 (architect light) intentionally open — orchestrator dispatch.
- 2026-10-03 [agent] — Architect 028/05 micro round executed by dotnet-coder: W1 — promotion is lock-aware (IssueService injects FeatureClaimService; PromoteAsync calls EnsureHolderAsync for the target feature BEFORE the transaction; foreign claim in any liveness → 409 FeatureClaimHeld naming the holder with its state — promote is not take-over; own-session/claim-free proceed; sessionId flows: agent door header / human door null / MCP tools inherit via the stamped adapter session — verified the ById and project-scoped agent handlers both read the header). W2 — both doors' GET wrapped in try/catch, unknown issue id now 404 on both doors (was a naked 500); pinned by UnknownIssueId_Returns404OnBothDoors (six legs: get/status/append/promote across both doors). N5 — human-claim aging appended to the result follow-ups (c909e954 v2). Gates: dotnet build --no-incremental 0 errors / 0 warnings; per-project sequential full run 1857 passed / 0 failed (Core 274, Infra 569, Api 582, Mcp 435).
- 2026-10-03 [agent] — 2026-10-03 [agent] — re-reviewed 028/05 fix round (W1+W2) by dotnet-code-reviewer: PASS — claim-aware promotion verified end-to-end (guard before tx, nothing-written pinned, session flows on all caller shapes, ended-claim refusal pinned), W2 six-leg 404s pinned, dependency graph clean; build 0/0, 16/16 + 11/11 re-run; report audits/review-028-05-closeout.md.
- 2026-10-03 [agent] — architect light APPROVED-WITH-WARNINGS closed (W1 promote-lock via user decision + W2 404s, delta review PASS); AC all ticked; final tree 1857/1857 + web green. Closing as done — wave 028 code-complete.
</log>