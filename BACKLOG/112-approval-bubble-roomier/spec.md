---
feature: 112
slug: approval-bubble-roomier
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 112 — Approval bubble: up to 800 chars of text, bigger window

> User dogfood feedback 2026-10-04, approved in chat («До 800 символов легко»).

## Goal

The approval bubble stops being a 4-line teaser: text up to 800 characters is readable right in the bubble, and the window grows to carry it comfortably.

## Decisions

- Text cap: 800 characters — a client-side slice with «…» on longer texts (server still allows 2000; the bubble previews up to 800, the full text stays one click away in the notification center as today).
- line-clamp-4 → line-clamp-none within that 800-char bound (the bound is the clamp).
- Window: max width bumped one Tailwind step so 800 chars don't render as a skinny column; internal padding/rhythm per house style; mobile unchanged (full-width sheet behavior stays).
- Only the bubble: the notification center rows keep their current density (that surface already expands on click).

## Non-goals

- Server-side text cap changes; per-kind sizes; the 095 attention surfaces (separate module).

<log>
- 2026-10-04 [agent] — user dogfood feedback in session: 800-char text bound, bigger window, clamp removed within the bound; notification center untouched.
- 2026-10-04 [agent] — validated with human proof: notification a134cf29-0827-4727-bd45-a0cfbce2b4e7 replied 2026-10-04 16:55:29 UTC
</log>