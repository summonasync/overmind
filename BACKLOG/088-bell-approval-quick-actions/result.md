---
feature: 088
slug: bell-approval-quick-actions
status: draft
created: 2026-09-26
---

# Result 088 — Колокольчик: кнопки ✓/✗ на approval-нотификациях (аппрув/отказ одним кликом = 077-proof)

<outcome>
Approval-kind bell items carry ✓ Approve / ✗ Reject buttons sending canonical replies ("approved"/"rejected") in one click; the click is the 077 validation proof. Optimistic update per the 020/022 idiom (instant disable, clean rollback on error, onSettled reconciliation); one-reply semantics are per-item; question-kind keeps its free-text form byte-identical; info-kind has no reply surface. Reject carries a hover hint that it does not by itself block agent validation (server reads any reply — user decision W-2 stands until the internal copilot).

Known limitation (by user decision): rejecting does not revoke proof capability — the agent must read the reply text. Backend enforcement deferred to the copilot-era denial semantics.
</outcome>

<evidence>
- Web tests 16/16 (5 new approval-reply pins + 11 from 086), tsc clean, lint 0 errors, build green 10 routes (coder + reviewer independent runs).
- Review: audits/review-088-01.md — PASS-WITH-NITS, 0 blockers/warnings; the known-limitation comment judged a legitimate non-obvious-decision comment; the 077 limitation backend-verified (proof search never reads reply content).
- S-1 title hint applied by the orchestrator post-review (tsc + lint re-verified).
</evidence>

<deviations>
- Disable/rollback behavior is code-reviewed but DOM-unpinned (no RTL/jsdom without new packages) — hardening follow-up.
</deviations>

<follow-ups>
- Hardening #97: extract ApprovalActions/QuestionReplyForm from the grown NotificationRow (~183 lines).
- Hardening #98: vitest + RTL harness for optimistic-mutation pins (needs package vetting).
- 077 denial-semantics task eventually rewrites the reject story end-to-end (S-1 hint is the interim UX).
</follow-ups>

<refs>
- Task 3813daa3 (done); review 088-01; user request 2026-09-26 («зелёная галочка/красный крестик»); 020/022 idioms; 077 proof semantics.
</refs>