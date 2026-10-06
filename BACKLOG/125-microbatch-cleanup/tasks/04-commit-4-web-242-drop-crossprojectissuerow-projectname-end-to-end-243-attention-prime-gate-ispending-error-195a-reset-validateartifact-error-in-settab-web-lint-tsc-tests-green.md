---
feature: 125
task: 04
slug: microbatch-cleanup
status: done
review: light
created: 2026-10-06
brief: Commit 4 (web): #242 drop CrossProjectIssueRow.projectName end-to-end, #243 attention prime gate isPending||error, #195a reset validateArtifact.error in setTab; web lint/tsc/tests green.
---

<goal>
Commit 4 of the wave (last): web batch — #242 drop the dead `projectName` client field end-to-end, #243 attention prime gate, #195(a) stale approveError reset.
</goal>

<scope>
- **#242:** drop `projectName` end to end — `src/Overmind.Host/Api/Contracts/IssueContracts.cs` (remove from `CrossProjectIssueRow`), `src/Overmind.Host/Api/CrossProjectIssuesApi.cs` (drop from the projection), `web/src/features/issues/types.ts` (and `web/src/features/issues/api.ts` if referenced). Confirm `web/src/pages/issues-page/ui/global-issue-row.tsx` never reads it (names resolve via `useProjectName`). Update the cross-project issues endpoint's wire-shape pins in tests/Overmind.Api.Tests.
- **#243:** `web/src/widgets/app-shell/lib/use-attention-surfaces.ts` — gate the prime on `isPending || error` so a failed first sync followed by a successful hidden refetch primes surfaces instead of leaving stale data. Add a test in web/tests following house patterns (logic test where the harness allows; the `readFileSync` source-pin precedent lives in approval-bubble.test.mts).
- **#195(a):** `web/src/features/planning/components/feature-workspace.tsx` — reset `validateArtifact.error` in `setTab` (~5 lines; ledger refs :276/:514) so a spec failure no longer lights the banner on the plan panel. Only (a).
- Commit: one focused commit; tombstones #242, #243, #195 (note: only (a) landed, (b) remains recorded) immediately after.
</scope>

<anti-scope>
- No backend changes beyond the #242 field drop (the Host-side batch already landed as Commit 2).
- No redesign of the attention hooks beyond the prime gate.
- #195(b) double-click race stays out.
</anti-scope>

<constraints>
- Strictly sequential: after T3; last commit of the wave.
- Web gates in `web/`: `npm run lint && npx tsc --noEmit` + web test suite; backend still builds (`dotnet build`).
- Tombstone mechanics as in T1–T3; the #195 tombstone text must say only (a) landed.
- No `git push`. English-only.
</constraints>

<acceptance-criteria>
- [x] #242: `projectName` gone from Host contract, projection and web types; wire pins updated; the global issue row keeps resolving names via `useProjectName`.
- [x] #243: prime gated on `isPending || error`; the failed-first-sync → hidden-refetch case is covered by a test.
- [x] #195(a): tab change resets `validateArtifact.error`; a spec failure no longer renders on the plan panel.
- [x] Web `npm run lint`, `npx tsc --noEmit` and tests green; `dotnet build` clean.
- [x] Ledger #242, #243, #195 tombstoned with this commit's hash ((a)-only note on #195).
</acceptance-criteria>

<refs>
- Spec: cdb6854b-01ac-410c-82fc-8f13e706cb7d — #242, #243, #195(a).
- Plan: e3cbeb50-ac62-4d36-8fc8-d4cbb379dd3d — Commit 4.
- Ledger: `/home/johan-boolman/.claude/projects/-mnt-Ubuntu-Dev-Projects-AI-Tools-Overmind/memory/overmind-microbatch-queue.md` — #242 (115/02 N1), #243 (095/01 N-остаток), #195 (122/02).
- Sources: `audits/review-122-02-web.md` (#195a); review-115-02 and delta-review-095 exist only via the ledger.
</refs>

<obstacles>
- #195 only (a) is in scope; (b) remains a recorded ledger item — do not "fix" it while touching the same file.
- Web test harness is heterogeneous: logic tests where feasible, else the source-pin `readFileSync` precedent (approval-bubble.test.mts) — both are house-legal.
- #242 residue by design: `IssueBoardRow.ProjectName` (Infrastructure) lost its last reader (the Host projection now skips it) but stays — the contract enumerates Host contract + projection + web types + pins only, and removing it would drag IssueService SQL into a web-lane commit. Candidate for the next dotnet microbatch.
- #195(a) reset is wrapped in an `!validateArtifact.isPending` guard so a tab switch mid-flight does not hide a pending mutation's state (would have widened the (b) race the task explicitly leaves out).
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan (Commit 4); review: light.
- 2026-10-06 [agent] — T4 coding round started
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, gates verified by reviewer (lint/tsc/243 tests/dotnet build). Infrastructure leftover (IssueBoardRow.ProjectName + the JOIN serving only it + 2 test asserts) filed to ledger #246. Commit 217e0bd.
</log>