---
feature: 101
task: 02
slug: approval-project-prominence
status: done
review: light
created: 2026-10-03
depends_on: 01
brief: Bell rows + attention cards + toast: project name primary via the shared primitive
---

# Task 02 — Approval surfaces prominence (parallel with 03)

<goal>
Approval/question surfaces promote the project name to the primary position: bell rows, attention cards, global toast.
</goal>

<scope>
- Bell rows (084/096 components): project name large in the title position (ProjectNameBadge from 01), feature/slug + agent demoted to a secondary context line; unread styling unaffected.
- Attention page cards (087): same hierarchy — project prominent, rest secondary.
- Global approval toast (093): project name as the headline line of the toast.
- Regression: notification-href tests stay green; unread/replied states untouched; no behavior changes — visual hierarchy only.
- Per-surface presence tests: project name rendered in the primary position for each surface.
</scope>

<anti-scope>
No notification center grouping/filtering, no payload changes, no content-page work (task 03).
</anti-scope>

<constraints>
FSD, no new packages, English UI, no auto-commits; gates lint/tsc/build/test real numbers.
</constraints>

<acceptance-criteria>
- [x] lint + tsc + build + test green
- [x] Bell rows: project name primary, secondary context line
- [x] Attention cards: same hierarchy
- [x] Toast: project name headline
- [x] notification-href regressions green; unread states untouched
</acceptance-criteria>

<refs>
- Spec: 0f88a771 (A section).
- Task 01 (order 1) — the primitive.
</refs>

<obstacles>
- Size bump caveat for future consumers: the app's cn() is a plain join (no tailwind-merge), and shared Badge bakes text-xs into its base — passing plain text-sm from a consumer is a silent no-op (verified: .text-sm is emitted BEFORE .text-xs in the built CSS). Surfaces therefore use the Tailwind v4 important suffix (className="text-sm!"), verified present as .text-sm\!{...!important} in the built stylesheet. If Badge ever gets a size prop or cn gains twMerge, drop the bang.
- Surfaces consume projectId via a new pure helper notificationProjectId(item) in features/notifications (null/absent → ""), pinned by 2 node tests; the "" marker rides task 01's resolver fallback ("Unknown project"). Payload check: NotificationItem already carries projectId — no fetch added.
- Per-surface RENDER-presence tests are not pinnable in the house node --test culture (no DOM/RTL; components need QueryClientProvider). Pinned the mapping logic; JSX-position presence is static-review material (same caveat as task 01, applies to tasks 02/03 acceptance reads).
- BellPreviewRow (topbar bell modal one-liner) keeps the compact default badge size — hierarchy via first-position + info tone; the text-sm! bump would crowd the 50-row dense preview.
</obstacles>

<log>
- 2026-10-03 [agent] — dispatched to web-coder (same session — built the primitive it consumes)
- 2026-10-03 [agent] — implemented: ProjectNameBadge in title/headline position on all three surfaces — NotificationRow (badge primary, agent demoted to muted secondary next to the deep-link row; unread borders/mark-read/delete untouched), BellPreviewRow (badge first in the compact preview line), AwaitingRow attention card (badge primary, agent demoted to muted, kind icon kept), ApprovalBubble toast (badge as headline line, agent moved under it as muted secondary). New pure helper notificationProjectId + barrel export, 2 node tests; payload verified — NotificationItem already carries projectId, no fetch added. Found + fixed a cascade trap en route: plain text-sm was a silent no-op against Badge's baked text-xs (cn has no twMerge) — surfaces use text-sm! (verified !important in built CSS); full note in obstacles. Gates: lint 0 errors / 6 pre-existing warnings, tsc 0, tests 62/62 (60+2), build success; notification-href regressions green.
- 2026-10-03 [agent] — task 02 complete — 5/5 AC ticked, gates green (lint 0e/6 pre-existing w, tsc 0, tests 62/62, build ok), notification-href regressions pinned, unread/replied styling untouched; cascade caveat (text-sm!) + test-culture caveat in obstacles; ready for review. Task 03 surfaces (feature-page/dashboard/entries) untouched per scope split.
- 2026-10-03 [agent] — 2026-10-04 [agent] — reviewed by web-code-reviewer: PASS-WITH-NITS — all four surfaces badge-primary with agent demoted to muted secondary; regressions intact (href/unread/actions/deadTarget/dismiss verified by full files); text-sm! bang accepted with byte-offset verification of built CSS (condition to remove recorded in obstacles: Badge size prop or cn+twMerge); mapping verified end-to-end (Guid? → string|null → "" → resolver fallback, 2 tests); legacy null-projectId rows will show a loud "Unknown project" — spec-authorized, user should know visually; gates re-verified; report audits/review-101-02.md.
- 2026-10-03 [agent] — delta review PASS-WITH-NITS (audits/review-101-02.md): all four surfaces, bang verified at byte level, regressions intact; AC 5/5 (coder had ticked). Closing as done; legacy "Unknown project" visual noted to user; nits → microbatch.
</log>