---
feature: 112
task: 01
slug: approval-bubble-roomier
status: done
review: light
created: 2026-10-04
brief: Web: 800-char bubble text + wider window + test + result (Simple path)
---

# Task 01 — Text bound + window + tests + result

<goal>
The approval bubble carries up to 800 characters and stops being a 4-line teaser.
</goal>

<scope>
- approval-bubble.tsx: 800-char slice with «…» on longer texts (pure helper in lib, house precedent); line-clamp-4 → none within the bound; max-width one Tailwind step up; mobile full-width behavior unchanged.
- Web test pins the slice (≤800 + ellipsis) and the clamp removal.
- Result artifact.
</scope>

<anti-scope>
- Server text cap; notification center; 095 surfaces.
</anti-scope>

<acceptance-criteria>
- [x] Slice ≤800 + ellipsis pinned; clamp removed within the bound — pinned.
- [x] Gates green (lint/tsc/node test); result drafted.
</acceptance-criteria>

<refs>
- Spec fbd50db4; plan 300036dc.
</refs>

<log>
- 2026-10-04 [agent] — taken into work by the autonomous pipeline
- 2026-10-04 [agent] — execution complete, awaiting orchestrator review dispatch. Implementation: pure helper bubbleText at widgets/app-shell/lib/bubble-text.ts (BUBBLE_TEXT_LIMIT=800, «…» appended past the bound); bubble text renders through it with line-clamp-4 → line-clamp-none, title keeps the full text. Width w-72 → sm:w-80 (one step up from sm, phones keep today's width — spec asked for both the bump and untouched mobile behavior). Notification center untouched. Tests: tests/bubble-text.test.mts (slice ≤800+ellipsis, short passthrough, exact-bound edge) + source pin in tests/approval-bubble.test.mts (no line-clamp-4, line-clamp-none present, bubbleText wired, sm:w-80). Gates green: eslint 0 errors, tsc clean, node --test 103/103. No obstacles section on this artifact — width interpretation recorded here. AC2 result-drafting left open on purpose: per plan Simple path the result artifact lands after review.
- 2026-10-04 [agent] — [orchestrator] Light review passed (PASS-WITH-NITS, 0 blockers — nits #179/#180 to microbatch ledger), result 45852d7d drafted, both AC ticked. Closing task; result stays draft per pipeline mandate.
</log>
