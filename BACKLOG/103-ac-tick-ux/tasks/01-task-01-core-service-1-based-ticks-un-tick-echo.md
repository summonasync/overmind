---
feature: 103
task: 01
slug: ac-tick-ux
status: todo
review: light
created: 2026-10-04
brief: Core: TickAc 1-based, UntickAc op, FormatAcItems 1-based, tick/untick echo, matrix tests
---

# Task 01 — Core + service: 1-based ticks + un-tick + echo

<goal>
Move AC item indexing to 1-based in the domain and add the un-tick operation with a tick echo.
</goal>

<scope>
- TickAc: index semantics 1-based (text path unchanged).
- New UntickAc: by acText (primary) or 1-based acIndex; clears [x]→[ ]; already-unticked → no-op (honest «already unticked»); markerless item → TickItemNotCheckable.
- FormatAcItems renders 1-based indices (400 diagnostics).
- Patch result carries tickedIndex + tickedText echo (both tick and untick).
- Matrix tests: tick by 1-based index/text, retick no-op, untick all paths, markerless 400, out-of-range 400 with 1-based listing.
</scope>

<anti-scope>
- Doors and descriptions (task 02); parser (105).
</anti-scope>

<acceptance-criteria>
- [ ] TickAc resolves index 1 as the FIRST item; out-of-range 400 lists items 1-based.
- [ ] UntickAc: text, index, already-unticked no-op, markerless 400 — pinned.
- [ ] Patch echo carries tickedIndex+tickedText for tick and untick.
- [ ] Full matrix green.
</acceptance-criteria>

<refs>
- Spec 3f42d84f; plan 4a117b99 (task 1).
</refs>