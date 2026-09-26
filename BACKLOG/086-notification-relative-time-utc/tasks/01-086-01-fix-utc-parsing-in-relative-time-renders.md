---
feature: 086
task: 01
slug: notification-relative-time-utc
status: done
review: light
created: 2026-09-26
brief: Fix UTC-as-local relative time in the bell (and sweep other renders); pin sub-minute output
---

# 086/01 — Fix UTC parsing in relative-time renders

<goal>
Notification center renders a seconds-old notification as "3h ago" (user's UTC+3 vs UTC createdAt). Fix the parsing so the Z marker is respected, sweep the same defect across all relative-time renders in web/.
</goal>

<scope>
- Locate the notification-center relative-time formatter (web/src/features/notifications slice + shared time helpers).
- Fix: parse the ISO string as a real instant (Date constructor with the full ISO string, or a shared helper); never slice/naive-parse timestamps.
- Sweep: board "updated X ago", feature workspace age line (024 helper), any other relative render — same class, one shared fix if a helper exists, per-site if not.
- Pin: render test with a "now − 10s" UTC ISO timestamp asserting sub-minute output ("just now" or equivalent).
</scope>

<anti-scope>
- 024 age-formatting rules (days >24h, hours <24h); notification feature work (084); backend changes.
</anti-scope>

<constraints>
- TypeScript strict, English-only, no comments, no new packages; npm run lint && npx tsc --noEmit && npm run build green.
</constraints>

<acceptance-criteria>
- [x] Notification created 10s ago renders sub-minute text (pinned by test)
- [x] A UTC timestamp one local hour in the future renders as "just now"-class output, not negative/positive hour drift (pin)
- [x] lint + tsc + build green
</acceptance-criteria>

<refs>
- Feature body 086 (user report, live bell check during the 077 proof flow).
</refs>

<obstacles />

<log />

<log>
- 2026-09-26 [agent] — Picked up by next-js-coder sub-agent: locating relative-time formatters in web/ before the UTC-parsing fix.
- 2026-09-26 [agent] — Fix landed. Root cause: backend serializes Kind=Unspecified DateTime (Kind lost through the SQLite round-trip), so createdAt reaches the browser as a Z-less ISO string; Date.parse then reads it as LOCAL wall-clock — for a UTC+3 user a fresh notification drifts +3h (reproduced in node: 3.037h for the reported shape). Shared fix: new web/src/shared/lib/relative-time.ts exporting parseInstant (appends Z to Z-less time-bearing ISO strings, keeps explicit offsets and date-only forms untouched) plus formatRelativeTime / formatBacklogAge / formatNotificationTime promoted into it. Sweep result: notification bell (notification-row.tsx local copy deleted), board updated-ago (feature-board.tsx), feature workspace updated-ago (feature-workspace.tsx, artifact tab included), 024 backlog age line (feature-overview-panel.tsx via features/planning index re-export) — all four now share the one parse; features/planning/lib/relative-time.ts deleted. 024 age buckets (m/h/d) and the 30d locale branch preserved byte-for-byte. Pin tests: web/tests/relative-time.test.ts (8 cases, node:test, zero new packages, TZ pinned to Asia/Qatar inside the file; tsconfig gained allowImportingTsExtensions for the .ts-extension import). Gate: node --test 8 pass / 0 fail under TZ=Asia/Qatar; pin power proven — the old naive-parse algorithm fails exactly the 2 drift pins (now−10s and now+1h Z-less) under UTC+3; npm run lint 0 errors (6 pre-existing warnings, all in files untouched by this task); npx tsc --noEmit clean; npm run build green, 9 routes static-prerendered. Note for the microbatch queue: the serializer-side root cause remains — Host could emit Kind=Utc (or DateTimeOffset) so the Z marker travels on the wire; deliberately out of scope here (backend changes are anti-scope), the web parse now normalizes either shape. Changed files: web/src/shared/lib/relative-time.ts (new), web/src/features/planning/lib/relative-time.ts (deleted), web/src/features/planning/index.ts, web/src/features/planning/components/feature-board.tsx, web/src/features/planning/components/feature-workspace.tsx, web/src/widgets/app-shell/ui/notification-row.tsx, web/tsconfig.json, web/tests/relative-time.test.ts (new). No git commands run.
- 2026-09-26 [agent] — Done by next-js-coder: UTC-aware shared parseInstant + promoted formatters, 4 render sites re-pointed, 8-case node:test pin suite (old algorithm proven to fail the drift pins under UTC+3), lint 0 errors / tsc clean / static build green. Evidence in log.
- 2026-09-26 [agent] — Claim correction per review-086-01 S-2 (reviewer-verified): the earlier log line "the old naive-parse algorithm fails exactly the 2 drift pins" understates the exposure — 5 of the 8 pins in tests/relative-time.test.ts fail under the naive parse, not 2. The 2-failure count came from a scratch verification that exercised only the two formatNotificationTime drift assertions, not the full suite. Recorded as corrected; supersession note left here so the 086/01 record carries no stale figure.
</log>
