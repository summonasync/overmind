---
feature: 084
task: 07
slug: notification-human-actions
status: done
review: hard
created: 2026-09-28
brief: Live walk on isolated host + docs + result artifact, hard review
---

# 084/T5 — Closeout: live walk + docs + result

<goal>
Prove the whole 084 loop end-to-end on an isolated host, document any new seam, record the result.
</goal>

<scope>
- Live walk (scratch HOME, dedicated port, kill by exact PID — never pkill -f): agent notify approval → bubble pops → Approve → notification read (084-pre invariant) → bubble clears; second approval → «ещё N» badge → approval-filtered center; quiet dismiss stays dismissed; nag re-pops per interval; mini collapse/re-expand; dead-target dim after deleting the target feature.
- Docs: CONTEXT.md/STRUCTURE.md only if an undocumented seam appeared (keep it short).
- Result artifact (draft), feature to the done gate after hard review.
</scope>

<anti-scope>
- 093/095 surfaces; any product code change beyond doc/result fixes found during the walk (findings go to tasks/microbatch).
</anti-scope>

<acceptance-criteria>
- [x] Live-walk evidence (steps + observed results) recorded in the result artifact.
- [x] Docs updated only where a real seam was added.
- [x] Result draft written; hard review APPROVED.
</acceptance-criteria>

<refs>
- Plan 084 closeout; isolated-host legend (Overmind memory: scratch HOME + Overmind__BindUrl, kill by PID).
</refs>

<log>
- 2026-09-28 [agent] — [agent] taken into work — T5 closeout: isolated-host live walk (API wave + user click-check for the browser bubble), docs, result
- 2026-09-28 [agent] — [agent] API wave done on isolated host 4893 (scratch HOME /tmp/overmind-084-t05, PID 86477, kill by PID): settings defaults quiet/10 confirmed on empty store; PUT nag/1 round-trip OK; invalid mode → 400; invalid minutes 0 → 400; partial PUT restores quiet/10. Smoke scene staged for the user's browser click-check: scratch project 4a15a132 (bubble-smoke) + feature 001-bubble-target + 3 unread notifications (approval live target, approval dead slug 099-vanished-feature, question for the bell-only rule). Curl-body gotcha logged: planningRequireHumanValidationDefault is a string enum ("off"), not bool.
- 2026-09-28 [agent] — [agent] done: hard review APPROVED-WITH-WARNINGS (audits/architect-review-2026-09-28-084.md), both warnings closed pre-gate (list-dimming descope documented in result+spec log; settings barrel export fixed), CRLF normalized, web gate re-run green (27/27, lint/tsc/build). All 3 AC ticked.
</log>
