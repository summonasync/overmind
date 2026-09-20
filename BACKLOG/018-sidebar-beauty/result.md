---
feature: 018
slug: sidebar-beauty
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 018 — Sidebar beauty rework (left menu design)

<outcome>
018-sidebar-beauty shipped as a brand-book compliance pass, not a redesign: the Kerrigan art layer dropped to opacity 35 with a luminance-mode mask fading into the surface token (white 30% → var(--color-surface) 98% — an alpha mask between two opaque colors would render a hard rectangle, so mask-mode:luminance was the only correct implementation), the tree's spot bg-black/45 backdrop-blur crutch is gone (content sits on the solid aside surface), and the Projects row's rhythm matches the other three nav rows (gap/padding/radius, focus-ring geometry aligned). Typography hierarchy was already per the book — no structural changes needed anywhere; 011 behavior frozen byte-for-byte (className-only diff).
</outcome>

<evidence>
- Review: audits/review-018-01.md — PASS, 0 blockers. The mask deviation verified independently three ways: mask compositing math, the built CSS artifact (mask-mode present, var un-inlined — token sync end-to-end), and brightness measurements of the actual art crops (contrast over the quote zone improved ~2-3x vs the old opacity-80).
- Geometry: the four nav rows end at ~250px; art starts at 32% viewport height — nav never touches art at rest.
- tsc 0 / lint 0 errors / build green; only app-sidebar.tsx changed (4 class-level hunks).
</evidence>

<deferred>
- Microbatch: white mask stop 30% → 12% if kerrigan-3/7 brightness ever bothers (local ~2.6:1 dip in the quote zone); brand-book's 4.8:1 muted contrast claim doesn't reproduce (~3.8:1) — doc line fix on the next token touch.
- Visual pass on 0.1.8 pending (pure-visual change — the user is the final gate, same posture as the session's features).
</deferred>
