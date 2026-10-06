---
feature: 093
task: 03
slug: approval-deep-link-toast
status: done
review: light
created: 2026-09-28
depends_on: 01
brief: Web: question kind in bubble + (N) title counter + live e2e + architect light + result draft
---

# 093/03 — Web: question kind in the bubble, (N) title counter, closeout

<goal>
An open Overmind surfaces questions alongside approvals in the existing zerling bubble (no Approve/Reject on question rows), and the browser tab shows an (N) counter while anything awaits. Then the live e2e walk, light architect review, and the result artifact close the feature.
</goal>

<context>
Spec 3748e264 (validated), plan 98cb3e9a — task 3 of 3, AFTER 01+02 (its e2e validates the whole chain). Current surfaces: web/src/features/notifications/lib/approval-reply.ts (isAwaitingApproval filter + APPROVAL_REPLY), widgets/app-shell/lib/use-approval-bubble.ts (awaiting candidates, quiet/nag discipline via settings), widgets/app-shell/ui/approval-bubble.tsx (Approve/Reject/Open rows, moreCount, deadTarget, mounted in app-shell on every page). LIVE_SYNC_INTERVAL_MS = 15s poll already global. Static export: browser-API effects only, no next/link.
</context>

<scope>
- approval-reply.ts — awaiting filter widens to kind ∈ {approval, question}: unread, no reply yet. Keep the existing export shape (add, don't break isAwaitingApproval users if any beyond the bubble).
- use-approval-bubble.ts / approval-bubble.tsx — bubble runs on the widened awaiting set; question rows render text + Open (notificationHref) without Approve/Reject; approval rows unchanged. The deadTarget dimming and dismiss/expand discipline apply to both kinds; the settings quiet/nag mode governs both. "N more → open approvals" button: for a mixed set open the center without an approval pre-filter (initialKind reconsideration).
- Title counter: while the awaiting set is non-empty, document.title gets an "(N) " prefix; restore the original title when it empties. Browser-API effect, SSR-safe (static export), lives in the bubble or app-shell per FSD layering.
- Web tests: unit pins for the widened filter and the title formatter (extract a pure formatter so it is testable); existing vitest suite stays green.
- Verify: npm run lint && npx tsc --noEmit && npm run build in web/.
- Live e2e (orchestrator-level, isolated dev host per the 023/096 trick — scratch cwd, Overmind__BindUrl, kill by exact PID): POST agent notification (approval + featureSlug + tab) → response url pastes to the feature tab; question notification → appears in awaiting set (API-level: list shows unread question); UI-level evidence limited to build/deploy honesty — title/bubble clicks are the user's click-check, note that in the result.
- architect-review light pass over the full 093 delta (audits/architect-review-2026-09-28-093.md), findings into <obstacles>/fix-round.
- Result artifact (kind=result): outcome, deviations, the mortal-deep-link note (url targets features, not notification rows), follow-ups (poll cadence, SSE tombstone).
- Closeout: result → draft; do NOT validate or flip the feature — orchestrator gates those.
</scope>

<anti-scope>
- Host/MCP code (tasks 01/02), new toaster system, SSE, poll cadence changes, notification center redesign, OS notifications (095).
</anti-scope>

<constraints>
- FSD: features/notifications owns data logic; widgets/app-shell owns presentation; imports via barrels only. React Query v5, TypeScript strict, no comments, English-only. 084/088/096 invariants hold: reply⇒read, proof rows survive clear-read, cleanup buttons untouched.
</constraints>

<acceptance-criteria>
- [x] Question notifications join the bubble awaiting set; question rows render Open without Approve/Reject.
- [x] (N) title counter appears while awaiting non-empty, restores on empty; unit-tested formatter.
- [x] lint + tsc + build green; existing vitest suite green.
- [x] Live e2e evidence captured (url in create response lands on the feature tab; question reaches the awaiting set).
- [x] architect light review logged (audits/architect-review-2026-09-28-093.md); findings resolved or carried.
- [x] Result artifact written (draft); ACs ticked via planning_patch_artifact; log lines via planning_append_log.
</acceptance-criteria>

<refs>
- Spec 3748e264 (validated); plan 98cb3e9a; tasks 01+02 (url field shape).
- 084 bubble files (context section); APPROVAL_REPLY; notification-href.ts.
- 096 fallout: bubble actions on deleted rows 404 gracefully (ErrorText path) — do not regress.
</refs>

<obstacles>
- Task/scope text said "vitest suite" — the web repo has no vitest; tests run on node's built-in runner (npm test = node --test "tests/**/*.test.mts", tests/*.test.mts). Followed the repo pattern, suite green 36/36.
- Widened filter is intentionally NOT a superset of isAwaitingApproval: the spec adds the unread check (title drops on mark-all-read) while the bell filter stays read-tolerant — both pinned in tests to keep 088 bell behavior frozen.
- More-button label made conditional ("open approvals" / "open all") to match the centerKind decision; scope only asked for initialKind but a fixed "open approvals" label would lie on a mixed set.
- Title counts the pre-TTL-gate awaiting set: bubble discipline (14d gate, dismiss, nag) hides rows but unread+unreplied items still deserve the tab hint; title and bubble can therefore diverge only for items older than 14 days.
</obstacles>

<log>
- 2026-09-28 [agent] — task sliced from validated plan; runs after 01+02.
- 2026-09-28 [agent] — coder picked up web delta of task 03
- 2026-09-28 [agent] — web delta done: isAwaitingResponse added in approval-reply.ts (kind in {approval,question} + unread + unreplied; isAwaitingApproval left untouched for the bell); bubble + title counter run on the widened set; question rows render text + Open without Approve/Reject; mixed set opens the center unfiltered (centerKind=all when any awaiting question, else approval — 084 focus preserved) and the more-button label follows (open all / open approvals). Pure formatter awaitingTitle(baseTitle,count) in widgets/app-shell/lib/awaiting-title.ts + useAwaitingTitle browser effect (SSR-safe, restores exact pre-effect title on empty/unmount). Gates: lint 0 errors (5 pre-existing warnings in untouched files), tsc --noEmit clean, npm run build static export ok, npm test 36/36 (27 existing + 9 new pins: 6 filter, 3 title). Runner is node --test (tests/*.test.mts), not vitest — task text said vitest, repo reality is node:test.
- 2026-09-28 [agent] — web delta done, e2e/architect/result stay with orchestrator
- 2026-09-28 [agent] — [orchestrator] web-дельта прошла light review PASS-WITH-NITS (audits/review-093-03.md): 4 находки — 1 SUGGESTION (question-строка без центр-аффорданса при no-target, микробатч #132) + 3 NIT (#133 ghost-counter TTL, #134 stale base title, лог-неточность по warning-счётчику). Гейты ревьюером перепроверены: lint 0 errors / 6 pre-existing warnings, tsc чист, build ок, 36/36. Дальше orchestrator: live e2e → architect light → result.
- 2026-09-28 [agent] — [orchestrator] live e2e зелёный на изолированном хосте (HOME=/tmp/overmind-093-walk, порт 4895, kill по PID 1074424, прод 4761 не тронут): (1) approval без projectId → url=http://overmind.localhost:4895/dashboard?feature=…&tab=spec (FromLoopback, без project-параметра); (2) approval с projectId → 3 параметра в порядке project→feature→tab; (3) question без featureSlug → url-поля НЕТ в сыром JSON (JsonIgnore); (4) GET диплинка → 200 text/html, в т.ч. с Host: overmind.localhost:4895; (5) reply⇒read живой (ReadAt выставлен, unread 3→2); (6) UI-слой (бабла/title) headless не проверяется — юзер-клик-чек, отмечено в result.
- 2026-09-28 [agent] — [orchestrator] architect light APPROVED (audits/architect-review-2026-09-28-093.md, 2 SUGGESTION → микробатч #135/#136) + result artifact b3a499ef написан (draft). Таск 03 полностью закрыт по AC.
- 2026-09-28 [agent] — web-дельта review PASS-WITH-NITS, live e2e зелёный, architect APPROVED, result b3a499ef написан — AC 5/5, таск done
- 2026-09-28 [agent] — [orchestrator] result artifact b3a499ef написан (draft, 4.1k симв): outcome по трём таскам, верификация 1644/1644 + live e2e 6 пунктов, девиации (port-0 квирк, node:test vs vitest), follow-ups. AC закрыт.
</log>