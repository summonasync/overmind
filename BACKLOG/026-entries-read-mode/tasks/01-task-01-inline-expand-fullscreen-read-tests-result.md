---
feature: 026
task: 01
slug: entries-read-mode
status: todo
review: light
created: 2026-10-04
brief: Web: inline expand + title-click fullscreen read + tests + result (Simple path)
---

# Task 01 — Inline expand + fullscreen read + tests + result

<goal>
Entries become readable without opening the editor.
</goal>

<scope>
- Inline expand/collapse: row/chevron click toggles the body in place — full markdown render with type colors and tags (same renderer as edit-preview); independent toggles; chevron a11y (aria-expanded).
- Fullscreen read: title click opens the xl read-only modal (014 pattern) — meta row (type/tags/dates), full markdown, Edit button into the existing flow; Esc/backdrop close.
- Row Edit button and the edit flow untouched.
- Web tests: expand/collapse, modal content, edit entry points; result artifact.
</scope>

<anti-scope>
- Backend changes; realtime; scroll-position memory.
</anti-scope>

<acceptance-criteria>
- [ ] Expand/collapse pinned; modal content + Edit entry pinned; edit flow unchanged — pinned.
- [ ] Gates green (lint/tsc/node test); light review pass.
- [ ] Result artifact drafted.
</acceptance-criteria>

<refs>
- Spec 4c16515a; plan 79cd95ff; 014 read-mode pattern.
</refs>