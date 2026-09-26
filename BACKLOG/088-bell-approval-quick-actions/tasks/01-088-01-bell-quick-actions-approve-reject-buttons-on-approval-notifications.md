---
feature: 088
task: 01
slug: bell-approval-quick-actions
status: done
review: light
created: 2026-09-26
brief: ✓/✗ quick-action buttons on approval notifications; canonical replies; one-reply disable
---

# 088/01 — Bell quick actions: approve/reject buttons on approval notifications

<goal>
Spec-less quick route (single task): approval-kind items in the bell get ✓ (approve) / ✗ (reject) buttons sending canonical replies with one click — the click is the 077 proof.
</goal>

<scope>
- web notification slice (bell modal item): on kind=approval render two buttons — approve (green check) / reject (red cross); aria-labels.
- Click → existing reply mutation with canonical text "approved" / "rejected" (English). One-reply semantics: after either click both buttons disable, chosen state visible (existing reply display reused).
- Question-kind keeps the free-text input (asks, not requests).
- Optimistic update/rollback consistent with the slice's mutation patterns (020/022 idioms).
- Known limitation documented in the component context (no server denial semantics — 077 proof search reads any reply; backend unchanged by user decision).
</scope>

<anti-scope>
- Backend proof changes (077), attention page (087), OS notifications (084), backend changes at all.
</anti-scope>

<constraints>
- TypeScript strict; English-only; no comments; no new packages; a11y labels; npm run lint && npx tsc --noEmit && npm run build green.
</constraints>

<acceptance-criteria>
- [x] Approval item shows ✓/✗; click sends "approved"/"rejected" via the existing reply mutation (test)
- [x] After click both buttons disable and the state renders (test)
- [x] Question-kind item unchanged (free-text input) (test)
- [x] lint + tsc + build green
</acceptance-criteria>

<refs>
- Feature body 088 (user request: «зелёная галочка/красный крестик — аппрув, нет»); 020 bell patterns; 077 proof semantics.
</refs>

<obstacles />

<log />

<log>
- 2026-09-26 [agent] — [orchestrator] parked and re-queued: dispatched in parallel with 086/01 but both tasks live in the notification slice (file-set conflict) — agent stopped before edits, task returns to todo; re-dispatch right after 086/01 lands
- 2026-09-26 [agent] — [agent] 088/01 picked up: bell quick actions on approval items; surface = notification slice components only (086/02 in parallel on different files)
- 2026-09-26 [agent] — [agent] Implemented: approval rows render Approve (green Check, secondary variant) / Reject (red X, danger variant) with aria-labels "Approve/Reject request from {agent}"; click → existing reply mutation with canonical "approved"/"rejected" from APPROVAL_REPLY (web/src/features/notifications/lib/approval-reply.ts, exported via slice index). Reply mutation now optimistic per 020/022 idiom (cancelQueries → snapshot → setQueryData patch of replyText/repliedAt → onError rollback → onSettled invalidate) — both buttons disable instantly (replied || replyingThis) and chosen state renders colored by approvalChoice (green approved / red rejected / muted legacy text). Question-kind keeps the free-text form untouched; info unchanged. Known limitation documented as an in-component comment (no server denial semantics; 077 proof search reads any reply). Tests: tests/approval-reply.test.ts (5 node --test cases — canonical payloads + choice mapping). Gates in web/: npm run lint 0 errors / 6 pre-existing warnings; npx tsc --noEmit clean; npm test 16/16 pass; npm run build green (10 static routes). Note: empty <obstacles> section not patchable — non-blocking notes live here: (1) AC "(test)" evidence is pure-module coverage; DOM click-through needs RTL/jsdom, excluded by no-new-packages constraint; (2) node --test MODULE_TYPELESS_PACKAGE_JSON warning is pre-existing harness behavior. Files touched (notification slice only, no 086/02 collision): features/notifications/{index.ts, lib/use-notification-mutations.ts, lib/approval-reply.ts}, widgets/app-shell/ui/notification-row.tsx, tests/approval-reply.test.ts.
- 2026-09-26 [agent] — [agent] Done with evidence: 4/4 AC ticked; gates green in web/ — lint 0 errors (6 pre-existing warnings), tsc --noEmit clean, npm test 16/16 (5 new approval-reply cases), npm run build ok. Surface = notification slice only; parallel 086/02 files untouched.
</log>
