---
feature: 026
slug: entries-read-mode
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 026 — Entries read mode: inline expand/collapse + fullscreen view by title click

> Grooming 2026-10-04 (user in session). Original user request (visual pass, 0.1.8): entries open only in edit mode today; reading should not require an editor.

## Decisions

- **Inline expand/collapse:** clicking the row (or a chevron) expands the entry body in place — full markdown render with type colors and tags (same renderer as edit-preview); clicking again collapses. One entry expanded at a time is NOT enforced — independent toggles (cheap, no state coupling).
- **Fullscreen read:** clicking the TITLE opens the xl read-only modal (014 task-read-mode pattern): full markdown, meta row (type/tags/dates), Edit button jumping into the existing edit flow. Esc and backdrop close.
- The row Edit button stays exactly as is; the edit flow is untouched.
- Web-only: everything needed is already in the entry response; zero backend.

## Non-goals

- Backend changes; realtime/collab editing; per-entry scroll position memory.

<log>
- 2026-10-04 [agent] — groomed with the user in session: inline expand (independent toggles), title-click fullscreen read on the 014 pattern, edit flow untouched, web-only.
- 2026-10-04 [agent] — validated with human proof: notification 4e120bf8-9950-4c4a-a652-d0123bee3190 replied 2026-10-04 12:12:54 UTC
</log>