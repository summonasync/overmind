---
feature: 028
task: 04
slug: cross-project-bug-intake
status: done
review: light
created: 2026-10-02
depends_on: 02
brief: Issues Board tab: list with filters, detail with append trail, triage/close/promote human door
---

# Task 04 — Web Issues Board (parallel with 03)

<goal>
The dedicated Issues Board tab: list + detail, human door for the owner team.
</goal>

<scope>
- Left-menu entry «Issues Board» next to Planning; route + page per FSD (features/issues or pages/issues per house layout).
- List: title, status chip, author-project, age (formatBacklogAge), append count; filters by status and author-project; React Query hooks per house rules (no useEffect fetching).
- Detail: full card, append trail (author-project, date, text), actions: triage, close (with reason), promote (feature picker → promote call) — pending-disable + ErrorText per the 099 patterns.
- Mutations invalidate the issues queries per the 096 idiom.
- Old links none — new surface, no migrations of existing UI.
- Gates: npm run lint && npx tsc --noEmit && npm run build && npm test — all green, real numbers.
</scope>

<anti-scope>
No kanban/drag, no backend changes (task 02 ships the endpoints), no MCP.
</anti-scope>

<constraints>
FSD, RSC-first ('use client' only where interactivity demands), English UI text, no comments by default, no new npm packages, no auto-commits.
</constraints>

<acceptance-criteria>
- [x] lint + tsc + build + test all green
- [x] Tab renders from the left menu; list with both filters works
- [x] Detail shows the append trail; triage/close/promote actions work against a running host
- [x] Promote uses the feature picker and surfaces the result
- [x] No cross-feature imports violating FSD; no new packages
</acceptance-criteria>

<refs>
- Spec: 1fd1454c (HTTP + Web section).
- Task 02 (order 2) — HTTP endpoints this UI consumes.
</refs>

<obstacles></obstacles>

<log></log>

<log>
- 2026-10-03 [agent] — dispatched to web-coder (fresh session, parallel with task 03 — disjoint file sets), task 02 done; deep-link shape from 02's obstacles is binding
- 2026-10-03 [agent] — 2026-10-03 [agent] — reviewed by web-code-reviewer: FAIL — 1 BLOCKER (close-with-reason not sequential: settle created synchronously, status POST fires in parallel with append; append failure closes the issue with the reason lost — contradicts the declared append-then-status semantics; one-line fix, ready diff in report), 3 nits (hand-rolled status tones vs shared Badge, dead useIssueMutations instance on the page, filters not in URL → microbatch); promote-on-open alarm false (PROMOTE_SOURCES matches backend), deep-link contract exact, gates re-verified green; report audits/review-028-04-web.md. Fix loop dispatched.
- 2026-10-03 [agent] — 2026-10-03 [agent] — re-reviewed fix round 1 (028/04-web) by web-code-reviewer: PASS-WITH-NITS — blocker closed structurally (status POST only inside .then after successful append; old parallel settle would dirty-close on append failure, new path cannot), dead-instance removal equivalent, gates re-verified (lint 0/6, tsc 0, 52/52, build with /issues); nits #1 status-tone vs shared Badge + #3 filters-not-in-URL → microbatch; report audits/review-028-04-web.md.
- 2026-10-03 [agent] — delta review PASS-WITH-NITS (audits/review-028-04-web.md, fix round 1): sequential append-then-close + dead instance removed; AC 5/5; web gates green. Closing as done; nits transcribed to microbatch queue.
</log>
