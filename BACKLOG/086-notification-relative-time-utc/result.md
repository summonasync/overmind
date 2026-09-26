---
feature: 086
slug: notification-relative-time-utc
status: draft
created: 2026-09-26
---

# Result 086 — Bug: relative time в колокольчике рендерит UTC как локальное — свежие нотификации «3h ago»

<outcome>
Bug fixed at the parsing layer and swept across the whole web surface: a shared parseInstant in web/src/shared/lib/relative-time.ts appends Z to Z-less time-bearing ISO strings (SQLite drops Kind=Utc on the backend), offsets and date-only strings untouched. Four target render sites (notification row, board updated-ago, workspace updated-ago incl. artifacts tab, overview age line via the planning barrel) plus six pre-existing naive sites (kits import labels, two frozenAt badges, three calendar dates via new formatCalendarDate) all parse through the helper with display formats preserved byte-for-byte. 024 bucket rules untouched.

Behavior note: fresh notifications now render "just now"-class output for any local timezone instead of a fake hours-age drift.
</outcome>

<evidence>
- Web test suite: 0 → 16 pins (8 + 3 + 5), npm test green via the new package.json test script; all date pins verified red under the naive parse (reviewer: 5 of the 8 originals + all 3 sweep pins, empirically).
- Gates: lint 0 errors (6 pre-existing warnings untouched), tsc clean, build green (10 static routes after 088's parallel landing).
- Reviews: audits/review-086-01.md, review-086-02.md — both PASS-WITH-NITS, all findings routed or fixed in-tree.
- Sweep audit: zero bare new Date(<string>) on API timestamps in web/src; documented exceptions (numeric epoch-ms, localeCompare sorts, client-side now).
</evidence>

<deviations>
- Root cause is backend serialization (SQLite loses Kind=Utc) — client-side parseInstant is the fix; the backend Z-stamping is microbatch #96 and will allow simplifying the helper later.
- MODULE_TYPELESS warnings (2 per test run) accepted and documented instead of a risky "type": "module" next to Next config loading; tests are explicit-ESM (.mts).
</deviations>

<follow-ups>
- Microbatch #96 (Host Z-stamping), #94/#95 and the standing queue.
- 084 (notification visibility/actions) and 087 (attention page) build on the fixed time rendering.
</follow-ups>

<refs>
- Tasks afa7da39 / 2d94150f (both done); reviews 086-01/02; user report 2026-09-26 (live bell check).
</refs>