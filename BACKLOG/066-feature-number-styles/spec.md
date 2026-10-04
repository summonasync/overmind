---
feature: 066
slug: feature-number-styles
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 066 — Feature-number styles per project (KEY-039 / #039 / plain) + вкладка project settings — один форматер для всех поверхностей

> Grooming 2026-10-04 (user in session). Foundation exists: `formatFeatureNumber` (shared/lib/feature-number.ts, `#NNN` hardcoded) + slug parser from 065; 062 put the number on task titles.

## Goal

One formatter for all surfaces, style chosen per project: `KEY-039` / `#039` / plain `039`. The brand decision (065) — the identifier is NOT a status — stays; only the shape becomes configurable.

## Decisions

- **Two project settings:** `numberStyle` enum (`hash` default | `key` | `plain`) + `numberPrefix` string (used by `key`; validated `[A-Z][A-Z0-9_]{1,15}` uppercase — the «KEY» part; empty prefix falls back to the project's lowercase name slug-cased? No — prefix REQUIRED for `key` style, validator enforces non-empty). Migration AddProjectNumberStyle with zero-impact defaults.
- **Single formatter, single source:** `formatFeatureNumber(slug, style, prefix)` in shared/lib; every surface (card badge, task titles, notification rows, any future consumer) goes through it — no local string templates. Server does NOT render numbers into wire strings (numbers stay UI cosmetics; MCP echoes untouched).
- **Settings UI:** a section on the project settings tab (063/066 live in the same settings page family): style select + prefix input (visible when `key`), live preview on a real feature number from the board.
- Per-project override of the system-wide default is the whole feature — no per-feature or per-view overrides.

## Non-goals

- Server-side number rendering; renaming slugs; per-feature style overrides; numeric ordering changes.

<log>
- 2026-10-04 [agent] — groomed with the user in session: enum+prefix project settings, single shared formatter, key-style prefix required and validated, live preview in settings.
- 2026-10-04 [agent] — validated with human proof: notification 6aafb601-5bad-4186-982f-5b51f6894a95 replied 2026-10-04 11:48:57 UTC
</log>