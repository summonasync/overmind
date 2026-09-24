---
feature: 064
slug: board-quick-search
status: draft
priority: p0
version: 1
created: 2026-09-23
---

# Feature 064 — Board quick search: client-side search row with Enter/Esc/Shift+Esc contract

User request 2026-09-23 (session): a quick search box on the planning board. The board is at 63+ features — finding one by eye no longer scales.

Requirements:
- **Fully client-side** filter if the board data is already loaded — no API round-trip required for v1.
- Placement: second row under the existing filters, or wherever the UX/UI design says is better — implementer's call, keep it consistent with the board's filter row.
- Search input + a search button.
- Keyboard: **Enter** applies the search; **Esc** clears the search (input + results); **Shift+Esc** clears ALL filters (search + status/state filters) in one stroke.
- Matches against feature number, slug, title (and body text if already client-side — implementer's judgment on cost).

Note: Enter/Esc/Shift+Esc semantics come from the user verbatim — treat them as acceptance, not suggestion.