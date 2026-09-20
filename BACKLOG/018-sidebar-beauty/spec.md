---
feature: 018
slug: sidebar-beauty
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Spec 018 — Sidebar beauty rework (left menu design)

<decisions>
- The fix is systemic, per the project's own brand-book (web/brand-book.md): "Do keep content panels solid — art lives behind the shell, not under text" and the Kerrigan mask "fades into --background". The current implementation violates both — the strip mask fades to transparent and only the tree carries a spot backdrop (bg-black/45). The rework brings the sidebar INTO the book: the aside stays a solid surface panel; the Kerrigan art becomes a properly masked, lower-opacity decorative layer fading into the surface color; the spot bg-black/45 backdrop-blur on the tree is REMOVED (no longer needed once art sits correctly); nav labels render directly on the solid surface at full contrast (decision delegated by the user 2026-09-20).
- Kerrigan layer: keep the 7-variant pool and the random rAF pick (delight stays); opacity 0.80 → ~0.35; mask-image linear-gradient fades to the SURFACE color (#14141c) from ~30% to 98% width (the book's spec), height stays ~68% bottom-anchored; pointer-events-none unchanged.
- Typography/hierarchy unified per the book's scale: logo block (wordmark tracking, tagline) → nav section labels (all four items one style: text-sm font-medium, active state border-l-2 primary; the Projects special-case styling aligns with the others — only its chevron/tree affordance stays special) → tree (text-xs, depth padding, muted folders, primary project nodes) → bottom quote (text-xs muted, border-l primary/40). Nothing dimmer than --muted (the 4.8:1 floor on surface).
- 011 behavior is frozen: exactly Projects/Kits/Search/Settings in NAV_ITEMS (mobile-nav stays in sync), tree collapse persistence (overmind.tree.projectsOpen, expandedPaths, lastProjectId), the register modal, aria-current/aria-expanded, focus-visible rings.
- The quote stays the rotating 3-quote block, now guaranteed legible on solid surface. No new tokens, no palette changes — this feature only corrects the application of the existing system.
</decisions>

<open-questions>
- none (user delegated autonomous decisions 2026-09-20; all decisions cite brand-book.md sections)
</open-questions>

<requirements>
Web only (widgets/app-shell + features/projects tree):
- app-sidebar.tsx: Kerrigan layer rework (opacity/mask-to-surface); remove the tree's spot bg-black/45 backdrop-blur-sm; unify the four nav item styles (Projects row matches Kits/Search/Settings typography; chevron + tree keep their affordances); section spacing/rhythm per the book's sidebar layout rule; quote block unchanged except it now sits on solid surface.
- project-tree.tsx: no structural changes — only class alignment if the label hierarchy demands it (folders muted xs, projects primary xs, headers 11px uppercase).
- mobile-nav.tsx: untouched except staying in sync if NAV_ITEMS ever changes (it does not here).
- Verify: npm run lint && npx tsc --noEmit && npm run build; manual visual sanity deferred to the user on 0.1.8 (same posture).
</requirements>

<acceptance-ideas>
- Kits/Search/Settings labels read cleanly over the sidebar at rest — no watermark under text anywhere in the sidebar.
- The Kerrigan strip is still there, visible bottom-left, fading into the panel — moody but not fighting the nav.
- Tree rows look native to the sidebar (no floating black box); collapse/expand + register flows work as before.
- Contrast: every label ≥ the muted floor on surface (4.8:1).
</acceptance-ideas>
