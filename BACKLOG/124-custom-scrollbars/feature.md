---
feature: 124
slug: custom-scrollbars
status: review
priority: p0
version: 1
created: 2026-10-06
---

# Feature 124 — Custom scrollbars app-wide: styled thin scrollbar instead of the stock default

## Goal

The apps ship with the browser default scrollbars (full-width grey tracks, OS-stock thumbs) — visually loud against the custom Tailwind surface system, especially on dark backgrounds. Replace them app-wide with a styled, brand-consistent scrollbar.

## Requirements

- One shared global style (single source, not per-component overrides): thin scrollbar (track transparent or surface-toned, thumb brand-muted with hover state).
- Webkit (`::-webkit-scrollbar*`) + Firefox (`scrollbar-width`/`scrollbar-color`) coverage — must degrade honestly on engines without one of the two.
- applies to page-level scroll AND inner scroll containers (modals, lists, the notification center) — no element should keep the stock look.
- No JS — pure CSS in the global layer (Tailwind 4 global css), zero runtime cost.
- Density/contrast must stay AA-readable: thumb visible on both light and dark themes.

## Non-goals

- Custom scroll physics / JS scroll-jacking.
- Per-project scrollbar theming (one brand look for the app).
- Mobile/touch scrollbars (overlay scrollbars on touch are the OS's job).

<log>
- 2026-10-06 [agent] — Van by claude claimed the feature
- 2026-10-06 [auto] — artifact created → ready
- 2026-10-06 [auto] — task 01 taken → inProgress
- 2026-10-06 [auto] — all tasks done + spec + plan validated → review
</log>
