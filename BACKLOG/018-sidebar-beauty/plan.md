---
feature: 018
slug: sidebar-beauty
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Plan 018 — Sidebar beauty rework (left menu design)

<approach>
One web-only task: bring the sidebar into compliance with the project's own brand-book — the Kerrigan art layer gets the book's mask (fade into the surface color, lower opacity), the tree's spot backdrop is removed, and the four nav rows unify typography. All behavior (011 menu, tree persistence, register modal) is frozen.
</approach>

<contract>
- app-sidebar.tsx: Kerrigan opacity 0.80 → ~0.35, mask-image gradient fading to #14141c (surface) over ~30%→98% width; remove bg-black/45 backdrop-blur-sm from the tree container; Projects row typography aligns with Kits/Search/Settings (chevron/tree affordances stay); spacing rhythm per brand-book.md:98-99; quote block unchanged.
- project-tree.tsx: class alignment only if hierarchy demands (folders muted xs / projects primary xs / headers 11px uppercase) — no structural changes.
- NAV_ITEMS untouched; mobile-nav untouched.
</contract>

<tasks>
- [ ] 01-sidebar-brandbook-rework — art layer mask/opacity, spot-backdrop removal, nav typography unification + verification → tasks/01-sidebar-brandbook-rework.md
</tasks>

<risks>
- Pure-visual change verified only by build + review — the visual pass lands with 0.1.8 (user).
- The mask-to-surface trick relies on the surface token value inside an arbitrary-value Tailwind class — keep the hex in sync with globals.css or use a CSS variable reference.
</risks>

<verification>
- lint + tsc + build green; 011 behaviors intact by code review (NAV_ITEMS, persistence keys, modal).
</verification>
