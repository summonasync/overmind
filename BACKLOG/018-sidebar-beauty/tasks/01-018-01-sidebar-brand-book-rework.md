---
feature: 018
task: 01
slug: sidebar-beauty
status: done
review: light
created: 2026-09-20
---

# 018/01 — Sidebar brand-book rework

<goal>
The left sidebar complies with web/brand-book.md: the Kerrigan art layer fades into the surface color at lower opacity instead of fighting the nav, the tree's spot backdrop disappears, and the four nav rows share one typography. All 011 behavior frozen.
</goal>

<context>
Feature 018, the only task. Verified facts: aside at widgets/app-shell/ui/app-sidebar.tsx:70 (solid bg-surface, z-30); Kerrigan layer :71-78 (opacity-80, mask fades to TRANSPARENT — violates the book's "fades into --background"); tree spot fix bg-black/45 backdrop-blur-sm at :125; non-Projects nav rows :133-149 (text-sm font-medium, active border-l-2 primary); Projects special-case :91-131; brand-book rules :62-64, :98-99, :121-135; tree at features/projects/components/project-tree.tsx (folders text-muted text-xs :282, projects text-primary :244-252, header 11px uppercase :118).
</context>

<scope>
- app-sidebar.tsx: Kerrigan layer → opacity ~0.35, mask-image linear-gradient fading to the SURFACE color (#14141c — prefer referencing the CSS variable over a hardcoded hex) over ~30%→98% width; keep the 7-variant pool, rAF pick, pointer-events-none, bottom-anchor sizing.
- Remove the tree container's bg-black/45 backdrop-blur-sm (:125) — content sits on the solid aside surface now.
- Unify the Projects row typography with the other three (text-sm font-medium; active border-l-2 primary bg-primary/10 text-primary; chevron + tree keep their affordances and aria).
- Section spacing/rhythm pass per brand-book.md:98-99 (sidebar layout rule) — conservative: consistent vertical rhythm, no structural moves.
- project-tree.tsx: align classes only where the hierarchy demands (nothing structural).
- Verify: npm run lint && npx tsc --noEmit && npm run build.
</scope>

<anti-scope>
- No NAV_ITEMS changes, no mobile-nav changes, no persistence key changes, no register-modal changes, no new tokens or palette changes, no logo/quote redesign.
</anti-scope>

<constraints>
- TypeScript strict; English-only; contrast floor: nothing dimmer than text-muted on surface.
- Pure visual — do not touch behavior or state handling.
</constraints>

<acceptance-criteria>
- [ ] Kerrigan layer: lower opacity, mask fades into surface color (not transparent); no text sits on raw art.
- [ ] Tree renders directly on the sidebar surface (no black box).
- [ ] All four nav rows share typography; active states consistent.
- [ ] 011 behaviors intact: NAV_ITEMS ×4, tree collapse persistence, register modal, aria-current/expanded, focus rings.
- [ ] lint + tsc + build green.
</acceptance-criteria>

<refs>
- Spec: feature 018, spec artifact (validated 2026-09-20) — every decision cites brand-book.md.
- Pattern refs: app-sidebar.tsx:70-78, :91-149, :152-154; project-tree.tsx:118, :238-299; globals.css @theme; brand-book.md:56-67, :90-99, :121-135.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — next-js-coder subagent (clean tree after the 019 commit).
- 2026-09-20 — Coder done: Kerrigan layer opacity-35 + luminance mask (white 30% → var(--color-surface) 98%, mask-mode:luminance — an alpha mask between two opaque colors would render a solid rectangle, no fade); tree spot-backdrop removed with its orphaned rounded-md; Projects row rhythm aligned (gap-1 pr-3 container, gap-2.5 rounded-md anchor) keeping all aria/affordances; project-tree untouched (hierarchy already per spec); 011 behavior frozen. lint 0 / tsc 0 / build green. To review.
- 2026-09-20 — Review PASS (audits/review-018-01.md): 0 blockers, 3 INFO. Luminance-mask deviation verified independently (alpha between two opaque stops = solid rectangle — coder's reasoning exact); built CSS confirms mask-mode + un-inlined var(--color-surface); 011 frozen byte-for-byte (className-only diff); geometry: nav never touches art at rest. INFO: kerrigan-3/7 can locally dip muted text to ~2.6:1 in the quote zone (cheap fix: white stop 12% — microbatch/visual call); Chrome<120 edge (evergreen assumption ok); brand-book's 4.8:1 muted claim doesn't reproduce (~3.8:1 — doc fix candidate). Done.
</log>
